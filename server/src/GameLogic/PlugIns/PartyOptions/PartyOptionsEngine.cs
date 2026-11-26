// <copyright file="PartyOptionsEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.Collections.Concurrent;

/// <summary>Validation result for password/spot party joins.</summary>
public enum PartyJoinCheckResult
{
    /// <summary>Permitted.</summary>
    Ok,

    /// <summary>Feature disabled.</summary>
    Disabled,

    /// <summary>No matching party found.</summary>
    NotFound,

    /// <summary>Player is already in a party.</summary>
    AlreadyInParty,

    /// <summary>Wrong password.</summary>
    WrongPassword,

    /// <summary>Target party is full.</summary>
    PartyFull,

    /// <summary>Player level outside advertised min/max range.</summary>
    LevelOutOfRange,
}

/// <summary>
/// Pure rule engine managing party password registration/lookup and auto-party hunting spot listings.
/// </summary>
public sealed class PartyOptionsEngine
{
    private readonly ConcurrentDictionary<string, PartyAdvertisement> _ads = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyOptionsEngine"/> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="store">Store.</param>
    /// <param name="enabled">Whether active.</param>
    public PartyOptionsEngine(PartyOptionsConfiguration configuration, PartyOptionsStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets configuration.</summary>
    public PartyOptionsConfiguration Configuration { get; }

    /// <summary>Gets store.</summary>
    public PartyOptionsStore Store { get; }

    /// <summary>Gets a value indicating whether enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Sets or updates an advertisement for <paramref name="ad"/>.</summary>
    /// <param name="ad">Advertisement.</param>
    public void SetAdvertisement(PartyAdvertisement ad)
    {
        if (string.IsNullOrWhiteSpace(ad.LeaderName))
        {
            return;
        }

        if (!string.IsNullOrEmpty(ad.Password) && ad.Password.Length > this.Configuration.MaxPasswordLength)
        {
            ad.Password = ad.Password[..this.Configuration.MaxPasswordLength];
        }

        this._ads[ad.LeaderName.Trim()] = ad;
        this.FlushPublicSnapshot();
    }

    /// <summary>Removes any advertisement for <paramref name="leaderName"/>.</summary>
    /// <param name="leaderName">Leader name.</param>
    /// <returns><c>true</c> if removed.</returns>
    public bool RemoveAdvertisement(string leaderName)
    {
        if (string.IsNullOrWhiteSpace(leaderName))
        {
            return false;
        }

        var removed = this._ads.TryRemove(leaderName.Trim(), out _);
        if (removed)
        {
            this.FlushPublicSnapshot();
        }

        return removed;
    }

    /// <summary>Returns current advertisements ordered newest first.</summary>
    /// <returns>Ads.</returns>
    public IReadOnlyList<PartyAdvertisement> ListAdvertisements() =>
        this._ads.Values.OrderByDescending(a => a.UpdatedAtUtc).ToList();

    /// <summary>Gets advertisement led by <paramref name="leaderName"/>, if any.</summary>
    /// <param name="leaderName">Leader name.</param>
    /// <returns>Advertisement or <c>null</c>.</returns>
    public PartyAdvertisement? GetAdvertisement(string? leaderName) =>
        !string.IsNullOrWhiteSpace(leaderName) && this._ads.TryGetValue(leaderName.Trim(), out var ad) ? ad : null;

    /// <summary>Finds an open advertisement matching <paramref name="password"/>.</summary>
    /// <param name="password">Candidate password.</param>
    /// <param name="playerLevel">Joining player's level.</param>
    /// <param name="hasParty">Whether joining player already belongs to a party.</param>
    /// <param name="match">Matched advertisement if <c>Ok</c>.</param>
    /// <returns>Validation result.</returns>
    public PartyJoinCheckResult FindByPassword(string password, int playerLevel, bool hasParty, out PartyAdvertisement? match)
    {
        match = null;
        if (!this.Enabled || !this.Configuration.AllowPasswordJoin)
        {
            return PartyJoinCheckResult.Disabled;
        }

        if (hasParty)
        {
            return PartyJoinCheckResult.AlreadyInParty;
        }

        var trimmed = password?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return PartyJoinCheckResult.NotFound;
        }

        var found = this._ads.Values.FirstOrDefault(a => !string.IsNullOrEmpty(a.Password) && string.Equals(a.Password, trimmed, StringComparison.Ordinal));
        if (found is null)
        {
            return PartyJoinCheckResult.NotFound;
        }

        var eligibility = CheckEligibility(found, playerLevel, trimmed);
        if (eligibility == PartyJoinCheckResult.Ok)
        {
            match = found;
        }

        return eligibility;
    }

    /// <summary>Validates joining a spot party led by <paramref name="leaderName"/>.</summary>
    /// <param name="leaderName">Target leader name.</param>
    /// <param name="password">Password provided (if passworded).</param>
    /// <param name="playerLevel">Joining player's level.</param>
    /// <param name="hasParty">Whether joiner already has a party.</param>
    /// <param name="match">Matched advertisement if <c>Ok</c>.</param>
    /// <returns>Check result.</returns>
    public PartyJoinCheckResult PlanSpotJoin(string leaderName, string? password, int playerLevel, bool hasParty, out PartyAdvertisement? match)
    {
        match = null;
        var viaPassword = !string.IsNullOrEmpty(password?.Trim());
        if (!this.Enabled || (viaPassword ? !this.Configuration.AllowPasswordJoin : !this.Configuration.AllowAutoSpotParty))
        {
            return PartyJoinCheckResult.Disabled;
        }

        if (hasParty)
        {
            return PartyJoinCheckResult.AlreadyInParty;
        }

        var ad = this.GetAdvertisement(leaderName);
        if (ad is null)
        {
            return PartyJoinCheckResult.NotFound;
        }

        var eligibility = CheckEligibility(ad, playerLevel, password?.Trim() ?? string.Empty);
        if (eligibility == PartyJoinCheckResult.Ok)
        {
            match = ad;
        }

        return eligibility;
    }

    private static PartyJoinCheckResult CheckEligibility(PartyAdvertisement ad, int playerLevel, string suppliedPassword)
    {
        if (ad.MemberCount >= ad.MaxSize)
        {
            return PartyJoinCheckResult.PartyFull;
        }

        if (playerLevel < ad.MinLevel || (ad.MaxLevel > 0 && playerLevel > ad.MaxLevel))
        {
            return PartyJoinCheckResult.LevelOutOfRange;
        }

        if (!string.IsNullOrEmpty(ad.Password) && !string.Equals(ad.Password, suppliedPassword, StringComparison.Ordinal))
        {
            return PartyJoinCheckResult.WrongPassword;
        }

        return PartyJoinCheckResult.Ok;
    }

    private void FlushPublicSnapshot()
    {
        var list = this._ads.Values
            .OrderByDescending(a => a.UpdatedAtUtc)
            .Select(a => new PartyAdSnapshot
            {
                LeaderName = a.LeaderName,
                MapName = a.MapName,
                SpotName = a.SpotName,
                MinLevel = a.MinLevel,
                MaxLevel = a.MaxLevel,
                HasPassword = !string.IsNullOrEmpty(a.Password),
                MemberCount = a.MemberCount,
                MaxSize = a.MaxSize,
                UpdatedAtUtc = a.UpdatedAtUtc,
            })
            .ToList();
        this.Store.SaveAds(list);
    }
}
