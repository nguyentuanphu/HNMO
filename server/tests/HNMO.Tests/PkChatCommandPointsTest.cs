// <copyright file="PkChatCommandPointsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ChatCommands;

/// <summary>
/// The outlaw points a game master's <c>/pk</c> gives per season: full 3h/stage totals in Season 6.3
/// and interior stage totals in Season 9, so the stage holds after ticks or initial monster kills.
/// </summary>
[TestFixture]
public class PkChatCommandPointsTest
{
    /// <summary>The stage holds a tick later and for almost 3 hours in Season 6.3.</summary>
    /// <param name="state">The stage asked for.</param>
    [TestCase(HeroState.PlayerKillWarning)]
    [TestCase(HeroState.PlayerKiller1stStage)]
    [TestCase(HeroState.PlayerKiller2ndStage)]
    public void SeasonSixStageHoldsThreeHours(HeroState state)
    {
        var points = PkChatCommandPlugIn.GetPoints(state, isSeasonNine: false);

        Assert.That(OutlawPoints.GetState(points, isSeasonNine: false), Is.EqualTo(state));
        Assert.That(OutlawPoints.GetState(points - 1, isSeasonNine: false), Is.EqualTo(state), "one tick later");
        Assert.That(OutlawPoints.GetState(points - (OutlawPoints.FormerStageSeconds - 1), isSeasonNine: false), Is.EqualTo(state), "3 hours later, less a second");
    }

    /// <summary>The stage holds well inside its Season 9 range even after initial monster kills.</summary>
    /// <param name="state">The stage asked for.</param>
    [TestCase(HeroState.PlayerKillWarning)]
    [TestCase(HeroState.PlayerKiller1stStage)]
    [TestCase(HeroState.PlayerKiller2ndStage)]
    public void SeasonNineStageHoldsInsideRange(HeroState state)
    {
        var points = PkChatCommandPlugIn.GetPoints(state, isSeasonNine: true);

        Assert.That(OutlawPoints.GetState(points, isSeasonNine: true), Is.EqualTo(state));
        Assert.That(OutlawPoints.GetState(points - 500, isSeasonNine: true), Is.EqualTo(state), "does not drop out after monster kills");
    }
}
