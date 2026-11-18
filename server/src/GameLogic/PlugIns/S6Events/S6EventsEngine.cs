// <copyright file="S6EventsEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Persistence;

/// <summary>
/// Pure helper logic for Season 6 scheduled LMS slots, stacked token consumption, weighted reward draws, and bag/owed-reward delivery.
/// </summary>
public static class S6EventsEngine
{
    /// <summary>Counts total stacked durability units matching <paramref name="group"/> and <paramref name="number"/> in <paramref name="inventory"/>.</summary>
    /// <param name="inventory">Inventory storage.</param>
    /// <param name="group">Target item group.</param>
    /// <param name="number">Target item number.</param>
    /// <returns>Total available units across all stacks.</returns>
    public static int CountStackedItems(IStorage? inventory, int group, short number)
    {
        if (inventory?.Items is null)
        {
            return 0;
        }

        return inventory.Items
            .Where(i => i.Definition is not null && i.Definition.Group == group && i.Definition.Number == number && i.Durability > 0)
            .Sum(i => Math.Max(0, (int)Math.Ceiling(i.Durability)));
    }

    /// <summary>Resolves the best matching <see cref="S6ExchangeTier"/> (explicit required count if passed, else highest affordable tier).</summary>
    /// <param name="tiers">Configured exchange tiers.</param>
    /// <param name="availableCount">Available stacked tokens.</param>
    /// <param name="requestedCount">Optional explicit tier cost (&lt;= 0 means auto-select highest affordable).</param>
    /// <returns>Matching affordable tier, or <c>null</c>.</returns>
    public static S6ExchangeTier? SelectAffordableTier(
        IReadOnlyCollection<S6ExchangeTier>? tiers,
        int availableCount,
        int requestedCount = 0)
    {
        if (tiers is null || tiers.Count == 0 || availableCount <= 0)
        {
            return null;
        }

        if (requestedCount > 0)
        {
            return tiers.FirstOrDefault(t => t.RequiredCount == requestedCount && availableCount >= t.RequiredCount && t.Rewards.Count > 0);
        }

        return tiers
            .Where(t => t.RequiredCount > 0 && availableCount >= t.RequiredCount && t.Rewards.Count > 0)
            .OrderByDescending(t => t.RequiredCount)
            .FirstOrDefault();
    }

    /// <summary>Picks a reward from <paramref name="rewards"/> proportionally by positive <see cref="S6ExchangeRewardEntry.Weight"/>.</summary>
    /// <param name="rewards">Candidate reward entries.</param>
    /// <param name="randomFraction">Uniform random value in [0, 1).</param>
    /// <returns>Selected reward or <c>null</c> when empty.</returns>
    public static S6ExchangeRewardEntry? PickWeightedReward(
        IReadOnlyList<S6ExchangeRewardEntry>? rewards,
        double randomFraction)
    {
        if (rewards is null || rewards.Count == 0)
        {
            return null;
        }

        var totalWeight = rewards.Sum(r => Math.Max(1, r.Weight));
        var clamped = Math.Clamp(randomFraction, 0.0, 0.999999999);
        var roll = clamped * totalWeight;
        var acc = 0.0;
        foreach (var entry in rewards)
        {
            acc += Math.Max(1, entry.Weight);
            if (roll < acc)
            {
                return entry;
            }
        }

        return rewards[^1];
    }

