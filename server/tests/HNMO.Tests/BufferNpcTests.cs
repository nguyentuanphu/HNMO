// <copyright file="BufferNpcTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.GameLogic.PlugIns.BufferNpc;
using HNMO.PlugIns;

/// <summary>
/// Unit and integration tests for <see cref="BufferNpcEngine"/> and <see cref="BufferNpcPlugIn"/>.
/// </summary>
[TestFixture]
public class BufferNpcTests
{
    /// <summary>Disabled plug-in returns <see cref="BufferNpcDenialReason.Disabled"/>.</summary>
    [Test]
    public void EngineRejectsWhenDisabled()
    {
        var cfg = new BufferNpcConfiguration();
        var effects = CreateEffects(1, 2, 3);
        var decision = BufferNpcEngine.Evaluate(cfg, false, 257, 10, 0, 0, effects);
        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo(BufferNpcDenialReason.Disabled));
    }

    /// <summary>Verifies level cap (inclusive), reset limit, zen fee, and non-matching NPC handling.</summary>
    [Test]
    public void EngineEnforcesLevelResetsZenAndNpcFilter()
    {
        var cfg = new BufferNpcConfiguration
        {
            MaxLevel = 200,
            MaxResets = 2,
            ZenCost = 5000,
            DurationSeconds = 900,
            NpcNumbers = [257],
            BuffEffectNumbers = [3, 1, 2, 99],
        };
        var effects = CreateEffects(1, 2, 3);

        var wrongNpc = BufferNpcEngine.Evaluate(cfg, true, 250, 50, 0, 10000, effects);
        Assert.That(wrongNpc.Allowed, Is.False);
        Assert.That(wrongNpc.Reason, Is.EqualTo(BufferNpcDenialReason.NpcNotConfigured));

        var tooHigh = BufferNpcEngine.Evaluate(cfg, true, 257, 201, 0, 10000, effects);
        Assert.That(tooHigh.Allowed, Is.False);
        Assert.That(tooHigh.Reason, Is.EqualTo(BufferNpcDenialReason.LevelTooHigh));

        var resetsHigh = BufferNpcEngine.Evaluate(cfg, true, 257, 150, 3, 10000, effects);
        Assert.That(resetsHigh.Allowed, Is.False);
        Assert.That(resetsHigh.Reason, Is.EqualTo(BufferNpcDenialReason.ResetsTooHigh));

        var lowZen = BufferNpcEngine.Evaluate(cfg, true, 257, 150, 2, 4999, effects);
        Assert.That(lowZen.Allowed, Is.False);
        Assert.That(lowZen.Reason, Is.EqualTo(BufferNpcDenialReason.NotEnoughZen));

        var ok = BufferNpcEngine.Evaluate(cfg, true, 257, 200, 2, 5000, effects);
        Assert.That(ok.Allowed, Is.True);
        Assert.That(ok.Reason, Is.EqualTo(BufferNpcDenialReason.None));
        Assert.That(ok.Effects.Select(e => e.Number), Is.EqualTo(new short[] { 3, 1, 2 }));
        Assert.That(ok.DurationOverrideSeconds, Is.EqualTo(900));
        Assert.That(ok.ZenCost, Is.EqualTo(5000));
    }

    /// <summary>Verifies <see cref="BufferNpcEngine.ParseShortIdList"/> parses distinct positive IDs.</summary>
    [Test]
    public void ParseShortIdListFiltersInvalidAndDuplicates()
    {
        var parsed = BufferNpcEngine.ParseShortIdList("257, 1; 2 257 abc -5 0 8");
        Assert.That(parsed, Is.EqualTo(new short[] { 257, 1, 2, 8 }));
    }

    /// <summary>Verifies <see cref="NpcBuffRequestAction"/> applies all configured buffs and deducts zen when active.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask NpcBuffRequestAppliesConfiguredBuffsAndRefusesOverMaxLevelAsync()
    {
        var pluginCfg = new PlugInConfiguration
        {
            TypeId = typeof(BufferNpcPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new BufferNpcConfiguration
            {
                MaxLevel = 150,
                MaxResets = -1,
                DurationSeconds = 600,
                ZenCost = 1000,
                NpcNumbers = [257],
                BuffEffectNumbers = [1, 2, 3],
            }),
        };

        var context = GameContextTestHelper.CreateGameContext([pluginCfg]);
        var effects = CreateEffects(1, 2, 3);
        foreach (var e in effects.Values)
        {
            context.Configuration.MagicEffects.Add(e);
        }

        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Attributes![Stats.Level] = 120;
        player.TryAddMoney(2500);

        var elfNpcDef = new MonsterDefinition
        {
            Number = 257,
            Designation = "Elf Soldier",
            NpcWindow = NpcWindow.NpcDialog,
            ObjectKind = NpcObjectKind.PassiveNpc,
        };
        var npc = new NonPlayerCharacter(new MonsterSpawnArea(), elfNpcDef, player.CurrentMap!);
        player.OpenedNpc = npc;

        var action = new NpcBuffRequestAction();
        await action.RequestBuffAsync(player).ConfigureAwait(false);

        Assert.That(player.MagicEffectList.ContainsEffect(1), Is.True);
        Assert.That(player.MagicEffectList.ContainsEffect(2), Is.True);
        Assert.That(player.MagicEffectList.ContainsEffect(3), Is.True);
        Assert.That(player.Money, Is.EqualTo(1500));

        // Now raise level above MaxLevel and verify new request is rejected without charging zen.
        await player.MagicEffectList.ClearAllEffectsProducingSpecificStatAsync(Stats.DefenseFinal).ConfigureAwait(false);
        await player.MagicEffectList.ClearAllEffectsProducingSpecificStatAsync(Stats.GreaterDamageBonus).ConfigureAwait(false);
        player.Attributes![Stats.Level] = 180;
        await action.RequestBuffAsync(player).ConfigureAwait(false);

        Assert.That(player.MagicEffectList.ContainsEffect(1), Is.False);
        Assert.That(player.MagicEffectList.ContainsEffect(2), Is.False);
        Assert.That(player.MagicEffectList.ContainsEffect(3), Is.False);
        Assert.That(player.Money, Is.EqualTo(1500));
    }

    private static Dictionary<short, MagicEffectDefinition> CreateEffects(params short[] ids)
    {
        var dict = new Dictionary<short, MagicEffectDefinition>();
        foreach (var id in ids)
        {
            var effect = new HNMO.Persistence.BasicModel.MagicEffectDefinition
            {
                Number = id,
                InformObservers = true,
                Duration = new HNMO.Persistence.BasicModel.PowerUpDefinitionValue
                {
                    ConstantValue = { Value = 120, AggregateType = AggregateType.AddRaw },
                },
                PowerUpDefinitions =
                {
                    new HNMO.Persistence.BasicModel.PowerUpDefinition
                    {
                        TargetAttribute = id == 1 ? Stats.GreaterDamageBonus : Stats.DefenseFinal,
                        Boost = new HNMO.Persistence.BasicModel.PowerUpDefinitionValue
                        {
                            ConstantValue = { Value = 25, AggregateType = AggregateType.AddRaw },
                        },
                    },
                },
            };
            dict[id] = effect;
        }

        return dict;
    }
}
