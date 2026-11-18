// <copyright file="S6EventsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Timetable;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> (<c>s6Events</c>) for Last Man Standing
/// history and pending item/Zen owed rewards.
/// </summary>
public sealed class S6EventsStore
{
    private const string SectionKey = "s6Events";
    private const int MaxHistoryEntries = 100;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly HnmoStateStore _stateStore;

    private S6EventsStore(HnmoStateStore stateStore)
    {
        this._stateStore = stateStore;
    }

    /// <summary>Opens the persistent state store for <paramref name="path"/>.</summary>
    /// <param name="path">Target save JSON path.</param>
    /// <returns>Store instance.</returns>
    public static S6EventsStore Open(string? path)
    {
        var store = (string.IsNullOrWhiteSpace(path) ? null : HnmoStateStore.Open(path))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-s6e-{Guid.NewGuid():N}.json"));
        return new(store);
    }

    /// <summary>Reads the current snapshot safely.</summary>
    /// <returns>Deep copy or default state.</returns>
    public S6EventsState Snapshot()
    {
        lock (this._gate)
        {
            return this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
        }
    }

    /// <summary>Queues an owed reward to be delivered to inventory on next claim/login.</summary>
    /// <param name="reward">Reward entry.</param>
    public void QueueOwedReward(S6EventsOwedReward reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        lock (this._gate)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
            state.OwedRewards.Add(reward);
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }

    /// <summary>Returns owed rewards belonging to <paramref name="loginName"/> and optionally <paramref name="characterName"/>.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="characterName">Optional character name.</param>
    /// <returns>Matching owed rewards.</returns>
    public List<S6EventsOwedReward> GetOwedRewardsFor(string loginName, string? characterName)
    {
        var state = this.Snapshot();
        return state.OwedRewards
            .Where(r => string.Equals(r.LoginName, loginName, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(r.CharacterName)
                    || string.Equals(r.CharacterName, characterName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Removes a delivered owed reward by id.</summary>
    /// <param name="rewardId">Reward id.</param>
    public void RemoveOwedReward(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            return;
        }

        lock (this._gate)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
            state.OwedRewards.RemoveAll(r => string.Equals(r.RewardId, rewardId, StringComparison.Ordinal));
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }

    /// <summary>Marks a scheduled LMS slot key as started so the same minute cannot double-fire.</summary>
    /// <param name="slotKey">Schedule slot key (<c>yyyy-MM-dd HH:mm</c>).</param>
    /// <returns><see langword="true"/> if newly claimed; <see langword="false"/> if already fired.</returns>
    public bool TryClaimScheduleSlot(string slotKey)
    {
        lock (this._gate)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
            if (string.Equals(state.LastTriggeredSlotKey, slotKey, StringComparison.Ordinal))
            {
                return false;
            }

            state.LastTriggeredSlotKey = slotKey;
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
            return true;
        }
    }

    /// <summary>Appends a completed Last Man Standing winner record with Sydney timestamp.</summary>
    /// <param name="entry">Winner summary.</param>
    public void RecordLmsWin(S6EventsLmsWinEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.CompletedAtSydney))
        {
            var sydney = TimetableEngine.ResolveTimeZone("Australia/Sydney");
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entry.CompletedAtUtc, DateTimeKind.Utc), sydney);
            entry.CompletedAtSydney = local.ToString("yyyy-MM-dd HH:mm:ss 'Sydney'", CultureInfo.InvariantCulture);
        }

        lock (this._gate)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
            state.LastLmsCompletedUtc = entry.CompletedAtUtc;
            state.LmsHistory.Insert(0, entry);
            if (state.LmsHistory.Count > MaxHistoryEntries)
            {
                state.LmsHistory = state.LmsHistory.Take(MaxHistoryEntries).ToList();
            }

            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }
}
