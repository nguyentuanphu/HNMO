// <copyright file="MarriageEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.Collections.Concurrent;

/// <summary>Outcome codes for marriage proposals, ceremonies, title changes, and divorces.</summary>
public enum MarriageActionResult
{
    /// <summary>Action succeeded.</summary>
    Success,

    /// <summary>Marriage feature is disabled.</summary>
    Disabled,

    /// <summary>Invalid target (empty or self).</summary>
    InvalidTarget,

    /// <summary>Proposer or target is already married.</summary>
    AlreadyMarried,

    /// <summary>Caller is not currently married.</summary>
    NotMarried,

    /// <summary>One or both partners are below <see cref="MarriageConfiguration.MinimumLevel"/>.</summary>
    LevelTooLow,

    /// <summary>One or both partners are not standing at the ceremony spot.</summary>
    NotAtCeremonyLocation,

    /// <summary>Caller does not have enough zen for ceremony or divorce.</summary>
    NotEnoughZen,

    /// <summary>One of the characters is still on remarriage cooldown.</summary>
    CooldownActive,

    /// <summary>No matching pending proposal exists.</summary>
    NoPendingProposal,

    /// <summary>Custom titles are disabled or invalid.</summary>
    InvalidTitle,
}

/// <summary>
/// Pure rule engine governing marriage proposals, ceremonies, couple titles, and divorce rules.
/// </summary>
public sealed class MarriageEngine
{
    private readonly ConcurrentDictionary<string, PendingMarriageProposal> _pendingByTarget = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="MarriageEngine"/> class.
    /// </summary>
    /// <param name="configuration">The marriage configuration.</param>
    /// <param name="store">The persistence store.</param>
    /// <param name="enabled">Whether the feature is active.</param>
    public MarriageEngine(MarriageConfiguration configuration, MarriageStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets the configuration.</summary>
    public MarriageConfiguration Configuration { get; }

    /// <summary>Gets the backing store.</summary>
    public MarriageStore Store { get; }

    /// <summary>Gets a value indicating whether marriage is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Checks whether a coordinate lies within the ceremony sanctuary.</summary>
    /// <param name="mapNumber">Current map number.</param>
    /// <param name="x">Current X coordinate.</param>
    /// <param name="y">Current Y coordinate.</param>
    /// <returns><c>true</c> if inside the ceremony spot or location check is disabled.</returns>
    public bool IsAtCeremonySpot(short mapNumber, byte x, byte y)
    {
        if (!this.Configuration.RequireCeremonyLocation)
        {
            return true;
        }

        if (mapNumber != this.Configuration.CeremonyMapNumber)
        {
            return false;
        }

        var dx = x - this.Configuration.CeremonyX;
        var dy = y - this.Configuration.CeremonyY;
        var r = Math.Max(1, this.Configuration.CeremonyRadius);
        return (dx * dx) + (dy * dy) <= r * r;
    }

    /// <summary>Creates or refreshes a pending marriage proposal.</summary>
    /// <param name="proposerName">Proposing character name.</param>
    /// <param name="proposerLogin">Proposer account login.</param>
    /// <param name="proposerLevel">Proposer character level.</param>
    /// <param name="targetName">Target character name.</param>
    /// <param name="targetLevel">Target character level.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Action outcome.</returns>
    public MarriageActionResult Propose(
        string proposerName,
        string proposerLogin,
        int proposerLevel,
        string targetName,
        int targetLevel,
        DateTime utcNow)
    {
        if (!this.Enabled)
        {
            return MarriageActionResult.Disabled;
        }

        if (string.IsNullOrWhiteSpace(targetName) || string.Equals(proposerName, targetName, StringComparison.OrdinalIgnoreCase))
        {
            return MarriageActionResult.InvalidTarget;
        }

        if (proposerLevel < this.Configuration.MinimumLevel || targetLevel < this.Configuration.MinimumLevel)
        {
            return MarriageActionResult.LevelTooLow;
        }

        var eligibility = this.Store.Read(data =>
        {
            if (FindCoupleLocked(data, proposerName) is not null || FindCoupleLocked(data, targetName) is not null)
            {
                return MarriageActionResult.AlreadyMarried;
            }

            if ((data.RemarryCooldownUntilUtc.TryGetValue(proposerName, out var pUntil) && utcNow < pUntil)
                || (data.RemarryCooldownUntilUtc.TryGetValue(targetName, out var tUntil) && utcNow < tUntil))
            {
                return MarriageActionResult.CooldownActive;
            }

            return MarriageActionResult.Success;
        });

        if (eligibility != MarriageActionResult.Success)
        {
            return eligibility;
        }

        this._pendingByTarget[targetName.Trim()] = new PendingMarriageProposal(
            proposerName.Trim(),
            proposerLogin,
            targetName.Trim(),
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).AddMinutes(2));
        return MarriageActionResult.Success;
    }

