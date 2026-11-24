// <copyright file="WebsiteTestTopUpStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.Web.AdminPanel.Operations;

internal sealed record TestCoinPackage(string Id, string EnglishName, string VietnameseName, int PriceVnd, int Coins, CashShopCoinType CoinType);

internal sealed record TestTopUpSettings(bool BankSimulationEnabled, int OrdersPerPlayerPerDay, TestCoinPackage[] Packages)
{
    public static TestTopUpSettings Empty { get; } = new(false, 3, []);
}

internal sealed record TestTopUpOrder(Guid Id, Guid AccountId, TestCoinPackage Package, DateTimeOffset CreatedAt, DateTimeOffset? CreditedAt = null,
    string Provider = "bank", string? StripeSessionId = null, string? PaymentIntent = null)
{
    public string Reference => $"TEST-ORDER-{this.Id:N}";
}

internal sealed class WebsiteTestTopUpStore(HnmoStateStore state, AdminAuditSettings? audit = null, TimeProvider? time = null)
{
    private static readonly TimeZoneInfo Sydney = HnmoStateStore.ResolveTimeZone(null);
    private const string SettingsSection = "websiteTestTopUp";
    private const string OrdersSection = "websiteTestTopUpOrders";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public bool CanRun => audit?.IsDemo == true;

    public string StatePath => state.FilePath;

    public TestTopUpSettings Settings => state.ReadSection<TestTopUpSettings>(SettingsSection, JsonOptions) ?? TestTopUpSettings.Empty;

    public TestTopUpSettings PublicSettings
    {
        get
        {
            var settings = this.CanRun ? this.Settings : TestTopUpSettings.Empty;
            return settings.BankSimulationEnabled ? settings : TestTopUpSettings.Empty;
        }
    }

    public void SaveSettings(TestTopUpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.OrdersPerPlayerPerDay is < 1 or > 10
            || settings.Packages is not { Length: <= 24 }
            || settings.Packages.Any(p => p is null
                || p.Id is not { Length: > 0 and <= 32 } || p.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')
                || string.IsNullOrWhiteSpace(p.EnglishName) || p.EnglishName.Length > 100
                || string.IsNullOrWhiteSpace(p.VietnameseName) || p.VietnameseName.Length > 100
                || p.PriceVnd is < 1 or > 99_999_999 || p.Coins is < 1 or > 1_000_000 || !Enum.IsDefined(p.CoinType))
            || settings.Packages.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != settings.Packages.Length)
        {
            throw new ArgumentException("Invalid test top-up settings.", nameof(settings));
        }

        lock (this._gate)
        {
            state.WriteSection(SettingsSection, settings, JsonOptions);
        }
    }

    public TestTopUpOrder? TryCreate(Guid account, Guid operation, string packageId, string provider = "bank", bool stripeEnabled = false)
    {
        if (account == Guid.Empty || operation == Guid.Empty)
        {
            return null;
        }

        lock (this._gate)
        {
            var settings = this.Settings;
            if (!this.CanRun || !(provider == "bank" && settings.BankSimulationEnabled || provider == "stripe" && stripeEnabled))
            {
                return null;
            }

            var orders = this.ReadOrders();
            if (orders.TryGetValue(operation, out var existing))
            {
                return existing.AccountId == account && existing.Package.Id == packageId && existing.Provider == provider ? existing : null;
            }

            var package = settings.Packages.FirstOrDefault(p => p.Id == packageId);
            var now = this._time.GetUtcNow();
            var today = SydneyDay(now);
            if (package is null || orders.Values.Count(o => o.AccountId == account && SydneyDay(o.CreatedAt) == today) >= settings.OrdersPerPlayerPerDay)
            {
                return null;
            }

            var order = new TestTopUpOrder(operation, account, package, now, Provider: provider);
            orders.Add(operation, order);
            state.WriteSection(OrdersSection, orders, JsonOptions);
            return order;
        }
    }

    public TestTopUpOrder? Find(Guid id) => this.CanRun ? this.ReadOrders().GetValueOrDefault(id) : null;

    public TestTopUpOrder[] Recent() => this.CanRun
        ? this.ReadOrders().Values.OrderByDescending(o => o.CreatedAt).Take(50).ToArray()
        : [];

    public bool BindStripeSession(Guid id, string session)
    {
        lock (this._gate)
        {
            var orders = this.ReadOrders();
            if (!orders.TryGetValue(id, out var order) || order.Provider != "stripe")
            {
                return false;
            }

            if (order.StripeSessionId is not null)
            {
                return order.StripeSessionId == session;
            }

            orders[id] = order with { StripeSessionId = session };
            state.WriteSection(OrdersSection, orders, JsonOptions);
            return true;
        }
    }

    public bool BindStripePayment(Guid id, string intent)
    {
        lock (this._gate)
        {
            var orders = this.ReadOrders();
            if (!orders.TryGetValue(id, out var order) || order.Provider != "stripe" || order.StripeSessionId is null
                || orders.Values.Any(other => other.Id != id && other.PaymentIntent == intent))
            {
                return false;
            }

            if (order.PaymentIntent is not null)
            {
                return order.PaymentIntent == intent;
            }

            orders[id] = order with { PaymentIntent = intent };
            state.WriteSection(OrdersSection, orders, JsonOptions);
            return true;
        }
    }

    public IReadOnlyDictionary<string, string> StripeFlags()
        => state.ReadSection<Dictionary<string, string>>("websiteStripeTestFlags", JsonOptions) ?? [];

    public void FlagStripePayment(string intent, string reason)
    {
        lock (this._gate)
        {
            var flags = new Dictionary<string, string>(this.StripeFlags(), StringComparer.Ordinal);
            if (flags.TryAdd(intent, reason))
            {
                state.WriteSection("websiteStripeTestFlags", flags, JsonOptions);
            }
        }
    }

    public void MarkCredited(Guid id)
    {
        lock (this._gate)
        {
            var orders = this.ReadOrders();
            if (orders.TryGetValue(id, out var order) && order.CreditedAt is null)
            {
                orders[id] = order with { CreditedAt = this._time.GetUtcNow() };
                state.WriteSection(OrdersSection, orders, JsonOptions);
            }
        }
    }

    private static DateOnly SydneyDay(DateTimeOffset stamp) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(stamp, Sydney).DateTime);

    private Dictionary<Guid, TestTopUpOrder> ReadOrders() => state.ReadSection<Dictionary<Guid, TestTopUpOrder>>(OrdersSection, JsonOptions) ?? [];
}
