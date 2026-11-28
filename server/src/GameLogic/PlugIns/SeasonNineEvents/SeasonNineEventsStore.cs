// <copyright file="SeasonNineEventsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Timetable;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> (<c>s9Events</c>) for Season 9 events,
/// schedule claim deduplication, and owed rewards.
/// </summary>
public sealed class SeasonNineEventsStore
{
    private const string SectionKey = "s9Events";
    private const int MaxHistoryEntries = 100;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _stateStore;

    private SeasonNineEventsStore(HnmoStateStore stateStore)
    {
        this._stateStore = stateStore;
    }

    /// <summary>Opens the persistent state store for <paramref name="path"/>.</summary>
    /// <param name="path">Target JSON path.</param>
    /// <returns>Store instance.</returns>
    public static SeasonNineEventsStore Open(string? path)
    {
        var store = (string.IsNullOrWhiteSpace(path) ? null : HnmoStateStore.Open(path))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-s9e-{Guid.NewGuid():N}.json"));
        return new(store);
    }

    /// <summary>Reads a thread-safe snapshot of state.</summary>
    /// <returns>Current state.</returns>
    public SeasonNineEventsState Snapshot()
    {
        lock (this._stateStore.SyncRoot)
        {
            return this._stateStore.ReadSection<SeasonNineEventsState>(SectionKey, JsonOptions) ?? new SeasonNineEventsState();
        }
    }

    /// <summary>Claims <paramref name="slotKey"/> for <paramref name="eventKey"/> so the same minute cannot fire twice.</summary>
    /// <param name="eventKey">Event identifier.</param>
    /// <param name="slotKey">Schedule slot key (<c>yyyy-MM-dd HH:mm</c>).</param>
    /// <returns><see langword="true"/> if newly claimed.</returns>
    public bool TryClaimScheduleSlot(string eventKey, string slotKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotKey);
        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<SeasonNineEventsState>(SectionKey, JsonOptions) ?? new SeasonNineEventsState();
            if (state.LastSlotByEvent.TryGetValue(eventKey, out var existing)
                && string.Equals(existing, slotKey, StringComparison.Ordinal))
            {
                return false;
            }

            state.LastSlotByEvent[eventKey] = slotKey;
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
            return true;
        }
    }

    /// <summary>Queues an owed reward to be delivered when inventory space allows or on next login.</summary>
    /// <param name="reward">Reward payload.</param>
    public void QueueOwedReward(S9EventOwedReward reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<SeasonNineEventsState>(SectionKey, JsonOptions) ?? new SeasonNineEventsState();
            state.OwedRewards.Add(reward);
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }

    /// <summary>Gets queued owed rewards matching <paramref name="loginName"/> and optionally <paramref name="characterName"/>.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="characterName">Optional character name.</param>
    /// <returns>Matching rewards.</returns>
    public List<S9EventOwedReward> GetOwedRewardsFor(string loginName, string? characterName)
    {
        var state = this.Snapshot();
        return state.OwedRewards
            .Where(r => string.Equals(r.LoginName, loginName, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(r.CharacterName)
                    || string.Equals(r.CharacterName, characterName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Removes a delivered owed reward by id.</summary>
    /// <param name="rewardId">Reward identifier.</param>
    public void RemoveOwedReward(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            return;
        }

        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<SeasonNineEventsState>(SectionKey, JsonOptions) ?? new SeasonNineEventsState();
            state.OwedRewards.RemoveAll(r => string.Equals(r.RewardId, rewardId, StringComparison.Ordinal));
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }

    /// <summary>Appends a Season 9 event history record with Sydney timestamp.</summary>
    /// <param name="entry">History record.</param>
    public void RecordHistory(S9EventHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.CompletedAtSydney))
        {
            var sydneyTz = TimetableEngine.ResolveTimeZone("Australia/Sydney");
            var utc = DateTime.SpecifyKind(entry.CompletedAtUtc == default ? DateTime.UtcNow : entry.CompletedAtUtc, DateTimeKind.Utc);
            entry.CompletedAtSydney = TimeZoneInfo.ConvertTimeFromUtc(utc, sydneyTz)
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<SeasonNineEventsState>(SectionKey, JsonOptions) ?? new SeasonNineEventsState();
            state.History.Insert(0, entry);
            if (state.History.Count > MaxHistoryEntries)
            {
                state.History.RemoveRange(MaxHistoryEntries, state.History.Count - MaxHistoryEntries);
            }

            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }

    /// <summary>Checks and stamps Uruk Mountain guard spawn cooldown.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="cooldownSeconds">Cooldown duration in seconds.</param>
    /// <returns><see langword="true"/> if cooldown elapsed and was stamped.</returns>
    public bool TryClaimUrukGuardCooldown(DateTime utcNow, int cooldownSeconds)
    {
        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<SeasonNineEventsState>(SectionKey, JsonOptions) ?? new SeasonNineEventsState();
            var minDelay = TimeSpan.FromSeconds(Math.Max(10, cooldownSeconds));
            if (state.LastUrukGuardSpawnUtc is { } last && utcNow - last < minDelay)
            {
                return false;
            }

            state.LastUrukGuardSpawnUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
            return true;
        }
    }
}
