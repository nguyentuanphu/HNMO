// <copyright file="GiftBoxCollection.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence;

/// <summary>Saves delivered goods, balances and their replay proof in the same account transaction.</summary>
/// <remarks>The trusted adapter must authenticate and own a fresh account: hold its live persistence lock
/// or an exclusive offline login reservation. Live adapters must not expose staged items before confirmation,
/// and must close the live connection on an uncertain save before releasing the lock. A pending context
/// retains all mutations and may only flush that same receipt; never restore the balance alone.</remarks>
/// <param name="dispatch">The existing immutable mailbox.</param>
/// <param name="configuration">The authoritative inventory limits and item definitions.</param>
/// <param name="logger">The operational logger; no credentials or private mail bodies are logged.</param>
public sealed class GiftBoxCollection(GiftBoxDispatch dispatch, GameConfiguration configuration, ILogger logger)
{
    /// <summary>The receipt owner, distinct from arbitrary host coin grants.</summary>
    public const string GrantedBy = "Gift box";

    private const string ReceiptKind = "gift-box-v1";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    private static readonly ConditionalWeakTable<IPlayerContext, Dictionary<Guid, CashShopCoinGrant>> Tracked = new();

    /// <summary>Checks whether this exclusively owned context retains an unconfirmed delivery.</summary>
    /// <param name="context">The serialized account context.</param>
    /// <returns>Whether it must reconcile or close before unrelated account mutations.</returns>
    public static bool HasUnconfirmed(IPlayerContext context) => Tracked.TryGetValue(context, out var receipts) && receipts.Count > 0;

    /// <summary>Checks whether the selected letter has staged proof which must not be displayed as confirmed.</summary>
    /// <param name="context">The serialized account context.</param>
    /// <param name="letterId">The selected immutable letter.</param>
    /// <returns>Whether its proof is still awaiting a confirmed save.</returns>
    public static bool IsUnconfirmed(IPlayerContext context, Guid letterId) => Tracked.TryGetValue(context, out var receipts) && receipts.ContainsKey(letterId);

    /// <summary>Reads only recognized account-bound delivery proof for history and mailbox projection.</summary>
    /// <param name="row">The stored coin receipt.</param>
    /// <returns>The proof, or null for an ordinary or malformed row.</returns>
    public static GiftBoxReceipt? ReadReceipt(CashShopCoinGrant row)
    {
        var proof = ReadReceiptMetadata(row.Reason);
        return row.GrantedBy == GrantedBy && row.AppliedAt is not null && proof is not null
            && proof.AccountId == row.AccountId && proof.Coins == row.Amount && proof.CoinType == row.CoinType
            && row.Reference == Reference(proof.AccountId, proof.LetterId) ? proof : null;
    }

