// <copyright file="VipRules.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Globalization;

/// <summary>Pure subscription rules and shared-state bookkeeping, without a wallet or gameplay adapter.</summary>
/// <remarks>
/// A quote is not a purchase. The future wallet adapter must serialize payment and fulfillment, journal
/// recovery and verify a durable payment receipt before recording it here. These rules expose no player API.
/// </remarks>
/// <param name="configuration">The current host configuration.</param>
/// <param name="store">The shared VIP state section.</param>
public sealed class VipRules(VipConfiguration configuration, VipStateStore store)
{
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    /// <summary>Quotes a purchase without debiting coins, changing state or applying benefits.</summary>
    /// <param name="login">The authenticated account name.</param>
    /// <param name="levelId">The requested configured level.</param>
    /// <param name="days">The offered duration.</param>
    /// <param name="coins">The current balance of the offer's currency.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>A rule refusal or a quote for the wallet adapter to review.</returns>
    public VipQuote Quote(string login, int levelId, int days, long coins, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (!configuration.Enabled)
        {
            return new(VipRuleResult.Disabled);
        }

        if (!ValidLogin(login) || !ValidConfiguration(configuration))
        {
            return new(VipRuleResult.Invalid);
        }

        var level = configuration.Levels.SingleOrDefault(value => value.Id == levelId);
        if (level is null)
        {
            return new(VipRuleResult.UnknownLevel);
        }

        var offer = level.Offers.SingleOrDefault(value => value.Days == days);
        if (offer is null)
        {
            return new(VipRuleResult.UnknownDuration);
        }

        lock (store.SyncRoot)
        {
            return this.QuoteLocked(Canonical(login), levelId, offer, coins, nowUtc);
        }
    }

    /// <summary>Records a trusted caller's already-confirmed payment; this method never charges a wallet.</summary>
    /// <param name="plan">The unchanged quote.</param>
    /// <param name="paymentId">The durable, globally unique receipt verified by the wallet adapter.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The current membership; replaying a matching receipt never extends it again.</returns>
    public VipMembership? RecordConfirmedPayment(VipPurchasePlan plan, Guid paymentId, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        ArgumentNullException.ThrowIfNull(plan);
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("A confirmed payment identifier is required.", nameof(paymentId));
        }

