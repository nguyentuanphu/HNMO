// <copyright file="SeasonNineMonstersUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Adds the Season 9 monsters and NPCs missing from the Season 6 catalog and updates Dark Elf (Trainee Soldier)
/// respawn delay when running as Season 9.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineMonstersUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineMonstersUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A4F8B2C1-9E3D-4712-90B4-5C8D3E1A7904")]
public class SeasonNineMonstersUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    private const short DarkElfTraineeSoldierNumber = 412;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineMonstersUpdatePlugIn"/> class.
    /// </summary>
    public SeasonNineMonstersUpdatePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineMonstersUpdatePlugIn"/> class with an explicit season check.
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    internal SeasonNineMonstersUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineMonstersUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineMonstersUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 0, 20, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var existingNumbers = gameConfiguration.Monsters.Select(m => m.Number).ToHashSet();
        foreach (var spec in SeasonNineMonstersCatalog.All)
        {
            if (existingNumbers.Contains(spec.Number))
            {
                continue;
            }

            var monster = CreateMonster(context, gameConfiguration, spec);
            gameConfiguration.Monsters.Add(monster);
            existingNumbers.Add(spec.Number);
        }

        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == DarkElfTraineeSoldierNumber) is { } darkElf
            && darkElf.RespawnDelay == TimeSpan.FromSeconds(10))
        {
            darkElf.RespawnDelay = TimeSpan.FromSeconds(10800);
        }

        return ValueTask.CompletedTask;
    }

    private static MonsterDefinition CreateMonster(
        IContext context,
        GameConfiguration gameConfiguration,
        SeasonNineMonstersCatalog.SeasonNineMonsterSpec spec)
    {
        var monster = context.CreateNew<MonsterDefinition>();
        monster.Number = spec.Number;
        monster.Designation = LocalizedString.FromResource(spec.NameExpression);
        monster.ObjectKind = spec.ObjectKind;
        monster.NpcWindow = spec.NpcWindow;
        monster.MoveRange = spec.MoveRange;
        monster.AttackRange = spec.AttackRange;
        monster.ViewRange = spec.ViewRange;
        monster.MoveDelay = TimeSpan.FromMilliseconds(spec.MoveDelayMs);
        monster.AttackDelay = TimeSpan.FromMilliseconds(spec.AttackDelayMs);
        monster.RespawnDelay = TimeSpan.FromSeconds(spec.RegenDelaySec);
        monster.Attribute = spec.Attribute;
        monster.NumberOfMaximumItemDrops = spec.NumberOfMaximumItemDrops;
        if (spec.IsTrap)
        {
            monster.IntelligenceTypeName = typeof(AttackSingleWhenPressedTrapIntelligence).FullName;
        }

        if (spec.Attribute > 0 || spec.ObjectKind is NpcObjectKind.Statue or NpcObjectKind.Destructible)
        {
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, spec.Level },
                { Stats.MaximumHealth, spec.MaximumHealth },
                { Stats.MinimumPhysBaseDmg, spec.MinimumDamage },
                { Stats.MaximumPhysBaseDmg, spec.MaximumDamage },
                { Stats.DefenseBase, spec.Defense },
                { Stats.AttackRatePvm, spec.AttackRate },
                { Stats.DefenseRatePvm, spec.DefenseRate },
            };

            AddResistanceIfNonZero(attributes, Stats.IceResistance, spec.IceResistance);
            AddResistanceIfNonZero(attributes, Stats.PoisonResistance, spec.PoisonResistance);
            AddResistanceIfNonZero(attributes, Stats.LightningResistance, spec.LightningResistance);
            AddResistanceIfNonZero(attributes, Stats.FireResistance, spec.FireResistance);

            monster.AddAttributes(attributes, context, gameConfiguration);
        }

        monster.SetGuid(monster.Number);
        return monster;
    }

    private static void AddResistanceIfNonZero(
        IDictionary<AttributeDefinition, float> attributes,
        AttributeDefinition resistance,
        int value)
    {
        if (value > 0)
        {
            attributes[resistance] = value / 255f;
        }
    }
}