    /// <summary>Reads bounded display metadata only; it does not prove delivery without validating the complete stored row.</summary>
    /// <param name="reason">The stored receipt text.</param>
    /// <returns>Recognized metadata, or null. Collection authorization must use <see cref="ReadReceipt"/> instead.</returns>
    public static GiftBoxReceipt? ReadReceiptMetadata(string? reason)
    {
        if (reason is not { Length: > 0 and <= 16384 })
        {
            return null;
        }

        try
        {
            var proof = JsonSerializer.Deserialize<GiftBoxReceipt>(reason);
            return proof is { Kind: ReceiptKind, ItemIds: { Count: <= 50 }, TitleEn.Length: > 0 and <= 100, TitleVi.Length: > 0 and <= 100 }
                && proof.AccountId != Guid.Empty && proof.LetterId != Guid.Empty && proof.CharacterId != Guid.Empty
                && proof.ItemIds.All(id => id != Guid.Empty) && proof.ItemIds.Distinct().Count() == proof.ItemIds.Count
                && proof.Zen >= 0 && proof.Coins >= 0 && Enum.IsDefined(proof.CoinType)
                && !string.IsNullOrWhiteSpace(proof.TitleEn) && !string.IsNullOrWhiteSpace(proof.TitleVi) ? proof : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Collects a letter once for an owned character; a full bag leaves every reward owed.</summary>
    /// <param name="context">The exclusively owned player context.</param>
    /// <param name="account">The authenticated account from that context.</param>
    /// <param name="letterId">The existing letter; never a client-supplied reward.</param>
    /// <param name="characterId">The chosen owned character.</param>
    /// <param name="nowUtc">The authoritative UTC instant.</param>
    /// <param name="save">Saves this same account context; false/throw means uncertain.</param>
    /// <returns>The confirmed proof, pending recovery or a localized-adapter refusal code.</returns>
    public async ValueTask<GiftBoxCollectionResult> CollectAsync(IPlayerContext context, Account account, Guid letterId, Guid characterId, DateTime nowUtc, Func<ValueTask<bool>> save)
    {
        if (account.GetId() == Guid.Empty || letterId == Guid.Empty || characterId == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc)
        {
            return new("Invalid");
        }

        var gate = Gates.GetOrAdd(account.GetId(), _ => new(1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var tracked = Tracked.GetOrCreateValue(context);
            var wasTracked = tracked.TryGetValue(letterId, out var row);
            row ??= await context.GetCashShopCoinGrantByReferenceAsync(Reference(account.GetId(), letterId)).ConfigureAwait(false);
            if (row is not null)
            {
                if (ReadReceipt(row) is not { } proof || proof.AccountId != account.GetId())
                {
                    return new("Conflict");
                }

                if (wasTracked && proof.CharacterId != characterId)
                {
                    return new("Conflict");
                }

                if (wasTracked && !await this.TrySaveAsync(save, row.Reference!).ConfigureAwait(false))
                {
                    return new("Pending");
                }

                tracked.Remove(letterId);
                return new(wasTracked ? "Applied" : "AlreadyCollected", proof);
            }

            if (tracked.Count > 0)
            {
                return new("Pending");
            }

            var letter = dispatch.ReadLetter(account.GetId(), letterId);
            if (letter is null)
            {
                return new("NotFound");
            }

            if (letter.IsExpired(nowUtc))
            {
                return new("Expired");
            }

            var character = account.Characters.SingleOrDefault(value => value.GetId() == characterId);
            if (character?.Inventory is not { } inventory)
            {
                return new("InvalidCharacter");
            }

            var reward = letter.Reward;
            if (!Enum.IsDefined(reward.CoinType) || reward.Coins < 0 || reward.Zen < 0)
            {
                return new("InvalidReward");
            }

            var coins = account.GetCashShopCoins(reward.CoinType);
            if (coins < 0 || inventory.Money < 0 || (long)coins + reward.Coins > int.MaxValue
                || (long)inventory.Money + reward.Zen > configuration.MaximumInventoryMoney)
            {
                return new("BalanceFull");
            }

            var plan = await GiftBoxPacking.PlanAsync(configuration, inventory.Items, character.InventoryExtensions, reward).ConfigureAwait(false);
            if (plan.Status != GiftBoxPackingStatus.Ready)
            {
                return new(plan.Status.ToString());
            }

            var created = context.CreateNew<CashShopCoinGrant>();
            var items = new List<Item>();
            GiftBoxReceipt completed;
            try
            {
                foreach (var planned in plan.Items)
                {
                    // Capture each new root before copying options, so preparation failure cannot leave
                    // an unproved item for an unrelated later save to commit.
                    var item = context.CreateNew<Item>();
                    items.Add(item);
                    item.AssignValues(planned);
                    item.ItemSlot = planned.ItemSlot;
                }

                completed = new(ReceiptKind, account.GetId(), letterId, characterId, items.Select(item => item.GetId()).ToArray(), reward.Zen, reward.Coins, reward.CoinType, letter.TitleEn, letter.TitleVi);
                created.AccountId = account.GetId();
                created.Reference = Reference(account.GetId(), letterId);
                created.GrantedBy = GrantedBy;
                created.CoinType = reward.CoinType;
                created.Amount = reward.Coins;
                created.Reason = JsonSerializer.Serialize(completed);
                created.CreatedAt = nowUtc;
                created.AppliedAt = nowUtc;
                foreach (var item in items)
                {
                    inventory.Items.Add(item);
                }
            }
            catch
            {
                foreach (var item in items)
                {
                    inventory.Items.Remove(item);
                    context.Detach(item);
                }

                context.Detach(created);
                throw;
            }

            inventory.Money += reward.Zen;
            account.SetCashShopCoins(reward.CoinType, coins + reward.Coins);
            tracked.Add(letterId, created);
            if (!await this.TrySaveAsync(save, created.Reference).ConfigureAwait(false))
            {
                return new("Pending");
            }

            tracked.Remove(letterId);
            return new("Applied", completed);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string Reference(Guid accountId, Guid letterId) => $"mail:{accountId:N}:{letterId:N}";

    private async ValueTask<bool> TrySaveAsync(Func<ValueTask<bool>> save, string reference)
    {
        try
        {
            return await save().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gift box delivery {Reference} needs confirmation in the same account context.", reference);
            return false;
        }
    }
}