    /// <summary>Gets an active non-expired incoming proposal for a character.</summary>
    /// <param name="characterName">Target character name.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>The pending proposal, or <c>null</c>.</returns>
    public PendingMarriageProposal? GetIncomingProposal(string characterName, DateTime utcNow)
    {
        if (!this._pendingByTarget.TryGetValue(characterName, out var pending))
        {
            return null;
        }

        if (utcNow >= pending.ExpiresAtUtc)
        {
            this._pendingByTarget.TryRemove(characterName, out _);
            return null;
        }

        return pending;
    }

    /// <summary>Gets an active outgoing proposal sent by a character.</summary>
    /// <param name="proposerName">Proposer character name.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>The pending proposal, or <c>null</c>.</returns>
    public PendingMarriageProposal? GetOutgoingProposal(string proposerName, DateTime utcNow)
    {
        return this._pendingByTarget.Values
            .FirstOrDefault(p => string.Equals(p.ProposerName, proposerName, StringComparison.OrdinalIgnoreCase) && utcNow < p.ExpiresAtUtc);
    }

    /// <summary>Completes a ceremony and records the marriage if all ceremony rules pass.</summary>
    /// <param name="accepterName">Accepting character name.</param>
    /// <param name="accepterLogin">Accepting account login.</param>
    /// <param name="accepterLevel">Accepting character level.</param>
    /// <param name="proposerLevel">Proposing character level.</param>
    /// <param name="bothAtCeremonySpot">Whether both stand inside the ceremony spot.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="couple">Created couple entry if successful.</param>
    /// <returns>Outcome code.</returns>
    public MarriageActionResult AcceptCeremony(
        string accepterName,
        string accepterLogin,
        int accepterLevel,
        int proposerLevel,
        bool bothAtCeremonySpot,
        DateTime utcNow,
        out MarriageCoupleEntry? couple)
    {
        couple = null;
        if (!this.Enabled)
        {
            return MarriageActionResult.Disabled;
        }

        var pending = this.GetIncomingProposal(accepterName, utcNow);
        if (pending is null)
        {
            return MarriageActionResult.NoPendingProposal;
        }

        if (accepterLevel < this.Configuration.MinimumLevel || proposerLevel < this.Configuration.MinimumLevel)
        {
            return MarriageActionResult.LevelTooLow;
        }

        if (this.Configuration.RequireCeremonyLocation && !bothAtCeremonySpot)
        {
            return MarriageActionResult.NotAtCeremonyLocation;
        }

        var (outcome, created) = this.Store.Mutate(data =>
        {
            if (FindCoupleLocked(data, accepterName) is not null || FindCoupleLocked(data, pending.ProposerName) is not null)
            {
                return (false, (MarriageActionResult.AlreadyMarried, (MarriageCoupleEntry?)null));
            }

            var entry = new MarriageCoupleEntry
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                PartnerAName = pending.ProposerName,
                PartnerALogin = pending.ProposerLogin,
                PartnerBName = accepterName.Trim(),
                PartnerBLogin = accepterLogin,
                CustomTitle = string.Empty,
                MarriedAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            };
            data.Couples.Add(entry);
            return (true, (MarriageActionResult.Success, entry));
        });

        if (outcome == MarriageActionResult.Success)
        {
            this._pendingByTarget.TryRemove(accepterName, out _);
        }

