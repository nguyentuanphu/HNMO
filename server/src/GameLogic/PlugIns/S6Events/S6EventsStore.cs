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
        lock (this._stateStore.SyncRoot)
        {
            return this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
        }
    }

    /// <summary>Queues an owed reward to be delivered to inventory on next claim/login.</summary>
    /// <param name="reward">Reward entry.</param>
    public void QueueOwedReward(S6EventsOwedReward reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        lock (this._stateStore.SyncRoot)
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

        lock (this._stateStore.SyncRoot)
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
        lock (this._stateStore.SyncRoot)
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

        lock (this._stateStore.SyncRoot)
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

    /// <summary>Marks a scheduled Loren Deep slot key as started so the same minute cannot double-fire.</summary>
    /// <param name="slotKey">Schedule slot key (<c>yyyy-MM-dd HH:mm</c>).</param>
    /// <returns><see langword="true"/> if newly claimed; <see langword="false"/> if already fired.</returns>
    public bool TryClaimLorenScheduleSlot(string slotKey)
    {
        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
            if (string.Equals(state.LastLorenTriggeredSlotKey, slotKey, StringComparison.Ordinal))
            {
                return false;
            }

            state.LastLorenTriggeredSlotKey = slotKey;
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
            return true;
        }
    }

    /// <summary>Checks cooldown and records an XMas gift claim timestamp for <paramref name="characterName"/>.</summary>
    /// <param name="characterName">Character name.</param>
    /// <param name="cooldownMinutes">Required cooldown in minutes.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <param name="nextAvailableUtc">Outputs earliest next UTC claim time when cooling down.</param>
    /// <returns><see langword="true"/> if claim is allowed and recorded.</returns>
    public bool TryClaimXMasGift(string characterName, int cooldownMinutes, DateTime utcNow, out DateTime nextAvailableUtc)
    {
        nextAvailableUtc = utcNow;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return false;
        }

        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
            if (cooldownMinutes > 0
                && state.LastXMasGiftUtcByCharacter.TryGetValue(characterName, out var prev)
                && prev.AddMinutes(cooldownMinutes) > utcNow)
            {
                nextAvailableUtc = prev.AddMinutes(cooldownMinutes);
                return false;
            }

            state.LastXMasGiftUtcByCharacter[characterName] = utcNow;
            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
            return true;
        }
    }

    /// <summary>Gets saved Mu Rummy game state for <paramref name="characterName"/>.</summary>
    /// <param name="characterName">Character name.</param>
    /// <returns>Game state, or <see langword="null"/> if none active.</returns>
    public MuRummyGameState? GetMuRummyGame(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions);
            return state?.MuRummyGames.TryGetValue(characterName, out var game) == true ? game : null;
        }
    }

    /// <summary>Saves or clears (<see langword="null"/>) Mu Rummy game state for <paramref name="characterName"/>.</summary>
    /// <param name="characterName">Character name.</param>
    /// <param name="game">Game state or <see langword="null"/> to remove.</param>
    public void SaveMuRummyGame(string characterName, MuRummyGameState? game)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return;
        }

        lock (this._stateStore.SyncRoot)
        {
            var state = this._stateStore.ReadSection<S6EventsState>(SectionKey, JsonOptions) ?? new S6EventsState();
            if (game is null)
            {
                state.MuRummyGames.Remove(characterName);
            }
            else
            {
                state.MuRummyGames[characterName] = game;
            }

            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }
}