        lock (store.SyncRoot)
        {
            if (store.Data.Payments.TryGetValue(paymentId, out var recorded))
            {
                if (recorded != plan)
                {
                    throw new InvalidOperationException("This payment already belongs to another VIP quote.");
                }

                store.Save();
                return this.ActiveLocked(plan.LoginName, nowUtc);
            }

            this.ValidateConfirmedPlan(plan, nowUtc);
            var from = store.Data.Memberships.TryGetValue(plan.LoginName, out var previous) && previous.UntilUtc > plan.QuotedAtUtc
                ? previous.FromUtc : plan.QuotedAtUtc;
            var membership = new VipMembership(plan.LoginName, plan.LevelId, from, plan.UntilUtc);
            store.Data.Memberships[plan.LoginName] = membership;
            store.Data.Payments[paymentId] = plan;
            store.Data.Audit.Add(new(nowUtc, plan.LoginName, "purchase", "wallet", paymentId.ToString("N")));
            store.Save();
            return membership;
        }
    }

    /// <summary>Plans and records an administrative grant; caller authorization and gameplay application are separate.</summary>
    /// <param name="login">The verified account.</param>
    /// <param name="levelId">The configured level.</param>
    /// <param name="untilUtc">The explicit exclusive expiry.</param>
    /// <param name="actor">The verified administrator.</param>
    /// <param name="reason">The nonempty operational reason.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The saved subscription record.</returns>
    public VipMembership Grant(string login, int levelId, DateTime untilUtc, string actor, string reason, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        RequireUtc(untilUtc);
        ValidateActor(actor, reason);
        if (!configuration.Enabled || !ValidLogin(login) || !ValidConfiguration(configuration)
            || configuration.Levels.All(level => level.Id != levelId) || untilUtc <= nowUtc)
        {
            throw new ArgumentException("The VIP grant is invalid or disabled.");
        }

        lock (store.SyncRoot)
        {
            var name = Canonical(login);
            var membership = new VipMembership(name, levelId, nowUtc, untilUtc);
            store.Data.Memberships[name] = membership;
            store.Data.Audit.Add(new(nowUtc, name, "grant", actor.Trim(), reason.Trim()));
            store.Save();
            return membership;
        }
    }

    /// <summary>Removes the stored subscription without deleting payment replay protection or daily-claim history.</summary>
    /// <param name="login">The verified account.</param>
    /// <param name="actor">The verified administrator.</param>
    /// <param name="reason">The nonempty operational reason.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>Whether a stored subscription was removed.</returns>
    public bool Remove(string login, string actor, string reason, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        ValidateActor(actor, reason);
        if (!ValidLogin(login))
        {
            throw new ArgumentException("The account name is invalid.", nameof(login));
        }

        lock (store.SyncRoot)
        {
            var name = Canonical(login);
            if (!store.Data.Memberships.Remove(name))
            {
                return false;
            }

            store.Data.Audit.Add(new(nowUtc, name, "remove", actor.Trim(), reason.Trim()));
            store.Save();
            return true;
        }
    }

    /// <summary>Gets the active rule membership; this does not apply an attribute or refresh a player's state.</summary>
    /// <param name="login">The account name.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The active membership, or null at/after expiry or when disabled.</returns>
    public VipMembership? Active(string login, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        lock (store.SyncRoot)
        {
            return ValidLogin(login) ? this.ActiveLocked(Canonical(login), nowUtc) : null;
        }
    }

    /// <summary>Gets configured benefit values for an active subscription without applying them to gameplay.</summary>
    /// <param name="login">The account name.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The active benefit values, or null when inactive or disabled.</returns>
    public VipBenefits? BenefitsAt(string login, DateTime nowUtc)
    {
        lock (store.SyncRoot)
        {
            if (this.Active(login, nowUtc) is not { } membership)
            {
                return null;
            }

            var level = configuration.Levels.Single(value => value.Id == membership.LevelId);
            return new(level.ExperiencePercent, level.ZenPercent, level.DailyCoins, level.DailyCoinType);
        }
    }

    /// <summary>Checks daily bonus eligibility using Sydney midnight; no coins are granted by this method.</summary>
    /// <param name="login">The account name.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>Whether a wallet adapter may offer the current level's daily bonus.</returns>
    public bool CanClaimDaily(string login, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        lock (store.SyncRoot)
        {
            var name = ValidLogin(login) ? Canonical(login) : string.Empty;
            return this.ActiveLocked(name, nowUtc) is { } membership
                && configuration.Levels.Single(level => level.Id == membership.LevelId).DailyCoins > 0
                && string.CompareOrdinal(store.Data.DailyClaims.GetValueOrDefault(name), SydneyDay(nowUtc)) < 0;
        }
    }

    /// <summary>Records an already-granted daily bonus; the future wallet adapter must use an idempotent grant reference.</summary>
    /// <param name="login">The account name.</param>
    /// <param name="nowUtc">The UTC time of the confirmed grant.</param>
    /// <returns>Whether the Sydney day was recorded for the first time.</returns>
    public bool RecordGrantedDailyBonus(string login, DateTime nowUtc)
    {
        lock (store.SyncRoot)
        {
            if (!this.CanClaimDaily(login, nowUtc))
            {
                return false;
            }

            store.Data.DailyClaims[Canonical(login)] = SydneyDay(nowUtc);
            store.Save();
            return true;
        }
    }

    private static string Canonical(string login) => login.Trim().ToUpperInvariant();

    private static bool ValidLogin(string login) => !string.IsNullOrWhiteSpace(login) && login.Length is >= 3 and <= 10
        && login.All(character => character is > ' ' and <= '~');

    private static void RequireUtc(DateTime time)
    {
        if (time.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("VIP rule timestamps must be UTC.", nameof(time));
        }
    }

    private static string SydneyDay(DateTime nowUtc) => TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Sydney).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void ValidateActor(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 100 || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new ArgumentException("A bounded administrator identity and reason are required.");
        }
    }

    private static bool ValidConfiguration(VipConfiguration settings) => settings.Levels is { Count: > 0 }
        && settings.Levels.Select(level => level.Id).Distinct().Count() == settings.Levels.Count
        && settings.Levels.All(level => level.Id > 0 && !string.IsNullOrWhiteSpace(level.NameEn) && !string.IsNullOrWhiteSpace(level.NameVi)
            && level.ExperiencePercent is >= 0 and <= 10000 && level.ZenPercent is >= 0 and <= 10000 && level.DailyCoins >= 0
            && Enum.IsDefined(level.DailyCoinType) && level.Offers is not null
            && level.Offers.Select(offer => offer.Days).Distinct().Count() == level.Offers.Count
            && level.Offers.All(offer => offer.Days is > 0 and <= 3650 && offer.Price >= 0 && Enum.IsDefined(offer.CoinType)));

    private VipMembership? ActiveLocked(string name, DateTime nowUtc) => configuration.Enabled && ValidConfiguration(configuration)
        && store.Data.Memberships.TryGetValue(name, out var member) && member.FromUtc <= nowUtc && member.UntilUtc > nowUtc
        && configuration.Levels.Any(level => level.Id == member.LevelId) ? member : null;

    private VipQuote QuoteLocked(string name, int levelId, VipOffer offer, long coins, DateTime nowUtc)
    {
        var previous = store.Data.Memberships.GetValueOrDefault(name);
        if (previous is { } active && active.UntilUtc > nowUtc && active.LevelId != levelId)
        {
            return new(VipRuleResult.DifferentActiveLevel);
        }

        if (coins < offer.Price)
        {
            return new(VipRuleResult.InsufficientCoins);
        }

        var from = previous is { } current && current.UntilUtc > nowUtc ? current.UntilUtc : nowUtc;
        if (from > DateTime.MaxValue.AddDays(-offer.Days))
        {
            return new(VipRuleResult.Invalid);
        }

        return new(VipRuleResult.Ok, new(name, levelId, offer.Days, offer.Price, offer.CoinType, nowUtc, previous?.UntilUtc, from.AddDays(offer.Days)));
    }

    private void ValidateConfirmedPlan(VipPurchasePlan plan, DateTime nowUtc)
    {
        RequireUtc(plan.QuotedAtUtc);
        RequireUtc(plan.UntilUtc);
        var quote = this.Quote(plan.LoginName, plan.LevelId, plan.Days, long.MaxValue, plan.QuotedAtUtc);
        if (quote.Plan != plan || plan.QuotedAtUtc > nowUtc || plan.UntilUtc <= nowUtc)
        {
            throw new InvalidOperationException("The confirmed VIP quote is stale or no longer matches the configured offer.");
        }
    }
}