        couple = created;
        return outcome;
    }

    /// <summary>Declines and removes a pending proposal for <paramref name="accepterName"/>.</summary>
    /// <param name="accepterName">Accepting character name.</param>
    /// <param name="proposerName">Outputs the proposer name if removed.</param>
    /// <returns><c>true</c> if a proposal existed.</returns>
    public bool DeclineProposal(string accepterName, out string? proposerName)
    {
        proposerName = null;
        if (!this._pendingByTarget.TryRemove(accepterName, out var removed))
        {
            return false;
        }

        proposerName = removed.ProposerName;
        return true;
    }

    /// <summary>Updates the shared custom couple title for a married character.</summary>
    /// <param name="characterName">Caller character name.</param>
    /// <param name="title">Requested title (empty resets to default template).</param>
    /// <returns>Outcome code.</returns>
    public MarriageActionResult SetCustomTitle(string characterName, string title)
    {
        if (!this.Enabled)
        {
            return MarriageActionResult.Disabled;
        }

        if (!this.Configuration.AllowCustomTitle)
        {
            return MarriageActionResult.InvalidTitle;
        }

        var cleaned = (title ?? string.Empty).Trim();
        if (cleaned.Length > 32 || cleaned.IndexOfAny(['<', '>', '\r', '\n']) >= 0)
        {
            return MarriageActionResult.InvalidTitle;
        }

        return this.Store.Mutate(data =>
        {
            var couple = FindCoupleLocked(data, characterName);
            if (couple is null)
            {
                return (false, MarriageActionResult.NotMarried);
            }

            couple.CustomTitle = cleaned;
            return (true, MarriageActionResult.Success);
        });
    }

    /// <summary>Dissolves a character's marriage and sets the remarriage cooldown for both ex-spouses.</summary>
    /// <param name="characterName">Caller (or admin target) character name.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="exSpouseName">Outputs the ex-spouse's name if successful.</param>
    /// <returns>Outcome code.</returns>
    public MarriageActionResult Divorce(string characterName, DateTime utcNow, out string? exSpouseName)
    {
        exSpouseName = null;
        var cooldown = TimeSpan.FromHours(Math.Max(0, this.Configuration.RemarryCooldownHours));
        var (result, spouse) = this.Store.Mutate(data =>
        {
            var couple = FindCoupleLocked(data, characterName);
            if (couple is null)
            {
                return (false, (MarriageActionResult.NotMarried, (string?)null));
            }

            var other = string.Equals(couple.PartnerAName, characterName, StringComparison.OrdinalIgnoreCase)
                ? couple.PartnerBName
                : couple.PartnerAName;
            data.Couples.Remove(couple);
            if (cooldown > TimeSpan.Zero)
            {
                var until = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) + cooldown;
                data.RemarryCooldownUntilUtc[couple.PartnerAName] = until;
                data.RemarryCooldownUntilUtc[couple.PartnerBName] = until;
            }

            return (true, (MarriageActionResult.Success, other));
        });

        exSpouseName = spouse;
        return result;
    }

    /// <summary>Gets the active couple for <paramref name="characterName"/>, if married.</summary>
    /// <param name="characterName">Character name.</param>
    /// <returns>Couple entry, or <c>null</c>.</returns>
    public MarriageCoupleEntry? GetCoupleOf(string characterName)
    {
        return this.Store.Read(data => FindCoupleLocked(data, characterName) is { } c ? Copy(c) : null);
    }

    /// <summary>Gets the spouse name of <paramref name="characterName"/>, or <c>null</c> if unmarried.</summary>
    /// <param name="characterName">Character name.</param>
    /// <returns>Spouse character name, or <c>null</c>.</returns>
    public string? GetSpouseName(string characterName)
    {
        var couple = this.GetCoupleOf(characterName);
        if (couple is null)
        {
            return null;
        }

        return string.Equals(couple.PartnerAName, characterName, StringComparison.OrdinalIgnoreCase)
            ? couple.PartnerBName
            : couple.PartnerAName;
    }

    /// <summary>Lists all active couples sorted newest first.</summary>
    /// <returns>List of couples.</returns>
    public IReadOnlyList<MarriageCoupleEntry> AllCouples()
    {
        return this.Store.Read(data =>
            data.Couples
                .OrderByDescending(c => c.MarriedAtUtc)
                .Select(Copy)
                .ToList());
    }

    private static MarriageCoupleEntry? FindCoupleLocked(MarriageStoreData data, string characterName) =>
        data.Couples.FirstOrDefault(c =>
            string.Equals(c.PartnerAName, characterName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.PartnerBName, characterName, StringComparison.OrdinalIgnoreCase));

    private static MarriageCoupleEntry Copy(MarriageCoupleEntry c) => new()
    {
        Id = c.Id,
        PartnerAName = c.PartnerAName,
        PartnerALogin = c.PartnerALogin,
        PartnerBName = c.PartnerBName,
        PartnerBLogin = c.PartnerBLogin,
        CustomTitle = c.CustomTitle,
        MarriedAtUtc = DateTime.SpecifyKind(c.MarriedAtUtc, DateTimeKind.Utc),
    };
}
