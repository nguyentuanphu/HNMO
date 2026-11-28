// <copyright file="BankTopUpService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Text.RegularExpressions;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using Microsoft.Extensions.Logging;

/// <summary>
/// The outcome of a reported transfer.
/// </summary>
public enum BankTopUpResult
{
    /// <summary>The coins were added.</summary>
    Credited,

    /// <summary>This transaction was already turned into coins (a repeated report, or reported by both services).</summary>
    AlreadyCredited,

    /// <summary>The message has no account name after the transfer word.</summary>
    NoAccountInMessage,

    /// <summary>The account named in the message doesn't exist.</summary>
    AccountNotFound,

    /// <summary>The amount is below the minimum.</summary>
    BelowMinimum,

    /// <summary>The transfer names a waiting top-up but brings less than it asked for; the host decides by hand.</summary>
    BelowRequested,

    /// <summary>There's no top-up of this id.</summary>
    NotFound,

    /// <summary>The top-up was rejected (manual confirmation refused; a later transfer still credits it).</summary>
    Rejected,
}

/// <summary>Why a waiting top-up couldn't be created for the top-up page.</summary>
public enum BankTopUpCreateResult
{
    /// <summary>Created, or the same waiting one given again.</summary>
    Created,

    /// <summary>Bank top-ups are off or have no account.</summary>
    Off,

    /// <summary>The account doesn't exist.</summary>
    AccountNotFound,

    /// <summary>The amount is below the minimum or above 100,000,000 VND.</summary>
    BadAmount,

    /// <summary>The chosen bank account isn't one of the host's.</summary>
    UnknownBankAccount,

    /// <summary>The account or the server has too many waiting top-ups.</summary>
    TooMany,
}

/// <summary>
/// Turns transfers reported by Casso or SePay into cash shop coins for the account named in the transfer
/// message ("HNMO test2"), at the rate of the <see cref="BankTopUpPlugIn"/>. Every bank transaction counts
/// once: the grant's reference is the bank's reference (or the service's id), which the database keeps unique.
/// </summary>
public class BankTopUpService
{
    /// <summary>The largest amount one top-up may ask for, in VND.</summary>
    public const long MaximumVnd = 100_000_000;

    /// <summary>One confirmation at a time per top-up in this process; the grant's unique reference covers the rest.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    private readonly CashShopCoinService _coins;
    private readonly ILogger<BankTopUpService> _logger;
    private readonly BankTopUpPendingStore? _pendingStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankTopUpService"/> class.
    /// </summary>
    /// <param name="coins">The cash shop coin service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="pending">The waiting top-ups; null for the server's state file.</param>
    public BankTopUpService(CashShopCoinService coins, ILogger<BankTopUpService> logger, BankTopUpPendingStore? pending = null)
    {
        this._coins = coins;
        this._logger = logger;
        this._pendingStore = pending;
    }

    /// <summary>Gets the waiting top-ups.</summary>
    public BankTopUpPendingStore Pending => this._pendingStore ?? BankTopUpPendingStore.Open();

    /// <summary>
    /// Finds the account name after the transfer word in a transfer message. Banks often add their own
    /// text, change the case or replace spaces with dots, e.g. "MBVCB.123456.HNMO.test2.CT tu ...".
    /// </summary>
    /// <param name="message">The transfer message.</param>
    /// <param name="prefix">The transfer word, e.g. "HNMO".</param>
    /// <returns>The account name as written, or <c>null</c>.</returns>
    public static string? FindAccountName(string? message, string prefix)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        var match = Regex.Match(
            message,
            $@"(?<![A-Za-z0-9]){Regex.Escape(prefix.Trim())}[\s.\-_:]*([A-Za-z0-9]{{3,10}})(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// The coins an amount buys: whole coins only, the rest is dropped.
    /// </summary>
    /// <param name="amountVnd">The amount in VND.</param>
    /// <param name="vndPerCoin">The VND per coin.</param>
    /// <returns>The coins.</returns>
    public static int CoinsFor(long amountVnd, int vndPerCoin)
    {
        if (amountVnd <= 0 || vndPerCoin <= 0)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, amountVnd / vndPerCoin);
    }

    /// <summary>
    /// The grant reference of a transfer: the bank's reference when known, so that the same transaction
    /// reported by both services counts once; otherwise the service's own id.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <returns>The reference.</returns>
    public static string ReferenceOf(BankTransfer transfer)
    {
        return string.IsNullOrWhiteSpace(transfer.BankReference)
            ? $"topup:{transfer.Provider}:{transfer.ProviderId}"
            : $"topup:bank:{transfer.BankReference.Trim().ToUpperInvariant()}";
    }

