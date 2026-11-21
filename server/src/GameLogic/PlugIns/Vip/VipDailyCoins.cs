// <copyright file="VipDailyCoins.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence;

/// <summary>Records daily bonuses as durable, uniquely referenced positive grants, without overwriting a live account.</summary>
/// <param name="persistence">The account's existing persistence provider.</param>
/// <param name="gameConfiguration">The canonical game configuration.</param>
/// <param name="wallet">The paid entitlement reader.</param>
/// <param name="store">The shared vip section, used only as a day-claim projection.</param>
/// <param name="logger">The operational logger.</param>
public sealed class VipDailyCoins(IPersistenceContextProvider persistence, GameConfiguration gameConfiguration, VipWallet wallet, VipStateStore store, ILogger logger)
{
    private const string ReceiptKind = "vip-daily-v1";
    private static readonly TimeZoneInfo Sydney = HnmoStateStore.ResolveTimeZone(null);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    /// <summary>Gets the same globally unique credit reference for every character, level and host for one Sydney day.</summary>
    /// <param name="accountId">The persistent account.</param>
    /// <param name="nowUtc">The server's UTC instant.</param>
    /// <returns>The daily reference.</returns>
    public static string ReferenceFor(Guid accountId, DateTime nowUtc) => $"vipdaily:{accountId:N}:{Day(nowUtc)}";

    /// <summary>Reads recognized daily metadata; an ordinary host reason stays ordinary text.</summary>
    /// <param name="reason">The saved reason.</param>
    /// <returns>The daily proof metadata or null.</returns>
    public static VipDailyReceipt? ReadReceipt(string? reason)
    {
        if (reason is null || reason.Length > 4096 || !reason.StartsWith('{'))
        {
            return null;
        }

        try
        {
            var receipt = JsonSerializer.Deserialize<VipDailyReceipt>(reason);
            return receipt is { Kind: ReceiptKind } ? receipt : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Creates at most one bonus credit; normal coin-grant application performs the later balance update.</summary>
    /// <param name="ownedContext">The authenticated account's exclusively owned context.</param>
    /// <param name="account">The authenticated account.</param>
    /// <param name="nowUtc">The server's UTC instant.</param>
    /// <param name="cancellationToken">Cancellation before creating the credit.</param>
    /// <returns>The durable reward outcome; Pending never means a credit has been confirmed.</returns>
    public async ValueTask<VipDailyResult> ClaimAsync(IPlayerContext ownedContext, Account account, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (account.GetId() == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc)
        {
            return new("DailyUnavailable");
        }

        var gate = Gates.GetOrAdd(account.GetId(), _ => new(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ClaimOwnedAsync(ownedContext, account, nowUtc, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string Day(DateTime nowUtc) => TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Sydney).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool Matches(CashShopCoinGrant entry, Guid accountId, string day, string reference)
        => entry.AccountId == accountId && entry.Reference == reference && entry.Amount > 0 && Enum.IsDefined(entry.CoinType)
        && ReadReceipt(entry.Reason) is { } proof && proof.AccountId == accountId && proof.SydneyDay == day;

    private async ValueTask<VipDailyResult> ClaimOwnedAsync(IPlayerContext ownedContext, Account account, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var accountId = account.GetId();
        var day = Day(nowUtc);
        var reference = ReferenceFor(accountId, nowUtc);
        using (var check = persistence.CreateNewPlayerContext(gameConfiguration))
        {
            if (await check.GetCashShopCoinGrantByReferenceAsync(reference, cancellationToken).ConfigureAwait(false) is { } previous)
            {
                if (!Matches(previous, accountId, day, reference))
                {
                    return new("Conflict");
                }

                this.Remember(account.LoginName, day);
                return new("AlreadyClaimed", previous.Amount);
            }
        }

        if (await wallet.ReadActiveAsync(ownedContext, account, nowUtc, cancellationToken).ConfigureAwait(false) is not { Intent.DailyCoins: > 0 } active
            || !Enum.IsDefined(active.Intent.DailyCoinType))
        {
            return new("DailyUnavailable");
        }

        var bonus = active.Intent.DailyCoins;
        var currency = active.Intent.DailyCoinType;
        long effective = account.GetCashShopCoins(currency);
        var pending = await ownedContext.GetPendingCashShopCoinGrantsAsync(accountId, cancellationToken).ConfigureAwait(false);
        foreach (var entry in pending.Where(value => value.CoinType == currency).OrderBy(value => value.CreatedAt))
        {
            effective = Math.Clamp(effective + entry.Amount, 0, int.MaxValue);
        }

        if (effective + bonus > int.MaxValue)
        {
            return new("BalanceFull");
        }

        try
        {
            using var context = persistence.CreateNewPlayerContext(gameConfiguration);
            if (await context.GetAccountIdByLoginNameAsync(account.LoginName, cancellationToken).ConfigureAwait(false) != accountId)
            {
                return new("DailyUnavailable");
            }

            var credit = context.CreateNew<CashShopCoinGrant>();
            credit.AccountId = accountId;
            credit.Reference = reference;
            credit.CoinType = currency;
            credit.Amount = bonus;
            credit.CreatedAt = nowUtc;
            credit.GrantedBy = "VIP";
            credit.Reason = JsonSerializer.Serialize(new VipDailyReceipt(ReceiptKind, accountId, day, active.Intent.OperationId, active.Intent.NameEn, active.Intent.NameVi));
            if (await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
            {
                this.Remember(account.LoginName, day);
                return new("DailyApplied", bonus);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "VIP daily credit {Reference} needs its durable result checked.", reference);
        }

        // Resolve a uniqueness race or an uncertain commit in a different context, never by the failed context's cache.
        using var probe = persistence.CreateNewPlayerContext(gameConfiguration);
        if (await probe.GetCashShopCoinGrantByReferenceAsync(reference, cancellationToken).ConfigureAwait(false) is { } recorded
            && Matches(recorded, accountId, day, reference))
        {
            this.Remember(account.LoginName, day);
            return new("DailyApplied", recorded.Amount);
        }

        return new("DailyPending");
    }

    private void Remember(string login, string day)
    {
        lock (store.SyncRoot)
        {
            store.Data.DailyClaims[login.Trim().ToUpperInvariant()] = day;
            try
            {
                store.Save();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "VIP daily claim projection needs retry; its database credit remains authoritative.");
            }
        }
    }
}
