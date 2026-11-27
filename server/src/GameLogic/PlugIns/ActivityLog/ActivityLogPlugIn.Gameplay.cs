// <copyright file="ActivityLogPlugIn.Gameplay.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

using HNMO.GameLogic.CastleSiege;
using HNMO.GameLogic.PlugIns.Arena;
using HNMO.GameLogic.Views.NPC;

public sealed partial class ActivityLogPlugIn
{
    private const int MaximumItemsPerAction = 64;

    internal bool IsEnabled => this.EffectiveConfig().Enabled;

    internal bool ShouldRecordFloorItem(Item item)
    {
        var cfg = this.EffectiveConfig();
        return cfg.Enabled && (!cfg.OnlyValuableDropsAndPickups || IsValuableItem(item, cfg.MinValuableDropLevel));
    }

    internal static ActivityItemState[] CaptureItems(IEnumerable<Item>? items)
        => (items ?? []).Take(MaximumItemsPerAction).Select(ActivityItemState.Capture).OfType<ActivityItemState>().ToArray();

    internal void RecordLoot(Player player, Item item, string source)
    {
        if (this.ShouldRecordFloorItem(item))
        {
            this.RecordItemStates(ActivityKind.Loot, player, source, after: CaptureItems([item]));
        }
    }

    internal void RecordGrant(Player player, Item item, string source)
    {
        if (this.IsEnabled)
        {
            this.RecordItemStates(ActivityKind.Grant, player, source, after: CaptureItems([item]));
        }
    }

    internal void RecordPartialPickup(Player player, ActivityItemState before, Item target, double quantity)
        => this.RecordItemStates(ActivityKind.Pickup, player, "PartialStack", [before], CaptureItems([target]), quantity);

    internal void RecordCraft(Player player, CraftingResult result, string recipe, ActivityItemState[] before, IEnumerable<Item>? finished)
    {
        if (!this.IsEnabled)
        {
            return;
        }

        var after = CaptureItems(finished);
        this.RecordItemStates(ActivityKind.ChaosMix, player, $"Recipe={recipe};Result={result}", before, after);
        foreach (var changed in after)
        {
            if (before.FirstOrDefault(item => item.Serial == changed.Serial) is { } original && HasUpgradeChange(original, changed))
            {
                this.RecordItemStates(ActivityKind.Upgrade, player, $"Recipe={recipe};Result={result}", [original], [changed]);
            }
        }
    }

    internal void RecordConsumption(Player player, ActivityItemState consumedBefore, Item? consumedAfter, ActivityItemState? targetBefore, Item? targetAfter)
    {
        if (!this.IsEnabled)
        {
            return;
        }

        var remaining = ActivityItemState.Capture(consumedAfter);
        if (remaining is null || remaining.Quantity != consumedBefore.Quantity || remaining.Durability != consumedBefore.Durability)
        {
            this.RecordItemStates(ActivityKind.Consume, player, "Inventory", [consumedBefore], remaining is null ? [] : [remaining]);
        }

        if (targetBefore is { } before)
        {
            var after = ActivityItemState.Capture(targetAfter);
            if (after is null || HasUpgradeChange(before, after))
            {
                this.RecordItemStates(ActivityKind.Upgrade, player, $"Material={consumedBefore.Description}", [before], after is null ? [] : [after]);
            }
        }
    }

    internal void RecordPvpKill(Player killer, Player defeated)
    {
        if (!this.IsEnabled || ReferenceEquals(killer, defeated))
        {
            return;
        }

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.PvpKill,
            Account = LoginOf(killer),
            Character = killer.Name,
            PartnerAccount = LoginOf(defeated),
            PartnerCharacter = defeated.Name,
            PvpMode = ResolvePvpMode(killer, defeated),
            ServerId = (defeated.GameContext as IGameServerContext)?.Id,
            MapNumber = defeated.CurrentMap?.Definition.Number,
        });
    }

    private static bool HasUpgradeChange(ActivityItemState before, ActivityItemState after)
        => before.Level != after.Level || before.Sockets != after.Sockets || before.HasSkill != after.HasSkill || before.Options != after.Options;

    private static ActivityPvpMode ResolvePvpMode(Player killer, Player defeated)
    {
        if (ArenaPlugIn.Get(defeated.GameContext) is { } arena && arena.IsInBattle(killer) && arena.IsInBattle(defeated))
        {
            return ActivityPvpMode.Arena;
        }

        if (defeated.DuelRoom is { State: DuelState.DuelStarted } duel && killer.DuelRoom == duel)
        {
            return ActivityPvpMode.Duel;
        }

        if (CastleSiegeContextResolver.GetContext(defeated) is { CurrentState: CastleSiegeState.Start } siege
            && siege.Configuration.CastleSiegeMapDefinition is { } map
            && defeated.CurrentMap?.Definition.Number == map.Number)
        {
            return ActivityPvpMode.Siege;
        }

        if (defeated.CurrentMiniGame?.AllowPlayerKilling == true)
        {
            return ActivityPvpMode.MiniGame;
        }

        return killer.GuildWarContext?.Score is { } score && score == defeated.GuildWarContext?.Score
            ? ActivityPvpMode.GuildWar
            : ActivityPvpMode.Field;
    }

    private void RecordItemStates(ActivityKind kind, Player player, string details, ActivityItemState[]? before = null, ActivityItemState[]? after = null, double? pickupQuantity = null)
    {
        var states = (after ?? []).Concat(before ?? []).DistinctBy(item => item.Serial).ToArray();
        this.AppendEntry(new ActivityLogEntry
        {
            Kind = kind,
            Account = LoginOf(player),
            Character = player.Name,
            ItemSummary = string.Join(", ", states.Select(item => item.Description)),
            ItemSerials = string.Join(",", states.Select(item => item.Serial.ToString("N"))),
            BeforeItems = before,
            AfterItems = after,
            PickupQuantity = pickupQuantity,
            ServerId = (player.GameContext as IGameServerContext)?.Id,
            MapNumber = player.CurrentMap?.Definition.Number,
            Details = details,
        });
    }
}
