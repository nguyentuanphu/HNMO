// <copyright file="WebsiteTestBankConfirmation.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;

internal sealed class WebsiteTestBankConfirmation(
    WebsiteTestTopUpStore store,
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    CashShopCoinService coins)
{
    private readonly SemaphoreSlim _confirmation = new(1, 1);

    public async Task<bool> ConfirmAsync(Guid id, string note, int amountVnd, CancellationToken cancellationToken = default)
    {
        await this._confirmation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!store.PublicSettings.BankSimulationEnabled || store.Find(id) is not { Provider: "bank" } order
                || order.Reference != note || order.Package.PriceVnd != amountVnd)
            {
                return false;
            }

            return await this.CreditAsync(order, $"website-test-bank:{id:N}", "test-bank", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this._confirmation.Release();
        }
    }

    public async Task<bool> SettleStripeAsync(Guid id, string intent, CancellationToken cancellationToken)
    {
        await this._confirmation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!store.BindStripePayment(id, intent) || store.Find(id) is not { } order)
            {
                return false;
            }

            return store.StripeFlags().ContainsKey(intent)
                || await this.CreditAsync(order, "website-test-stripe:" + intent, "test-stripe", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this._confirmation.Release();
        }
    }

    public async Task FlagStripeAsync(string intent, string reason, CancellationToken cancellationToken)
    {
        await this._confirmation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            store.FlagStripePayment(intent, reason);
        }
        finally
        {
            this._confirmation.Release();
        }
    }

    private async Task<bool> CreditAsync(TestTopUpOrder order, string reference, string channel, CancellationToken cancellationToken)
    {
        if (order.CreditedAt.HasValue)
        {
            return true;
        }

        var game = await configurations.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var players = persistence.CreateNewPlayerContext(game);
        if (await players.GetByIdAsync<Account>(order.AccountId, cancellationToken).ConfigureAwait(false) is not { IsBot: false } account
            || account.State is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return false;
        }

        var (status, grant) = await coins.GrantAsync(account.LoginName, order.Package.CoinType, order.Package.Coins,
            "Test website top-up", reference, channel, cancellationToken).ConfigureAwait(false);
        if (status is not (CashShopCoinGrantStatus.Created or CashShopCoinGrantStatus.AlreadyGranted)
            || grant?.Amount != order.Package.Coins || grant.CoinType != order.Package.CoinType)
        {
            return false;
        }

        var activity = await PlugInSettings.ReadStoredAsync<ActivityLogConfiguration>(persistence, typeof(ActivityLogPlugIn), cancellationToken).ConfigureAwait(false);
        ActivityLogStore.Open(store.StatePath).Append(new ActivityLogEntry
        {
            Id = $"tp-{order.Id:N}",
            Kind = ActivityKind.Administration,
            Account = account.LoginName,
            Details = JsonSerializer.Serialize(new { operation = channel, order = order.Id, order.Package.PriceVnd, order.Package.Coins, order.Package.CoinType }),
        }, activity.Settings?.RetentionDays ?? 30);
        store.MarkCredited(order.Id);
        return true;
    }
}
