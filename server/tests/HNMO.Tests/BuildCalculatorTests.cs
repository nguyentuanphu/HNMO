// <copyright file="BuildCalculatorTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.BuildCalculator;

/// <summary>
/// Formula-parity tests ensuring <see cref="BuildCalculatorService"/> computes identical damage,
/// defense, speed, HP/MP/SD/AG, and rates to a live <see cref="Player"/> in <see cref="GameContext"/> (ROADMAP 14.21).
/// </summary>
[TestFixture]
public class BuildCalculatorTests
{
    /// <summary>
    /// Verifies exact numeric parity between <see cref="BuildCalculatorService.Compute"/> and a real logged-in player's
    /// <see cref="Player.Attributes"/> (with equipped item powerups).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Compute_MatchesLivePlayerAttributesExactly()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var config = player.GameContext.Configuration;
        var sc = player.SelectedCharacter!;
        config.CharacterClasses.Add(sc.CharacterClass!);

        sc.Attributes.First(a => a.Definition == Stats.Level).Value = 250;
        sc.Attributes.First(a => a.Definition == Stats.BaseStrength).Value = 600;
        sc.Attributes.First(a => a.Definition == Stats.BaseAgility).Value = 400;
        sc.Attributes.First(a => a.Definition == Stats.BaseVitality).Value = 200;
        sc.Attributes.First(a => a.Definition == Stats.BaseEnergy).Value = 120;

        // Equip a real weapon on live player so item power-ups run on both paths
        var swordDef = config.Items.FirstOrDefault(i => i.Group == 0 && i.ItemSlot?.ItemSlots.Contains(0) == true);
        if (swordDef is not null)
        {
            var item = new HNMO.DataModel.Entities.Item
            {
                Definition = swordDef,
                Level = 9,
                Durability = Math.Max((byte)1, swordDef.Durability),
                ItemSlot = 0,
            };
            await player.Inventory!.AddItemAsync(0, item).ConfigureAwait(false);
        }

        var metadata = BuildCalculatorService.GetMetadata(config);
        Assert.That(metadata, Is.Not.Null);

        var calcInput = new BuildCalculatorInput
        {
            ClassNumber = sc.CharacterClass!.Number,
            Level = 250,
            Strength = 600,
            Agility = 400,
            Vitality = 200,
            Energy = 120,
            Items = swordDef is null ? [] : [new BuildCalculatorItemInput { Slot = 0, Group = swordDef.Group, Number = swordDef.Number, Level = 9 }],
        };

        var calc = BuildCalculatorService.Compute(config, calcInput);
        Assert.That(calc, Is.Not.Null);
        Assert.That(calc!.TotalStrength, Is.EqualTo((int)Math.Round(player.Attributes![Stats.TotalStrength])));
        Assert.That(calc.TotalAgility, Is.EqualTo((int)Math.Round(player.Attributes[Stats.TotalAgility])));
        Assert.That(calc.TotalVitality, Is.EqualTo((int)Math.Round(player.Attributes[Stats.TotalVitality])));
        Assert.That(calc.TotalEnergy, Is.EqualTo((int)Math.Round(player.Attributes[Stats.TotalEnergy])));
        Assert.That(calc.MinPhysDmg, Is.EqualTo((int)Math.Round(player.Attributes[Stats.MinimumPhysBaseDmg])));
        Assert.That(calc.MaxPhysDmg, Is.EqualTo((int)Math.Round(player.Attributes[Stats.MaximumPhysBaseDmg])));
        Assert.That(calc.DefensePvm, Is.EqualTo((int)Math.Round(player.Attributes[Stats.DefensePvm])));
        Assert.That(calc.AttackSpeed, Is.EqualTo((int)Math.Round(player.Attributes[Stats.AttackSpeed])));
        Assert.That(calc.MaximumHealth, Is.EqualTo((int)Math.Round(player.Attributes[Stats.MaximumHealth])));
        Assert.That(calc.MaximumMana, Is.EqualTo((int)Math.Round(player.Attributes[Stats.MaximumMana])));
        Assert.That(calc.DefenseRatePvm, Is.EqualTo((int)Math.Round(player.Attributes[Stats.DefenseRatePvm])));
    }
}
