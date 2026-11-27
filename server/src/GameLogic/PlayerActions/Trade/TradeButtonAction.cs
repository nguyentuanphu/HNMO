// <copyright file="TradeButtonAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Trade;

using System.Runtime.CompilerServices;
using System.Threading;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views.Trade;
using HNMO.Persistence;

/// <summary>
/// Action to change the trade button state.
/// </summary>
public class TradeButtonAction : BaseTradeAction
{
    /// <summary>
    /// A number per trader which gives every pair of traders the same order to lock their state machines in.
    /// </summary>
    private static readonly ConditionalWeakTable<ITrader, StrongBox<long>> LockOrder = new();

    private static long _lockOrderCounter;

    /// <summary>
    /// Tries to change the trade button state to the new <paramref name="tradeButtonState"/>.
    /// </summary>
    /// <param name="trader">The trader.</param>
    /// <param name="tradeButtonState">The new state of the trade button.</param>
    public ValueTask TradeButtonChangedAsync(ITrader trader, TradeButtonState tradeButtonState)
        => this.TradeButtonChangedAsync(trader, tradeButtonState, operationId: null);

    /// <summary>
    /// Tries to change the trade button state to the new <paramref name="tradeButtonState"/> with an explicit operation id.
    /// </summary>
    /// <param name="trader">The trader.</param>
    /// <param name="tradeButtonState">The new state of the trade button.</param>
    /// <param name="operationId">Optional idempotent asset operation id for crash recovery tests.</param>
    internal async ValueTask TradeButtonChangedAsync(ITrader trader, TradeButtonState tradeButtonState, Guid? operationId)
    {
        using var loggerScope = (trader as Player)?.Logger.BeginScope(this.GetType());
        var success = (tradeButtonState == TradeButtonState.Checked && await trader.PlayerState.TryAdvanceToAsync(PlayerState.TradeButtonPressed).ConfigureAwait(false))
                      || (tradeButtonState == TradeButtonState.Unchecked && await trader.PlayerState.TryAdvanceToAsync(PlayerState.TradeOpened).ConfigureAwait(false));
        if (!success)
        {
            return;
        }

        var tradingPartner = trader.TradingPartner;

        if (trader.PlayerState.CurrentState == PlayerState.TradeButtonPressed
            && tradingPartner is not null
            && tradingPartner.PlayerState.CurrentState == PlayerState.TradeButtonPressed)
        {
            // Both traders may press the button at the same time, on two connections: then both
            // see the other as pressed and both get here. The two state machines are locked in the
            // same order for both, so the second waits instead of deadlocking (each holding its own
            // lock and waiting for the other's), and then finds the trade finished.
            var (first, second) = InLockOrder(trader, tradingPartner);
            if (await this.InternalFinishTradeAsync(first, second, operationId).ConfigureAwait(false) is not { } result)
            {
                return;
            }

            if (result != TradeResult.Success)
            {
                await this.CancelTradeAsync(tradingPartner, false).ConfigureAwait(false);
                await this.CancelTradeAsync(trader, false).ConfigureAwait(false);
                (trader as Player)?.Logger.LogDebug($"Cancelled the trade because of unfinished state. trader: {trader.Name}, partner:{tradingPartner.Name}");
            }

            await trader.InvokeViewPlugInAsync<ITradeFinishedPlugIn>(p => p.TradeFinishedAsync(result)).ConfigureAwait(false);
            await tradingPartner.InvokeViewPlugInAsync<ITradeFinishedPlugIn>(p => p.TradeFinishedAsync(result)).ConfigureAwait(false);
        }
        else if (tradingPartner is not null)
        {
            await tradingPartner.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Checked)).ConfigureAwait(false);
        }
        else
        {
            // nothing to do.
        }
    }

    private static (ITrader First, ITrader Second) InLockOrder(ITrader trader, ITrader partner)
    {
        return LockOrder.GetValue(trader, _ => new StrongBox<long>(Interlocked.Increment(ref _lockOrderCounter))).Value
               < LockOrder.GetValue(partner, _ => new StrongBox<long>(Interlocked.Increment(ref _lockOrderCounter))).Value
            ? (trader, partner)
            : (partner, trader);
    }

    private static bool ExceedsMaximumMoney(ITrader receiver, int receivedMoney)
    {
        // A trader who receives no zen keeps their money, so an item-only trade is never refused,
        // even if the money is already above a limit which was lowered afterwards.
        if (receivedMoney <= 0)
        {
            return false;
        }

        var maximumMoney = receiver.GameContext?.Configuration?.MaximumInventoryMoney ?? int.MaxValue;
        return (long)receiver.Money + receivedMoney > maximumMoney;
    }

    private static async ValueTask<bool> TryAddItemsOfTradingPartnerAsync(ITrader trader)
    {
        if (trader.TradingPartner?.TemporaryStorage?.Items.Any() ?? false)
        {
            return await trader.Inventory!.TryTakeAllAsync(trader.TradingPartner.TemporaryStorage!).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Finishes the trade: checks it, moves the items and the zen and saves it.
    /// </summary>
    /// <param name="trader">The trader whose state machine is locked first.</param>
    /// <param name="tradingPartner">The other trader.</param>
    /// <returns>The result, or <c>null</c> if the other trader finished the trade in the meantime.</returns>
    private async ValueTask<TradeResult?> InternalFinishTradeAsync(ITrader trader, ITrader tradingPartner, Guid? operationId = null)
    {
        await using var context = await trader.PlayerState.TryBeginAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        await using var partnerContext = await tradingPartner.PlayerState.TryBeginAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        if (!ReferenceEquals(trader.TradingPartner, tradingPartner) || !ReferenceEquals(tradingPartner.TradingPartner, trader))
        {
            // The other trader's press finished (or cancelled) this trade while we waited for the locks.
            context.Allowed = false;
            partnerContext.Allowed = false;
            return null;
        }

        if (!context.Allowed || !partnerContext.Allowed)
        {
            context.Allowed = false;
            partnerContext.Allowed = false;
            (trader as Player)?.Logger.LogDebug($"Unexpected player states. {trader.Name}:{trader.PlayerState}, {tradingPartner.Name}:{tradingPartner.PlayerState}");
            return TradeResult.Cancelled;
        }

        using var itemContext = trader.GameContext.PersistenceContextProvider.CreateNewTradeContext();
        var traderItems = trader.TemporaryStorage!.Items.ToList();
        var tradePartnerItems = tradingPartner.TemporaryStorage!.Items.ToList();
        var traderMoney = trader.TradingMoney;
        var partnerMoney = tradingPartner.TradingMoney;

        // Checked here, and not only when an item is moved into the trade: the temporary storage
        // is shared with other windows (e.g. the chaos machine), so it may already contain items.
        // Both are checked, so that each trader gets the message about their own items.
        var traderItemsAreTradable = await this.CheckItemsAreTradableAsync(trader, traderItems).ConfigureAwait(false);
        var partnerItemsAreTradable = await this.CheckItemsAreTradableAsync(tradingPartner, tradePartnerItems).ConfigureAwait(false);
        if (!traderItemsAreTradable || !partnerItemsAreTradable)
        {
            return TradeResult.Cancelled;
        }

        // The escrowed zen is added to the inventory money of the partner below. Like in classic MU,
        // the trade fails when that would exceed the maximum inventory money - otherwise the money
        // grows past the configured limit, or even wraps around to a negative amount.
        if (ExceedsMaximumMoney(trader, partnerMoney) || ExceedsMaximumMoney(tradingPartner, traderMoney))
        {
            await this.SendMessageAsync(trader, nameof(PlayerMessage.TradeMoneyExceedsMaximum)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.TradeMoneyExceedsMaximum)).ConfigureAwait(false);
            return TradeResult.Cancelled;
        }

        this.AttachItemsToPersistenceContext(traderItems, itemContext);
        this.AttachItemsToPersistenceContext(tradePartnerItems, itemContext);

        if (!await TryAddItemsOfTradingPartnerAsync(trader).ConfigureAwait(false) || !await TryAddItemsOfTradingPartnerAsync(tradingPartner).ConfigureAwait(false))
        {
            // TODO: Maybe notify which player has the full inventory.
            await this.SendMessageAsync(trader, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            return TradeResult.FailedByFullInventory;
        }

        var itemsCommitted = false;
        try
        {
            this.DetachItemsFromPersistenceContext(traderItems, trader.PersistenceContext);
            this.DetachItemsFromPersistenceContext(tradePartnerItems, trader.TradingPartner!.PersistenceContext);
            if (itemContext is IAtomicTradeContext atomicTradeContext
                && trader.Inventory?.ItemStorage is { } traderStorage
                && tradingPartner.Inventory?.ItemStorage is { } partnerStorage
                && traderStorage.GetId() != Guid.Empty
                && partnerStorage.GetId() != Guid.Empty
                && ((trader as Player)?.Account?.GetId() ?? (tradingPartner as Player)?.Account?.GetId()) is { } receiptAccountId
                && receiptAccountId != Guid.Empty)
            {
                var opId = operationId ?? Guid.NewGuid();
                var payload = $"{traderStorage.GetId():N}|{partnerStorage.GetId():N}|{traderMoney}|{partnerMoney}|{string.Join(",", traderItems.Concat(tradePartnerItems).Select(i => i.GetId().ToString("N")).OrderBy(s => s))}";
                var request = new AssetOperationRequest(
                    opId,
                    receiptAccountId,
                    "trade",
                    AssetOperationCommitter.PayloadHash(payload),
                    "game",
                    null,
                    []);
                TradeMoneyDelta[] moneyChanges =
                [
                    new(traderStorage.GetId(), partnerMoney - traderMoney),
                    new(partnerStorage.GetId(), traderMoney - partnerMoney),
                ];
                var commitStatus = await atomicTradeContext.SaveTradeWithAssetOperationAsync(request, moneyChanges).ConfigureAwait(false);
                if (commitStatus is not (AssetCommitStatus.Committed or AssetCommitStatus.AlreadyCommitted))
                {
                    throw new InvalidOperationException($"Trade asset transaction was not committed ({commitStatus}).");
                }
            }
            else if (!await itemContext.SaveChangesAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException("Trade item changes were not saved.");
            }

            itemsCommitted = true;

            // Item ownership is now committed. Move the escrowed money before any
            // post-commit attachment or client notification can fail.
            trader.Money += trader.TradingPartner.TradingMoney;
            trader.TradingPartner.Money += trader.TradingMoney;

            // Attaching a transferred item's entire graph would walk the receiving
            // inventory and can collide with items that context already tracks.
            this.AttachTransferredItemsToPersistenceContext(traderItems, trader.TradingPartner.PersistenceContext);
            this.AttachTransferredItemsToPersistenceContext(tradePartnerItems, trader.PersistenceContext);
            await trader.TradingPartner.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Checked)).ConfigureAwait(false);
            this.ResetTradeState(trader.TradingPartner);
            this.ResetTradeState(trader);
            this.CallPlugIn(traderItems, trader, tradingPartner);
            this.CallPlugIn(tradePartnerItems, tradingPartner, trader);
            PlugIns.AccountSafety.AccountSafetyPlugIn.Get(trader.GameContext)?.RecordTrade(trader, tradingPartner, traderItems, tradePartnerItems, traderMoney, partnerMoney);
            PlugIns.ActivityLog.ActivityLogPlugIn.Get(trader.GameContext)?.RecordTrade(trader, tradingPartner, traderItems, tradePartnerItems, traderMoney, partnerMoney);
            FinishedTrades.Add(1);
            return TradeResult.Success;
        }
        catch (Exception exception)
        {
            if (itemsCommitted)
            {
                // SaveChanges already committed item ownership. Cancelling the trade
                // here would replay both inventory backups over the committed state.
                // A remaining attachment failure needs operator investigation, but it
                // must never turn a persisted trade into a reported cancellation.
                (trader as Player)?.Logger.LogCritical(
                    exception,
                    "Trade was committed but post-commit completion failed. trader: {Trader}, partner: {Partner}.",
                    trader.Name,
                    tradingPartner.Name);
                this.ResetTradeState(tradingPartner);
                this.ResetTradeState(trader);
                FinishedTrades.Add(1);
                return TradeResult.Success;
            }

            await this.SendMessageAsync(trader, nameof(PlayerMessage.UnexpectedErrorDuringClosingTrade)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.UnexpectedErrorDuringClosingTrade)).ConfigureAwait(false);
            context.Allowed = false;
            partnerContext.Allowed = false;
            (trader as Player)?.Logger.LogError(exception, $"An unexpected error occured during closing the trade. trader: {trader.Name}, partner:{tradingPartner.Name}");
            return TradeResult.Cancelled;
        }
    }

    private async ValueTask<bool> CheckItemsAreTradableAsync(ITrader trader, IEnumerable<Item> items)
    {
        // HNMO's account safety: a locked item can't be traded (it may have been locked after it went into the trade).
        if (trader is Player player && PlugIns.AccountSafety.AccountSafetyPlugIn.Get(player.GameContext) is { } safety)
        {
            foreach (var item in items)
            {
                if (await safety.RefuseLockedItemAsync(player, item).ConfigureAwait(false))
                {
                    return false;
                }

                if (await PlugIns.Escrow.EscrowContractsPlugIn.RefuseCooldownItemAsync(player, item).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
        else if (trader is Player escrowPlayer)
        {
            foreach (var item in items)
            {
                if (await PlugIns.Escrow.EscrowContractsPlugIn.RefuseCooldownItemAsync(escrowPlayer, item).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }

        var notTradable = items.FirstOrDefault(item => item.Definition is { IsTradable: false });
        if (notTradable is null)
        {
            return true;
        }

        (trader as Player)?.Logger.LogWarning(
            "Trader {0} offered {1}, which its item definition doesn't allow to trade. The trade is cancelled.",
            trader.Name,
            notTradable);
        await this.SendMessageAsync(trader, nameof(PlayerMessage.ItemCannotBeTraded)).ConfigureAwait(false);
        return false;
    }

    private void CallPlugIn(IEnumerable<Item> items, ITrader source, ITrader target)
    {
        var point = target.GameContext.PlugInManager.GetPlugInPoint<IItemTradedToOtherPlayerPlugIn>();
        if (point is null)
        {
            return;
        }

        foreach (var item in items)
        {
            point.ItemTraded(source, target, item);
        }
    }

    private void AttachItemsToPersistenceContext(IEnumerable<Item> items, IContext itemContext)
    {
        foreach (var item in items)
        {
            itemContext.Attach(item);
        }
    }

    private void AttachTransferredItemsToPersistenceContext(IEnumerable<Item> items, IContext itemContext)
    {
        foreach (var item in items)
        {
            itemContext.AttachWithoutParent(item);
        }
    }

    private void DetachItemsFromPersistenceContext(IEnumerable<Item> items, IContext itemContext)
    {
        foreach (var item in items)
        {
            itemContext.Detach(item);
        }
    }
}