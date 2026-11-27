// <copyright file="S6EventsMonstersAndItemsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Ensures Season 6 Loren Deep assault monsters (300-303), Cursed Goblin (466),
/// Mining Area twinkle nodes (605-607), Pandora Pick (0-41), and Rummy card items (14-215..216) exist.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.S6EventsMonstersAndItemsUpdatePlugIn_Name), Description = nameof(PlugInResources.S6EventsMonstersAndItemsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A48C29F1-5B17-4D63-8F10-6E9B2D3A4C81")]
public class S6EventsMonstersAndItemsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => PlugInResources.S6EventsMonstersAndItemsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.S6EventsMonstersAndItemsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 11, 30, 0, DateTimeKind.Utc);

    /// <summary>Adds missing Season 6 event monsters and items.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        ApplyItems(context, gameConfiguration);
        ApplyMonsters(context, gameConfiguration);
    }

    /// <summary>Adds missing Mining and Rummy items idempotently.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void ApplyItems(IContext context, GameConfiguration gameConfiguration)
    {
        EnsureItem(context, gameConfiguration, 0, 41, LocalizedString.FromResource(() => ItemNames.PandoraPick), 2, 2, 255);
        EnsureItem(context, gameConfiguration, 14, 215, LocalizedString.FromResource(() => ItemNames.CardPiece), 1, 1, 24);
        EnsureItem(context, gameConfiguration, 14, 216, LocalizedString.FromResource(() => ItemNames.CardDeck), 1, 1, 1);
    }

    /// <summary>Adds missing Loren Deep, Cursed Goblin, and Mining Area monster/NPC definitions idempotently.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void ApplyMonsters(IContext context, GameConfiguration gameConfiguration)
    {
        var skill = gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.MonsterSkill);

        EnsureMonster(
            context,
            gameConfiguration,
            300,
            LocalizedString.FromResource(() => MonsterNames.HeroMutant),
            NpcObjectKind.Monster,
            moveRange: 20,
            attackRange: 1,
            viewRange: 10,
            moveDelayMs: 800,
            attackDelayMs: 1600,
            respawnSeconds: 10,
            attribute: 2,
            attackSkill: skill,
            stats:
            [
                (Stats.Level, 82f),
                (Stats.MaximumHealth, 20000f),
                (Stats.MinimumPhysBaseDmg, 500f),
                (Stats.MaximumPhysBaseDmg, 560f),
                (Stats.DefenseBase, 380f),
                (Stats.AttackRatePvm, 415f),
                (Stats.DefenseRatePvm, 170f),
            ]);

        EnsureMonster(
            context,
            gameConfiguration,
            301,
            LocalizedString.FromResource(() => MonsterNames.OmegaWing),
            NpcObjectKind.Monster,
            moveRange: 20,
            attackRange: 1,
            viewRange: 10,
            moveDelayMs: 800,
            attackDelayMs: 1600,
            respawnSeconds: 10,
            attribute: 2,
            attackSkill: skill,
            stats:
            [
                (Stats.Level, 102f),
                (Stats.MaximumHealth, 69000f),
                (Stats.MinimumPhysBaseDmg, 978f),
                (Stats.MaximumPhysBaseDmg, 1080f),
                (Stats.DefenseBase, 720f),
                (Stats.AttackRatePvm, 670f),
                (Stats.DefenseRatePvm, 290f),
            ]);

        EnsureMonster(
            context,
            gameConfiguration,
            302,
            LocalizedString.FromResource(() => MonsterNames.AxlHero),
            NpcObjectKind.Monster,
            moveRange: 20,
            attackRange: 1,
            viewRange: 10,
            moveDelayMs: 800,
            attackDelayMs: 1600,
            respawnSeconds: 10,
            attribute: 2,
            attackSkill: skill,
            stats:
            [
                (Stats.Level, 85f),
                (Stats.MaximumHealth, 23000f),
                (Stats.MinimumPhysBaseDmg, 510f),
                (Stats.MaximumPhysBaseDmg, 580f),
                (Stats.DefenseBase, 390f),
                (Stats.AttackRatePvm, 435f),
                (Stats.DefenseRatePvm, 175f),
            ]);

        EnsureMonster(
            context,
            gameConfiguration,
            303,
            LocalizedString.FromResource(() => MonsterNames.GigasGolem),
            NpcObjectKind.Monster,
            moveRange: 20,
            attackRange: 5,
            viewRange: 10,
            moveDelayMs: 1000,
            attackDelayMs: 2500,
            respawnSeconds: 10,
            attribute: 2,
            attackSkill: skill,
            stats:
            [
                (Stats.Level, 94f),
                (Stats.MaximumHealth, 50000f),
                (Stats.MinimumPhysBaseDmg, 750f),
                (Stats.MaximumPhysBaseDmg, 850f),
                (Stats.DefenseBase, 550f),
                (Stats.AttackRatePvm, 580f),
                (Stats.DefenseRatePvm, 240f),
            ]);

        var cursedGoblin = EnsureMonster(
            context,
            gameConfiguration,
            466,
            LocalizedString.FromResource(() => MonsterNames.CursedGoblin),
            NpcObjectKind.Monster,
            moveRange: 3,
            attackRange: 2,
            viewRange: 5,
            moveDelayMs: 400,
            attackDelayMs: 1800,
            respawnSeconds: 10,
            attribute: 2,
            attackSkill: skill,
            stats:
            [
                (Stats.Level, 70f),
                (Stats.MaximumHealth, 12000f),
                (Stats.MinimumPhysBaseDmg, 220f),
                (Stats.MaximumPhysBaseDmg, 260f),
                (Stats.DefenseBase, 190f),
                (Stats.AttackRatePvm, 440f),
                (Stats.DefenseRatePvm, 130f),
            ]);

        if (cursedGoblin is not null
            && cursedGoblin.DropItemGroups.Count == 0
            && gameConfiguration.Items.FirstOrDefault(i => i.Group == 13 && i.Number == 66) is { } invitation)
        {
            var dropGroup = context.CreateNew<DropItemGroup>();
            dropGroup.SetGuid(466, 66);
            dropGroup.Chance = 0.1;
            dropGroup.Monster = cursedGoblin;
            dropGroup.PossibleItems.Add(invitation);
            cursedGoblin.DropItemGroups.Add(dropGroup);
            gameConfiguration.DropItemGroups.Add(dropGroup);
        }

        EnsureMonster(context, gameConfiguration, 605, LocalizedString.FromResource(() => MonsterNames.MiningAreaSmall), NpcObjectKind.PassiveNpc, 0, 0, 0, 0, 0, 10, 1, null, []);
        EnsureMonster(context, gameConfiguration, 606, LocalizedString.FromResource(() => MonsterNames.MiningAreaMedium), NpcObjectKind.PassiveNpc, 0, 0, 0, 0, 0, 20, 1, null, []);
        EnsureMonster(context, gameConfiguration, 607, LocalizedString.FromResource(() => MonsterNames.MiningAreaLarge), NpcObjectKind.PassiveNpc, 0, 0, 0, 0, 0, 30, 1, null, []);
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static void EnsureItem(
        IContext context,
        GameConfiguration gameConfiguration,
        byte group,
        short number,
        LocalizedString name,
        byte width,
        byte height,
        byte durability)
    {
        if (gameConfiguration.Items.Any(i => i.Group == group && i.Number == number))
        {
            return;
        }

        var item = context.CreateNew<ItemDefinition>();
        item.Group = group;
        item.Number = number;
        item.Name = name;
        item.Width = width;
        item.Height = height;
        item.Durability = durability;
        item.SetGuid(group, number);
        gameConfiguration.Items.Add(item);
    }

    private static MonsterDefinition? EnsureMonster(
        IContext context,
        GameConfiguration gameConfiguration,
        short number,
        LocalizedString designation,
        NpcObjectKind objectKind,
        byte moveRange,
        byte attackRange,
        byte viewRange,
        int moveDelayMs,
        int attackDelayMs,
        int respawnSeconds,
        byte attribute,
        Skill? attackSkill,
        (AttributeDefinition Stat, float Value)[] stats)
    {
        if (gameConfiguration.Monsters.Any(m => m.Number == number))
        {
            return null;
        }

        var monster = context.CreateNew<MonsterDefinition>();
        monster.Number = number;
        monster.Designation = designation;
        monster.ObjectKind = objectKind;
        monster.MoveRange = moveRange;
        monster.AttackRange = attackRange;
        monster.ViewRange = viewRange;
        monster.MoveDelay = TimeSpan.FromMilliseconds(moveDelayMs);
        monster.AttackDelay = TimeSpan.FromMilliseconds(attackDelayMs);
        monster.RespawnDelay = TimeSpan.FromSeconds(respawnSeconds);
        monster.Attribute = attribute;
        monster.NumberOfMaximumItemDrops = objectKind == NpcObjectKind.Monster ? (byte)1 : (byte)0;
        monster.AttackSkill = attackSkill;
        foreach (var (stat, value) in stats)
        {
            var attr = context.CreateNew<MonsterAttribute>();
            attr.AttributeDefinition = stat;
            attr.Value = value;
            monster.Attributes.Add(attr);
        }

        monster.SetGuid(number);
        gameConfiguration.Monsters.Add(monster);
        return monster;
    }
}
