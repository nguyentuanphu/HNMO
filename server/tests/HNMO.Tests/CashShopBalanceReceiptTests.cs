// <copyright file="CashShopBalanceReceiptTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;

/// <summary>Checks persisted ownership, signed deltas, rollback cleanup and protection against reapplying receipts.</summary>
[TestFixture]
public class CashShopBalanceReceiptTests
{
    /// <summary>Both debit and credit receipts persist under only their owner and are never pending payments.</summary>
    /// <param name="amount">The already-applied delta.</param>
    /// <returns>The test task.</returns>
    [TestCase(-25)]
    [TestCase(25)]
    public async Task ReceiptsNeverApplyAnotherPaymentAsync(int amount)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        try
        {
            player.Account!.WCoinC = 100 + amount;
            var receipt = CashShopBalanceReceipt.Record(player.PersistenceContext, player.Account, CashShopCoinType.WCoinC, amount, CashShopBalanceReason.ExtraVault)!;
            await player.PersistenceContext.SaveChangesAsync().ConfigureAwait(false);
            var history = await player.PersistenceContext.GetCashShopCoinHistoryAsync(player.Account.GetId(), 0, 20).ConfigureAwait(false);
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].Amount, Is.EqualTo(amount));
            Assert.That(history[0].AppliedAt, Is.EqualTo(history[0].CreatedAt));
            Assert.That(CashShopBalanceReceipt.Read(receipt.Reason)!.Action, Is.EqualTo(nameof(CashShopBalanceReason.ExtraVault)));
            await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
            Assert.That(player.Account.WCoinC, Is.EqualTo(100 + amount));
            Assert.That(await player.PersistenceContext.GetPendingCashShopCoinGrantsAsync(player.Account.GetId()).ConfigureAwait(false), Is.Empty);
        }
        finally
        {
            await player.DisposeAsync().ConfigureAwait(false);
            await ((GameContext)player.GameContext).DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Removing a failed payment's receipt prevents a later save from recording a nonexistent debit.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RolledBackReceiptsDoNotSurviveALaterSaveAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var configuration = setup.CreateNew<GameConfiguration>();
        using var context = provider.CreateNewPlayerContext(configuration);
        var account = context.CreateNew<Account>();
        account.WCoinC = 100;
        var receipt = CashShopBalanceReceipt.Record(context, account, CashShopCoinType.WCoinC, -20, CashShopBalanceReason.MarketPurchase);
        await CashShopBalanceReceipt.RemoveAsync(context, receipt).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await context.GetCashShopCoinHistoryAsync(account.GetId(), 0, 20).ConfigureAwait(false), Is.Empty);
        Assert.That(account.WCoinC, Is.EqualTo(100));
        Assert.That(CashShopBalanceReceipt.Record(context, account, CashShopCoinType.WCoinC, 0, CashShopBalanceReason.PlayTime), Is.Null);
    }

    /// <summary>One account's credits and another's debits remain separate retained histories.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task KeepsAccountHistoriesSeparateAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var configuration = setup.CreateNew<GameConfiguration>();
        using var context = provider.CreateNewPlayerContext(configuration);
        var buyer = context.CreateNew<Account>();
        var seller = context.CreateNew<Account>();
        CashShopBalanceReceipt.Record(context, buyer, CashShopCoinType.GoblinPoints, -30, CashShopBalanceReason.PlayerStorePurchase, "Seller");
        CashShopBalanceReceipt.Record(context, seller, CashShopCoinType.GoblinPoints, 30, CashShopBalanceReason.PlayerStoreSale, "Buyer");
        await context.SaveChangesAsync().ConfigureAwait(false);
        var bought = await context.GetCashShopCoinHistoryAsync(buyer.GetId(), 0, 20).ConfigureAwait(false);
        var sold = await context.GetCashShopCoinHistoryAsync(seller.GetId(), 0, 20).ConfigureAwait(false);
        Assert.That(bought.Single().Amount, Is.EqualTo(-30));
        Assert.That(sold.Single().Amount, Is.EqualTo(30));
        Assert.That(CashShopBalanceReceipt.Read(bought.Single().Reason)!.OtherCharacter, Is.EqualTo("Seller"));
        Assert.That(bought.Single().Reference, Is.Not.EqualTo(sold.Single().Reference));
    }

    /// <summary>Malformed and unrecognized metadata is never treated as a known balance action.</summary>
    /// <param name="reason">The legacy or malformed text.</param>
    [TestCase(null)]
    [TestCase("Host adjustment")]
    [TestCase("{")]
    [TestCase("{\"Kind\":\"account-balance-v1\",\"Action\":\"Unknown\"}")]
    [TestCase("{\"Kind\":\"account-balance-v1\",\"Action\":\"0\"}")]
    public void LeavesOrdinaryOrUnrecognizedReasonsAlone(string? reason)
        => Assert.That(CashShopBalanceReceipt.Read(reason), Is.Null);
}
