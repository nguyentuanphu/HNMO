// <copyright file="PentagramActions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;

/// <summary>
/// Wearing and taking off the Season 9 pentagram (slot <see cref="InventoryConstants.PentagramSlot"/> of the
/// inventory's item storage, after the personal store), putting errtels into its slots and taking them out, and the
/// errtel mixes. Each action is saved at once and undone in memory when the save fails, so the running game and the
/// database agree; a full bag changes nothing.
/// </summary>
public static class PentagramActions
{
    /// <summary>Gets or sets the random number source: a value in [0, max) (tests replace it).</summary>
    internal static Func<int, int> NextRandom { get; set; } = static max => Rand.NextInt(0, max);

    /// <summary>Wears the pentagram in a bag slot.</summary>
    /// <param name="player">The player.</param>
    /// <param name="bagSlot">The bag slot of the pentagram.</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> EquipAsync(Player player, byte bagSlot)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (Configuration(player) is null)
            {
                return PentagramActionResult.Disabled;
            }

            if (player.Inventory?.GetItem(bagSlot) is not { } item || bagSlot < InventoryConstants.EquippableSlotsCount || !Pentagram.IsPentagram(item.Definition))
            {
                return PentagramActionResult.InvalidItem;
            }

            if (Pentagram.Worn(player) is not null)
            {
                return PentagramActionResult.Occupied;
            }

            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
            item.ItemSlot = InventoryConstants.PentagramSlot;
            player.SelectedCharacter!.Inventory!.Items.Add(item);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                player.SelectedCharacter.Inventory.Items.Remove(item);
                await player.Inventory.AddItemAsync(bagSlot, item).ConfigureAwait(false);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(bagSlot)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    /// <summary>Takes off the worn pentagram into the first free bag slot.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> UnequipAsync(Player player)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (Pentagram.Worn(player) is not { } pentagram)
            {
                return PentagramActionResult.Empty;
            }

            if (player.Inventory?.CheckInvSpace(pentagram) is not { } freeSlot)
            {
                return PentagramActionResult.BagFull;
            }

            player.SelectedCharacter!.Inventory!.Items.Remove(pentagram);
            if (!await player.Inventory.AddItemAsync(freeSlot, pentagram).ConfigureAwait(false))
            {
                pentagram.ItemSlot = InventoryConstants.PentagramSlot;
                player.SelectedCharacter.Inventory.Items.Add(pentagram);
                return PentagramActionResult.BagFull;
            }

            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                await player.Inventory.RemoveItemAsync(pentagram).ConfigureAwait(false);
                pentagram.ItemSlot = InventoryConstants.PentagramSlot;
                player.SelectedCharacter.Inventory.Items.Add(pentagram);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(pentagram)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    /// <summary>Puts the errtel of a bag slot into an errtel slot of the worn pentagram.</summary>
    /// <param name="player">The player.</param>
    /// <param name="errtelBagSlot">The errtel's bag slot.</param>
    /// <param name="slotIndex">The pentagram's errtel slot (0..4).</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> AttachErrtelAsync(Player player, byte errtelBagSlot, int slotIndex)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (Configuration(player) is not { } configuration)
            {
                return PentagramActionResult.Disabled;
            }

            if (Pentagram.Worn(player) is not { } pentagram)
            {
                return PentagramActionResult.Empty;
            }

            if (player.Inventory?.GetItem(errtelBagSlot) is not { } errtel || errtelBagSlot < InventoryConstants.EquippableSlotsCount
                || !Pentagram.IsErrtel(errtel.Definition) || slotIndex < 0 || slotIndex >= Pentagram.UsableSlots(configuration, pentagram.Definition!))
            {
                return PentagramActionResult.InvalidItem;
            }

            var slots = Pentagram.ReadSlots(pentagram);
            if (slots[slotIndex] is not null)
            {
                return PentagramActionResult.Occupied;
            }

            var info = Pentagram.ReadErrtel(errtel);
            if (info.Element != (PentagramElement)pentagram.PentagramElement)
            {
                return PentagramActionResult.ElementMismatch;
            }

            var before = pentagram.PentagramErrtels;
            slots[slotIndex] = info;
            Pentagram.WriteSlots(pentagram, slots);
            await player.Inventory.RemoveItemAsync(errtel).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                // The deleted errtel stays deleted; a copy goes back where it was, so the next save writes the bag as
                // it was before.
                pentagram.PentagramErrtels = before;
                var copy = player.PersistenceContext.CreateNew<Item>();
                copy.AssignValues(errtel);
                await player.Inventory.AddItemAsync(errtelBagSlot, copy).ConfigureAwait(false);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(errtelBagSlot)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    /// <summary>
    /// Takes the errtel of an errtel slot of the worn pentagram out, for <see cref="SeasonNineSystemsConfiguration.ErrtelExtractZen"/>.
    /// It comes out intact with the extraction rate of its rank and level, else it breaks. All or nothing: the Zen, the
    /// slot and the errtel in the bag are saved together; a full bag or too little Zen changes nothing.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="slotIndex">The pentagram's errtel slot (0..4).</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> RemoveErrtelAsync(Player player, int slotIndex)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (Configuration(player) is not { } configuration)
            {
                return PentagramActionResult.Disabled;
            }

