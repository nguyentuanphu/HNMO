// <copyright file="MasterPassives314To324Tests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameServer.RemoteView;
using HNMO.Persistence;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Unit tests for rank 6-9 passive master skills (<c>314..324</c> and equivalents):
/// UI tree slots, armor-set defense bonus, third-class wing bonuses, Cast Invincibility,
/// and full shield recovery on hit.
/// </summary>
[TestFixture]
public class MasterPassives314To324Tests
{
    private const byte BattleMasterNumber = 7;
    private const byte LordEmperorNumber = 17;
    private const byte FistMasterNumber = 25;

    /// <summary>
    /// Verifies client master skill tree slot indices for newly mapped rank 6-9 passives.
    /// </summary>
    [Test]
    public void MasterSkillIndicesMatchMasterTreeLayout()
    {
        var battleMaster = new CharacterClass { Number = BattleMasterNumber };
        var lordEmperor = new CharacterClass { Number = LordEmperorNumber };
        var fistMaster = new CharacterClass { Number = FistMasterNumber };

        byte Slot(short skillNumber, CharacterClass characterClass) =>
            new Skill { Number = skillNumber, MasterDefinition = new MasterSkillDefinition() }.GetMasterSkillIndex(characterClass);

        Assert.Multiple(() =>
        {
            Assert.That(Slot(314, battleMaster), Is.EqualTo(22));
            Assert.That(Slot(315, battleMaster), Is.EqualTo(23));
            Assert.That(Slot(316, battleMaster), Is.EqualTo(24));
            Assert.That(Slot(321, battleMaster), Is.EqualTo(29));
            Assert.That(Slot(322, battleMaster), Is.EqualTo(30));
            Assert.That(Slot(324, battleMaster), Is.EqualTo(34));
            Assert.That(Slot(366, lordEmperor), Is.EqualTo(94));
            Assert.That(Slot(506, lordEmperor), Is.EqualTo(31));
            Assert.That(Slot(592, fistMaster), Is.EqualTo(22));
            Assert.That(Slot(548, fistMaster), Is.EqualTo(29));
            Assert.That(Slot(549, fistMaster), Is.EqualTo(30));
            Assert.That(Slot(550, fistMaster), Is.EqualTo(34));
            Assert.That(Slot(615, fistMaster), Is.EqualTo(106));
        });
    }

    /// <summary>
    /// <see cref="Stats.ArmorSetDefenseBonus"/> adds to <see cref="Stats.DefenseFinal"/> only while a complete
    /// always-applied armor set is equipped, and updates reactively when the passive skill value changes.
    /// </summary>
    [Test]
    public void ArmorSetDefenseBonusAppliesOnlyWithCompleteSet()
    {
        var config = PlayerTestHelper.CreateGameConfiguration();
        var attributes = new AttributeSystem([], [], []);
        attributes.AddElement(new SimpleElement(100f, AggregateType.AddFinal), Stats.DefenseFinal);
        var setBonus = new SimpleElement(25f, AggregateType.AddRaw);
        attributes.AddElement(setBonus, Stats.ArmorSetDefenseBonus);

        var helmDef = new BasicModel.ItemDefinition { Group = 7, Number = 0 };
        var armorDef = new BasicModel.ItemDefinition { Group = 8, Number = 0 };
        var setGroup = new BasicModel.ItemSetGroup
        {
            AlwaysApplies = true,
            MinimumItemCount = 2,
            Options = new BasicModel.ItemOptionDefinition(),
        };
        setGroup.Items.Add(new BasicModel.ItemOfItemSet { ItemDefinition = helmDef });
        setGroup.Items.Add(new BasicModel.ItemOfItemSet { ItemDefinition = armorDef });
        helmDef.PossibleItemSetGroups.Add(setGroup);
        armorDef.PossibleItemSetGroups.Add(setGroup);

        var factory = new ItemPowerUpFactory(NullLogger<ItemPowerUpFactory>.Instance, config);

        // Incomplete set: no armor-set passive bonus.
        var helm = new BasicModel.Item { Definition = helmDef, Durability = 10, ItemSlot = 2 };
        var incomplete = factory.GetSetPowerUps([helm], attributes, config).ToList();
        Assert.That(attributes[Stats.DefenseFinal], Is.EqualTo(100f));
        foreach (var w in incomplete)
        {
            w.Dispose();
        }

        // Complete set: adds passive bonus and tracks live changes.
        var armor = new BasicModel.Item { Definition = armorDef, Durability = 10, ItemSlot = 3 };
        var complete = factory.GetSetPowerUps([helm, armor], attributes, config).ToList();
        Assert.That(attributes[Stats.DefenseFinal], Is.EqualTo(125f));

        setBonus.Value = 40f;
        Assert.That(attributes[Stats.DefenseFinal], Is.EqualTo(140f));

        foreach (var w in complete)
        {
            w.Dispose();
        }

        Assert.That(attributes[Stats.DefenseFinal], Is.EqualTo(100f));
    }

