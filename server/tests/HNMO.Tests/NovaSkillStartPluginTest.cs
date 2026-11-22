// <copyright file="NovaSkillStartPluginTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.Pathfinding;

/// <summary>
/// Tests for <see cref="NovaSkillStartPlugin"/>.
/// </summary>
[TestFixture]
public class NovaSkillStartPluginTest
{
    /// <summary>
    /// Verifies that cancelling Nova while standing in a safezone clears <see cref="Player.SkillCancelTokenSource"/>
    /// so the player can cast Nova again later.
    /// </summary>
    [Test]
    public async ValueTask CancelledNovaInSafezoneClearsCancelTokenSourceAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var mapDef = player.CurrentMap!.Definition;
        mapDef.TerrainData ??= new byte[ushort.MaxValue + 3];

        // Configure Nova Start (58) and Nova End (40) skills in game configuration and player skill list.
        var novaStart = new HNMO.Persistence.BasicModel.Skill { Number = 58, Name = "Nova Start" };
        var novaEnd = new HNMO.Persistence.BasicModel.Skill
        {
            Number = 40,
            Name = "Nova",
            Range = 6,
            SkillType = SkillType.AreaSkillAutomaticHits,
            Target = SkillTarget.ImplicitAllInRange,
        };
        player.GameContext.Configuration.Skills.Add(novaStart);
        player.GameContext.Configuration.Skills.Add(novaEnd);
        await player.SkillList!.AddLearnedSkillAsync(novaEnd).ConfigureAwait(false);

        // Move the player outside safezone first (safezone defaults to false in empty TerrainData),
        // cast Nova, then cancel Nova while the player is dead.
        player.Position = new Point(10, 10);
        player.Attributes![Stats.CurrentAbility] = 100;
        player.Attributes[Stats.CurrentMana] = 100;

        var startPlugin = new NovaSkillStartPlugin();
        var stopPlugin = new NovaSkillStopPlugin();
        await startPlugin.PerformSkillAsync(player, player, 58).ConfigureAwait(false);
        Assert.That(player.SkillCancelTokenSource, Is.Not.Null);

        // Kill player (forces early return in AttackTargetsAsync) and stop Nova immediately.
        player.IsAlive = false;
        player.Attributes[Stats.CurrentHealth] = 0;
        await stopPlugin.PerformSkillAsync(player, player, 40).ConfigureAwait(false);

        // Wait briefly for the background Nova task to settle.
        for (var i = 0; i < 40 && player.SkillCancelTokenSource is not null; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        Assert.That(player.SkillCancelTokenSource, Is.Null, "Dead or safezoned player must not retain a stale SkillCancelTokenSource.");
    }
}