    /// <summary>
    /// Adds the coins of a reported transfer.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <param name="configuration">The top-up configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What happened.</returns>
    public async ValueTask<BankTopUpResult> CreditAsync(BankTransfer transfer, BankTopUpConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (transfer.AmountVnd < configuration.MinimumVnd)
        {
            this._logger.LogInformation("Top-up {Provider} {Id}: {Amount} VND is below the minimum of {Minimum} VND; not turned into coins.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, configuration.MinimumVnd);
            return BankTopUpResult.BelowMinimum;
        }

        foreach (var code in BankTopUpPendingStore.CodesIn(transfer.Message, configuration.TransferPrefix))
        {
            if (this.Pending.FindByCode(code) is not { } pending)
            {
                continue;
            }

            if (transfer.AmountVnd < pending.AmountVnd)
            {
                this._logger.LogWarning("Top-up {Provider} {Id}: {Amount} VND for {Code}, which asked for {Requested} VND; not credited, decide on the Bank top-ups page.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, pending.Code, pending.AmountVnd);
                return BankTopUpResult.BelowRequested;
            }

            var result = await this.GrantPendingAsync(pending, CoinsFor(transfer.AmountVnd, configuration.VndPerCoin), transfer.AmountVnd, configuration, transfer.Provider, $"Bank transfer {transfer.AmountVnd:N0} VND ({transfer.Provider}, {pending.Code})", cancellationToken).ConfigureAwait(false);
            return result;
        }

        if (FindAccountName(transfer.Message, configuration.TransferPrefix) is not { } written)
        {
            this._logger.LogWarning("Top-up {Provider} {Id}: {Amount} VND without an account name in the message \"{Message}\"; add the coins by hand.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, transfer.Message);
            return BankTopUpResult.NoAccountInMessage;
        }

        var coins = CoinsFor(transfer.AmountVnd, configuration.VndPerCoin);
        var reason = $"Bank transfer {transfer.AmountVnd:N0} VND ({transfer.Provider})";
        var reference = ReferenceOf(transfer);

        // Banks often change the case of the message: try it as written, then lower and upper case.
        foreach (var login in new[] { written, written.ToLowerInvariant(), written.ToUpperInvariant() }.Distinct())
        {
            var (status, _) = await this._coins.GrantAsync(login, configuration.CoinType, coins, reason, reference, transfer.Provider, cancellationToken).ConfigureAwait(false);
            switch (status)
            {
                case CashShopCoinGrantStatus.Created:
                    var milestoneStore = TopUpMilestoneStore.Open(TopUpMilestonesConfiguration.DefaultFileName);
                    var milestoneEngine = new TopUpMilestoneEngine(new TopUpMilestonesConfiguration(), milestoneStore, enabled: true);
                    milestoneEngine.RecordTopUp(login, transfer.AmountVnd, reference, $"{transfer.Provider} {transfer.ProviderId}", DateTime.UtcNow);
                    lock (milestoneStore.Lock)
                    {
                        milestoneStore.SaveIfChanged();
                    }

                    this._logger.LogInformation("Top-up {Provider} {Id}: {Amount} VND -> {Coins} {CoinType} for {Login}.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, coins, configuration.CoinType, login);
                    return BankTopUpResult.Credited;
                case CashShopCoinGrantStatus.AlreadyGranted:
                case CashShopCoinGrantStatus.ReferenceUsedByOtherAccount:
                    return BankTopUpResult.AlreadyCredited;
                case CashShopCoinGrantStatus.AccountNotFound:
                    continue;
            }
        }

        this._logger.LogWarning("Top-up {Provider} {Id}: {Amount} VND for account \"{Login}\", which doesn't exist; add the coins by hand.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, written);
        return BankTopUpResult.AccountNotFound;
    }

    /// <summary>
    /// Creates a waiting top-up for the top-up page: the player transfers the amount with its code in the message.
    /// </summary>
    /// <param name="login">The account's login name as the player wrote it.</param>
    /// <param name="amountVnd">The amount.</param>
    /// <param name="bin">The bank id of the chosen host account.</param>
    /// <param name="number">The chosen host account number.</param>
    /// <param name="configuration">The top-up configuration (null when off).</param>
    /// <param name="ticket">The browser's ticket for following it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What happened, and the top-up.</returns>
    public async ValueTask<(BankTopUpCreateResult Result, BankTopUpPending? TopUp)> CreatePendingAsync(string login, long amountVnd, string bin, string number, BankTopUpConfiguration? configuration, string ticket, CancellationToken cancellationToken = default)
    {
        if (configuration is null || configuration.UsableAccounts() is not { Count: > 0 } accounts)
        {
            return (BankTopUpCreateResult.Off, null);
        }

        if (amountVnd < configuration.MinimumVnd || amountVnd > MaximumVnd)
        {
            return (BankTopUpCreateResult.BadAmount, null);
        }

        if (accounts.FirstOrDefault(a => a.Bin == bin.Trim() && a.Number.Trim() == number.Trim()) is not { } account)
        {
            return (BankTopUpCreateResult.UnknownBankAccount, null);
        }

        var written = login.Trim();
        string? found = null;
        foreach (var candidate in new[] { written, written.ToLowerInvariant(), written.ToUpperInvariant() }.Distinct())
        {
            if (candidate.Length is >= 3 and <= 10 && await this._coins.GetSummaryAsync(candidate, cancellationToken).ConfigureAwait(false) is { } summary)
            {
                found = summary.LoginName;
                break;
            }
        }

        if (found is null)
        {
            return (BankTopUpCreateResult.AccountNotFound, null);
        }

        var (topUp, refusal) = this.Pending.Create(found, amountVnd, CoinsFor(amountVnd, configuration.VndPerCoin), account, configuration.TransferPrefix, ticket, DateTime.UtcNow);
        return topUp is null ? (BankTopUpCreateResult.TooMany, null) : (BankTopUpCreateResult.Created, topUp);
    }

    /// <summary>The host saw the transfer: adds the coins of a waiting top-up, once whoever confirms first.</summary>
    /// <param name="id">The top-up's id.</param>
    /// <param name="configuration">The top-up configuration.</param>
    /// <param name="confirmedBy">The admin's name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What happened.</returns>
    public async ValueTask<BankTopUpResult> ConfirmPendingAsync(string id, BankTopUpConfiguration configuration, string confirmedBy, CancellationToken cancellationToken = default)
    {
        if (this.Pending.Find(id) is not { } pending)
        {
            return BankTopUpResult.NotFound;
        }

        if (pending.Status == BankTopUpPendingStatus.Credited)
        {
            return BankTopUpResult.AlreadyCredited;
        }

        return await this.GrantPendingAsync(pending, pending.Coins, pending.AmountVnd, configuration, confirmedBy, $"Bank transfer {pending.AmountVnd:N0} VND (confirmed by {confirmedBy}, {pending.Code})", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The host saw no (or a wrong) transfer: rejects a waiting top-up. A credited one stays credited.</summary>
    /// <param name="id">The top-up's id.</param>
    /// <param name="rejectedBy">The admin's name.</param>
    /// <returns>What happened.</returns>
    public BankTopUpResult RejectPending(string id, string rejectedBy) =>
        this.Pending.Finish(id, BankTopUpPendingStatus.Rejected, rejectedBy, null, DateTime.UtcNow) switch
        {
            null => BankTopUpResult.NotFound,
            BankTopUpPendingStatus.Credited => BankTopUpResult.AlreadyCredited,
            _ => BankTopUpResult.Rejected,
        };

    private async ValueTask<BankTopUpResult> GrantPendingAsync(BankTopUpPending pending, int coins, long amountVnd, BankTopUpConfiguration configuration, string by, string reason, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(pending.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.GrantPendingLockedAsync(pending, coins, amountVnd, configuration, by, reason, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<BankTopUpResult> GrantPendingLockedAsync(BankTopUpPending pending, int coins, long amountVnd, BankTopUpConfiguration configuration, string by, string reason, CancellationToken cancellationToken)
    {
        var (status, _) = await this._coins.GrantAsync(pending.Login, configuration.CoinType, coins, reason, pending.Reference, by, cancellationToken).ConfigureAwait(false);
        switch (status)
        {
            case CashShopCoinGrantStatus.Created:
                this.Pending.Finish(pending.Id, BankTopUpPendingStatus.Credited, by, amountVnd, DateTime.UtcNow);
                var milestoneStore = TopUpMilestoneStore.Open(TopUpMilestonesConfiguration.DefaultFileName);
                var milestoneEngine = new TopUpMilestoneEngine(new TopUpMilestonesConfiguration(), milestoneStore, enabled: true);
                milestoneEngine.RecordTopUp(pending.Login, amountVnd, pending.Reference, $"{by} {pending.Code}", DateTime.UtcNow);
                lock (milestoneStore.Lock)
                {
                    milestoneStore.SaveIfChanged();
                }

                this._logger.LogInformation("Top-up {Code}: {Amount} VND -> {Coins} {CoinType} for {Login} ({By}).", pending.Code, amountVnd, coins, configuration.CoinType, pending.Login, by);
                return BankTopUpResult.Credited;
            case CashShopCoinGrantStatus.AlreadyGranted:
            case CashShopCoinGrantStatus.ReferenceUsedByOtherAccount:
                this.Pending.Finish(pending.Id, BankTopUpPendingStatus.Credited, by, amountVnd, DateTime.UtcNow);
                return BankTopUpResult.AlreadyCredited;
            default:
                this._logger.LogWarning("Top-up {Code}: the account {Login} no longer exists; not credited.", pending.Code, pending.Login);
                return BankTopUpResult.AccountNotFound;
        }
    }
}