    /// <summary>
    /// Third-class wing passives (<see cref="Stats.MasterWingDefenseBonus"/>, <see cref="Stats.MasterWingAttackBonus"/>,
    /// and <see cref="Stats.MasterWingAbsorbBonus"/>) boost defense, damage, and damage absorption only when a 3rd wing is equipped.
    /// </summary>
    [Test]
    public void ThirdClassWingPassivesApplyOnlyWhenThirdWingEquipped()
    {
        var config = PlayerTestHelper.CreateGameConfiguration();
        var attributes = new AttributeSystem([], [], []);
        attributes.AddElement(new SimpleElement(1f, AggregateType.AddRaw), Stats.DamageReceiveDecrement);
        attributes.AddElement(new SimpleElement(50f, AggregateType.AddRaw), Stats.MasterWingDefenseBonus);
        attributes.AddElement(new SimpleElement(30f, AggregateType.AddRaw), Stats.MasterWingAttackBonus);
        attributes.AddElement(new SimpleElement(0.03f, AggregateType.AddRaw), Stats.MasterWingAbsorbBonus);

        var factory = new ItemPowerUpFactory(NullLogger<ItemPowerUpFactory>.Instance, config);

        // 2nd wing (12:5 Wings of Dragon) must not receive 3rd-wing passives.
        var secondWing = new BasicModel.Item { Definition = new BasicModel.ItemDefinition { Group = 12, Number = 5 }, Durability = 10, ItemSlot = 7 };
        var secondWrappers = factory.GetPowerUps(secondWing, attributes).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(attributes[Stats.DefenseFinal], Is.Zero);
            Assert.That(attributes[Stats.MaximumPhysBaseDmg], Is.Zero);
            Assert.That(attributes[Stats.DamageReceiveDecrement], Is.EqualTo(1f));
        });
        foreach (var w in secondWrappers)
        {
            w.Dispose();
        }

        // 3rd wing (12:37 Wing of Eternal) receives defense, physical + wizardry attack, and absorb reduction.
        var thirdWing = new BasicModel.Item { Definition = new BasicModel.ItemDefinition { Group = 12, Number = 37 }, Durability = 10, ItemSlot = 7 };
        var thirdWrappers = factory.GetPowerUps(thirdWing, attributes).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(attributes[Stats.DefenseFinal], Is.EqualTo(50f));
            Assert.That(attributes[Stats.MinimumPhysBaseDmg], Is.EqualTo(30f));
            Assert.That(attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(30f));
            Assert.That(attributes[Stats.MinimumWizBaseDmg], Is.EqualTo(30f));
            Assert.That(attributes[Stats.MaximumWizBaseDmg], Is.EqualTo(30f));
            Assert.That(attributes[Stats.DamageReceiveDecrement], Is.EqualTo(0.97f).Within(0.0001f));
        });

        foreach (var w in thirdWrappers)
        {
            w.Dispose();
        }
    }

    /// <summary>
    /// <see cref="Stats.CastInvincibilityChance"/> of <c>1.0</c> absorbs an incoming hit cleanly,
    /// and <see cref="Stats.FullyRecoverShieldAfterHitChance"/> of <c>1.0</c> restores SD on hit.
    /// </summary>
    [Test]
    public async Task CastInvincibilityAndFullShieldRecoveryTriggerOnIncomingHitAsync()
    {
        await using var attacker = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var gameContext = (GameContext)attacker.GameContext;
        gameContext.Configuration.Maps.Single().BattleZone = new BattleZoneDefinition();
        await using var defender = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);

        attacker.Attributes![Stats.Level] = 100;
        attacker.Attributes.AddElement(new SimpleElement(200f, AggregateType.AddRaw), Stats.MinimumPhysBaseDmg);
        attacker.Attributes.AddElement(new SimpleElement(200f, AggregateType.AddRaw), Stats.MaximumPhysBaseDmg);
        attacker.Attributes.AddElement(new SimpleElement(10000f, AggregateType.AddRaw), Stats.AttackRatePvp);

        defender.Attributes![Stats.Level] = 100;
        defender.Attributes.AddElement(new SimpleElement(1000f, AggregateType.AddRaw), Stats.MaximumHealth);
        defender.Attributes[Stats.CurrentHealth] = 1000;
        defender.Attributes.AddElement(new SimpleElement(500f, AggregateType.AddRaw), Stats.MaximumShield);
        defender.Attributes[Stats.CurrentShield] = 100;
        var invincibility = new SimpleElement(1.0f, AggregateType.AddRaw);
        defender.Attributes.AddElement(invincibility, Stats.CastInvincibilityChance);

        var invincibleHit = await defender.AttackByAsync(attacker, null, false).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(invincibleHit, Is.Not.Null);
            Assert.That(invincibleHit!.Value.TotalDamage, Is.Zero);
            Assert.That(defender.Attributes[Stats.CurrentHealth], Is.EqualTo(1000));
            Assert.That(defender.Attributes[Stats.CurrentShield], Is.EqualTo(100));
        });

        invincibility.Value = 0f;
        defender.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.FullyRecoverShieldAfterHitChance);

        var normalHit = await defender.AttackByAsync(attacker, null, false).ConfigureAwait(false);
        Assert.That(normalHit, Is.Not.Null);
        Assert.That(normalHit!.Value.TotalDamage, Is.GreaterThan(0));
        // Shield was topped up to MaximumShield (500) right before HitAsync subtracted ShieldDamage (< 500),
        // so CurrentShield stays well above the original 100.
        Assert.That(defender.Attributes[Stats.CurrentShield], Is.GreaterThan(100));
    }
}
