// <copyright file="PkChatCommandPointsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ChatCommands;

/// <summary>
/// The outlaw points a game master's <c>/pk</c> gives: the character stays in the stage asked for after the next tick
/// (found in the real game: <c>/pk name 2 2</c> fell to the Warning stage a second later).
/// </summary>
[TestFixture]
public class PkChatCommandPointsTest
{
    /// <summary>The stage holds a tick later and still six hours later minus one second.</summary>
    /// <param name="state">The stage asked for.</param>
    [TestCase(HeroState.PlayerKillWarning)]
    [TestCase(HeroState.PlayerKiller1stStage)]
    [TestCase(HeroState.PlayerKiller2ndStage)]
    public void TheStageHoldsAfterTheNextTick(HeroState state)
    {
        var points = PkChatCommandPlugIn.GetPoints(state);

        Assert.That(OutlawPoints.GetState(points), Is.EqualTo(state));
        Assert.That(OutlawPoints.GetState(points - 1), Is.EqualTo(state), "one tick later");
        Assert.That(OutlawPoints.GetState(points - (OutlawPoints.WarningPoints - 1)), Is.EqualTo(state), "six hours later, less a second");
    }
}