            if (Pentagram.Worn(player) is not { } pentagram || slotIndex < 0 || slotIndex >= Pentagram.SlotCount)
            {
                return PentagramActionResult.Empty;
            }

            var slots = Pentagram.ReadSlots(pentagram);
            if (slots[slotIndex] is not { } info)
            {
                return PentagramActionResult.Empty;
            }

            if (!AtMixPlace(player, configuration))
            {
                return PentagramActionResult.WrongPlace;
            }

            var zen = Math.Max(0, configuration.ErrtelExtractZen);
            if (player.Money < zen)
            {
                return PentagramActionResult.NotEnoughZen;
            }

            var definition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == 12 && d.Number == info.Number);
            if (definition is null)
            {
                return PentagramActionResult.InvalidItem;
            }

            var errtel = player.PersistenceContext.CreateNew<Item>();
            errtel.Definition = definition;
            errtel.Durability = 1;
            Pentagram.WriteErrtel(errtel, info);
            if (player.Inventory?.CheckInvSpace(errtel) is not { } freeSlot)
            {
                await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
                return PentagramActionResult.BagFull;
            }

            var rank = (byte)Math.Clamp(info.Options.Count, 1, 5);
            var level = info.Options.Count == 0 ? (byte)0 : info.Options.Max(o => o.Level);
            var intact = NextRandom(10000) < PentagramData.ExtractionRate(configuration, rank, level);
            if (intact && !await player.Inventory.AddItemAsync(freeSlot, errtel).ConfigureAwait(false))
            {
                await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
                return PentagramActionResult.BagFull;
            }

            if (!intact)
            {
                await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
            }

            var before = pentagram.PentagramErrtels;
            slots[slotIndex] = null;
            Pentagram.WriteSlots(pentagram, slots);
            player.TryRemoveMoney(zen);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                pentagram.PentagramErrtels = before;
                player.TryAddMoney(zen);
                if (intact)
                {
                    await player.Inventory.RemoveItemAsync(errtel).ConfigureAwait(false);
                    await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
                }

                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            if (!intact)
            {
                return PentagramActionResult.Broken;
            }

            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(errtel)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    /// <summary>
    /// Raises the levels of the errtel of a bag slot by one (to at most 10), for
    /// <see cref="SeasonNineSystemsConfiguration.ErrtelLevelUpZenPerLevel"/> times the new level. It succeeds with the
    /// level-up rate; a failure keeps the errtel as it was and takes the Zen.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="bagSlot">The errtel's bag slot.</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> LevelUpErrtelAsync(Player player, byte bagSlot)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (Configuration(player) is not { } configuration)
            {
                return PentagramActionResult.Disabled;
            }

            if (player.Inventory?.GetItem(bagSlot) is not { } errtel || bagSlot < InventoryConstants.EquippableSlotsCount || !Pentagram.IsErrtel(errtel.Definition))
            {
                return PentagramActionResult.InvalidItem;
            }

            if (!AtMixPlace(player, configuration))
            {
                return PentagramActionResult.WrongPlace;
            }

            var info = Pentagram.ReadErrtel(errtel);
            var level = info.Options.Count == 0 ? (byte)0 : info.Options.Min(o => o.Level);
            if (info.Options.Count == 0 || level >= 10)
            {
                return PentagramActionResult.MaximumReached;
            }

            var zen = Math.Max(0, configuration.ErrtelLevelUpZenPerLevel) * (level + 1);
            if (player.Money < zen)
            {
                return PentagramActionResult.NotEnoughZen;
            }

            var rate = Math.Max(configuration.ErrtelLevelUpMinimumRatePct, configuration.ErrtelLevelUpBaseRatePct - (configuration.ErrtelLevelUpRateStepPct * level))
                       + configuration.PentagramErrtelMixBonusPct;
            var success = NextRandom(100) < Math.Clamp(rate, 0, 100);
            var before = errtel.PentagramErrtels;
            if (success)
            {
                Pentagram.WriteErrtel(errtel, info with { Options = info.Options.Select(o => (o.Index, (byte)Math.Min(10, o.Level + 1))).ToList() });
            }

            player.TryRemoveMoney(zen);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                errtel.PentagramErrtels = before;
                player.TryAddMoney(zen);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            return success ? PentagramActionResult.Ok : PentagramActionResult.Failed;
        });

    /// <summary>
    /// Raises the rank of the errtel of a bag slot by one, with a second errtel of the same kind, element and rank, for
    /// <see cref="SeasonNineSystemsConfiguration.ErrtelRankUpZen"/>. On success the first errtel gets one option of the
    /// next rank (chosen by the options' weights) at level 0; the second errtel is used up either way.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="bagSlot">The bag slot of the errtel to raise.</param>
    /// <param name="secondBagSlot">The bag slot of the second errtel.</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> RankUpErrtelAsync(Player player, byte bagSlot, byte secondBagSlot)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (Configuration(player) is not { } configuration)
            {
                return PentagramActionResult.Disabled;
            }

            if (bagSlot == secondBagSlot
                || player.Inventory?.GetItem(bagSlot) is not { } errtel || !Pentagram.IsErrtel(errtel.Definition)
                || player.Inventory.GetItem(secondBagSlot) is not { } second || second.Definition != errtel.Definition
                || bagSlot < InventoryConstants.EquippableSlotsCount || secondBagSlot < InventoryConstants.EquippableSlotsCount)
            {
                return PentagramActionResult.InvalidItem;
            }

            if (!AtMixPlace(player, configuration))
            {
                return PentagramActionResult.WrongPlace;
            }

            var info = Pentagram.ReadErrtel(errtel);
            var secondInfo = Pentagram.ReadErrtel(second);
            if (secondInfo.Element != info.Element || secondInfo.Options.Count != info.Options.Count)
            {
                return PentagramActionResult.ElementMismatch;
            }

            var nextRank = (byte)(info.Options.Count + 1);
            var candidates = PentagramData.Options(configuration).Where(o => o.ErrtelNumber == info.Number && o.Rank == nextRank && o.Rate > 0).ToList();
            if (nextRank > 5 || candidates.Count == 0)
            {
                return PentagramActionResult.MaximumReached;
            }

            var zen = Math.Max(0, configuration.ErrtelRankUpZen);
            if (player.Money < zen)
            {
                return PentagramActionResult.NotEnoughZen;
            }

            var success = NextRandom(100) < Math.Clamp(configuration.ErrtelRankUpRatePct + configuration.PentagramErrtelMixBonusPct, 0, 100);
            var before = errtel.PentagramErrtels;
            if (success)
            {
                var pick = NextRandom(candidates.Sum(c => c.Rate));
                var chosen = candidates[^1];
                foreach (var candidate in candidates)
                {
                    if (pick < candidate.Rate)
                    {
                        chosen = candidate;
                        break;
                    }

                    pick -= candidate.Rate;
                }

                Pentagram.WriteErrtel(errtel, info with { Options = [.. info.Options, (chosen.Index, (byte)0)] });
            }

            var secondSlot = second.ItemSlot;
            await player.Inventory.RemoveItemAsync(second).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(second).ConfigureAwait(false);
            player.TryRemoveMoney(zen);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                errtel.PentagramErrtels = before;
                player.TryAddMoney(zen);
                var copy = player.PersistenceContext.CreateNew<Item>();
                copy.AssignValues(second);
                await player.Inventory.AddItemAsync(secondSlot, copy).ConfigureAwait(false);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(secondSlot)).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            return success ? PentagramActionResult.Ok : PentagramActionResult.Failed;
        });

    /// <summary>Gets the pentagram settings when the system is on in this realm.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The configuration, or <c>null</c> when the system is off or this isn't a Season 9 realm.</returns>
    internal static SeasonNineSystemsConfiguration? Configuration(Player player)
        => SeasonNineSystemsPlugIn.Get(player.GameContext) is { } plugIn && plugIn.IsActive() && plugIn.Configuration is { PentagramEnabled: true } configuration
            ? configuration
            : null;

    // The mixes are done at the mix NPC when one is set, else in a safe zone.
    private static bool AtMixPlace(Player player, SeasonNineSystemsConfiguration configuration)
    {
        if (configuration.PentagramMixNpcNumber > 0)
        {
            return player.OpenedNpc?.Definition.Number == configuration.PentagramMixNpcNumber;
        }

        return player.CurrentMap is not { } map || map.Terrain.SafezoneMap[player.Position.X, player.Position.Y];
    }

    private static async ValueTask<bool> SaveAsync(Player player)
    {
        try
        {
            return await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            player.Logger.LogWarning(ex, "Pentagram: the change of {Player} couldn't be saved.", player.Name);
            return false;
        }
    }
}