    /// <summary>Atomically verifies and consumes <paramref name="requiredCount"/> stacked units from <paramref name="player"/> inventory.</summary>
    /// <param name="player">Player whose inventory is modified.</param>
    /// <param name="group">Item group.</param>
    /// <param name="number">Item number.</param>
    /// <param name="requiredCount">Units to consume.</param>
    /// <returns><see langword="true"/> if enough units were present and consumed.</returns>
    public static async ValueTask<bool> TryConsumeStackedItemsAsync(
        Player player,
        int group,
        short number,
        int requiredCount)
    {
        if (player.Inventory is not { } inventory || requiredCount <= 0)
        {
            return false;
        }

        var available = CountStackedItems(inventory, group, number);
        if (available < requiredCount)
        {
            return false;
        }

        var matching = inventory.Items
            .Where(i => i.Definition is not null && i.Definition.Group == group && i.Definition.Number == number && i.Durability > 0)
            .OrderBy(i => i.Durability)
            .ToList();

        var remaining = requiredCount;
        foreach (var item in matching)
        {
            if (remaining <= 0)
            {
                break;
            }

            var stackUnits = Math.Max(0, (int)Math.Ceiling(item.Durability));
            if (stackUnits <= 0)
            {
                continue;
            }

            var take = Math.Min(stackUnits, remaining);
            item.Durability -= take;
            remaining -= take;
            if (item.Durability <= 0.0001)
            {
                var slot = item.ItemSlot;
                await inventory.RemoveItemAsync(item).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
            }
            else
            {
                await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, true)).ConfigureAwait(false);
            }
        }

        return remaining == 0;
    }

    /// <summary>Attempts atomic delivery of non-coin <paramref name="reward"/> (items + Zen) into <paramref name="player"/>'s bag.</summary>
    /// <param name="player">Recipient player.</param>
    /// <param name="reward">Reward specification.</param>
    /// <returns><see langword="true"/> if delivered immediately; <see langword="false"/> if bag/Zen limit prevented delivery.</returns>
    public static async ValueTask<bool> TryDeliverGoodsAsync(Player player, S6EventsOwedReward reward)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var added = new List<Item>();
        async ValueTask RollbackAsync()
        {
            foreach (var back in added)
            {
                await inventory.RemoveItemAsync(back).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(back).ConfigureAwait(false);
            }
        }

        if (reward.ItemGroup >= 0 && reward.ItemCount > 0)
        {
            var definition = player.GameContext.Configuration.Items.FirstOrDefault(
                d => d.Group == reward.ItemGroup && d.Number == reward.ItemNumber);
            if (definition is not null)
            {
                var level = Math.Min(reward.ItemLevel, definition.MaximumItemLevel);
                var count = Math.Clamp(reward.ItemCount, 1, 20);
                for (var i = 0; i < count; i++)
                {
                    var persisted = player.PersistenceContext.CreateNew<Item>();
                    persisted.Definition = definition;
                    persisted.Level = level;
                    persisted.Durability = persisted.IsStackable() ? 1 : persisted.GetMaximumDurabilityOfOnePiece();
                    if (inventory.CheckInvSpace(persisted) is not { } slot
                        || !await inventory.AddItemAsync(slot, persisted).ConfigureAwait(false))
                    {
                        player.PersistenceContext.Detach(persisted);
                        await RollbackAsync().ConfigureAwait(false);
                        return false;
                    }

                    added.Add(persisted);
                }
            }
        }

        if (reward.Zen > 0 && !player.TryAddMoney((int)Math.Min(int.MaxValue, reward.Zen)))
        {
            await RollbackAsync().ConfigureAwait(false);
            return false;
        }

        foreach (var item in added)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (reward.Zen > 0)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Checks whether any configured <c>HH:mm</c> daily slot matches the current minute in <paramref name="timeZoneId"/>.</summary>
    /// <param name="dailyTimes">Configured <c>HH:mm</c> slots.</param>
    /// <param name="timeZoneId">IANA/Windows timezone id.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Canonical slot key (<c>yyyy-MM-dd HH:mm</c>) if due now; otherwise <c>null</c>.</returns>
    public static string? ResolveDueScheduleSlot(
        IEnumerable<string>? dailyTimes,
        string? timeZoneId,
        DateTime utcNow)
    {
        if (dailyTimes is null)
        {
            return null;
        }

        var tz = TimetableEngine.ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        var hm = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        foreach (var raw in dailyTimes)
        {
            if (TimeSpan.TryParseExact(raw?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var ts)
                && string.Equals($"{ts.Hours:D2}:{ts.Minutes:D2}", hm, StringComparison.Ordinal))
            {
                return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
        }

        return null;
    }
}
