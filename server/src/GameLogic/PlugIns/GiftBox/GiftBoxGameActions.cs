// <copyright file="GiftBoxGameActions.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Persistence;

/// <summary>
/// Connects authenticated in-world players to their Gift Box mailbox under the player's exclusive persistence lock.
/// Never reveals staged goods before confirmed persistence, disconnects BEFORE releasing the lock on an uncertain save,
/// and refreshes the live inventory cache without duplicating entity insertions.
/// </summary>
/// <param name="rows">Optional player-state backend (defaults to shared host row store).</param>
/// <param name="clock">Optional authoritative clock.</param>
/// <param name="activity">Optional activity log sink (defaults to ActivityLogStore).</param>
public sealed class GiftBoxGameActions(
    IHnmoPlayerStateRowBackend? rows = null,
    TimeProvider? clock = null,
    Action<ActivityLogEntry>? activity = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly IHnmoPlayerStateRowBackend? _injectedRows = rows;
    private IHnmoPlayerStateRowBackend? _rows = rows;

    private IHnmoPlayerStateRowBackend Rows => this._rows ??= HnmoPlayerStateRowStoreRegistry.GetBackend(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName));

    /// <summary>Executes an own-account mailbox read or live collection under the player's exclusive persistence lock.</summary>
    /// <param name="player">The authenticated in-world player.</param>
    /// <param name="request">The validated bounded selection.</param>
    /// <returns>The projected state JSON, or <c>null</c> if the session disconnected due to an unconfirmed save or became invalid.</returns>
    public ValueTask<string?> HandleAsync(Player player, GiftBoxRequest request)
        => this.HandleAsync(player, request, null);

    /// <summary>Executes an own-account mailbox read or live collection with an optional custom save delegate (for uncertain-save testing).</summary>
    /// <param name="player">The authenticated in-world player.</param>
    /// <param name="request">The validated bounded selection.</param>
    /// <param name="saveOverride">Optional save override for tests.</param>
    /// <returns>The projected state JSON, or <c>null</c> when the connection was closed for recovery.</returns>
    internal ValueTask<string?> HandleAsync(Player player, GiftBoxRequest request, Func<ValueTask<bool>>? saveOverride)
        => player.RunPersistenceExclusiveAsync<string?>(async () =>
        {
            if (!request.IsValid
                || player.Account is not { IsBot: false } account
                || player.SelectedCharacter is not { } character
                || player.PlayerState.CurrentState != PlayerState.EnteredWorld)
            {
                return null;
            }

            var context = player.PersistenceContext;
            var config = player.GameContext.Configuration;
            var dispatch = new GiftBoxDispatch(this.Rows);
            var now = this._clock.GetUtcNow().UtcDateTime;

            string? status = null;
            GiftBoxReceipt? confirmedReceipt = null;
            if (request.Action == "collect")
            {
                var collection = new GiftBoxCollection(dispatch, config, player.Logger);
                var result = await collection.CollectAsync(
                    context,
                    account,
                    request.LetterId!.Value,
                    character.GetId(),
                    now,
                    saveOverride ?? (() => context.SaveChangesAsync())).ConfigureAwait(false);

                status = result.Status;
                if (status == "Pending" || GiftBoxCollection.HasUnconfirmed(context))
                {
                    // Uncertain live delivery: disconnect BEFORE releasing the persistence lock so unconfirmed
                    // mutations are never observed or overwritten by later live actions.
                    await player.DisconnectAsync().ConfigureAwait(false);
                    return null;
                }

                confirmedReceipt = result.Receipt;
            }
            else if (GiftBoxCollection.HasUnconfirmed(context))
            {
                await player.DisconnectAsync().ConfigureAwait(false);
                return null;
            }

            if (confirmedReceipt is not null)
            {
                var bagStorage = player.Inventory as Storage;
                var invItems = character.Inventory?.Items;
                var activityLog = status == "Applied" ? ActivityLogPlugIn.Get(player.GameContext) : null;
                if (invItems is not null)
                {
                    foreach (var itemId in confirmedReceipt.ItemIds)
                    {
                        var item = invItems.FirstOrDefault(i => i.GetId() == itemId);
                        if (item is null)
                        {
                            continue;
                        }

                        bagStorage?.TryIncludeStoredBagItem(item);
                        activityLog?.RecordGrant(player, item, "GiftBox");
                        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(view => view.ItemAppearAsync(item)).ConfigureAwait(false);
                    }
                }

                if (confirmedReceipt.Zen > 0)
                {
                    await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(view => view.UpdateMoneyAsync()).ConfigureAwait(false);
                }

                this.RecordCollectActivity(account.LoginName, character.Name, confirmedReceipt, now);
            }

            return await GiftBoxStateWriter.WriteAsync(dispatch, config, context, account, request, now, status).ConfigureAwait(false);
        });

    private static object BuildActivityDetails(GiftBoxReceipt receipt, string source) => new
    {
        kind = "gift-box-collect",
        source,
        letterId = receipt.LetterId,
        coins = receipt.Coins,
        coinType = receipt.CoinType.ToString(),
        zen = receipt.Zen,
        items = receipt.ItemIds.Count,
    };

    private void RecordCollectActivity(string accountName, string characterName, GiftBoxReceipt receipt, DateTime timestampUtc)
    {
        try
        {
            var entry = new ActivityLogEntry
            {
                TimestampUtc = timestampUtc,
                Kind = ActivityKind.Administration,
                Account = accountName,
                Character = characterName,
                ItemSummary = receipt.TitleEn,
                Details = JsonSerializer.Serialize(BuildActivityDetails(receipt, "live")),
            };

            if (activity is not null)
            {
                activity(entry);
            }
            else if (this._injectedRows is null)
            {
                ActivityLogStore.Open(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName)).Append(entry);
            }
        }
        catch
        {
            // Non-fatal: the reward is already durably saved with its receipt.
        }
    }
}
