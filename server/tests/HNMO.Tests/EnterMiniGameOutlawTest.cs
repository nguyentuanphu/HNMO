// <copyright file="EnterMiniGameOutlawTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Reflection;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.MiniGames;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests that outlaws of every stage, the Warning stage included, can't enter the events (Blood Castle,
/// Devil Square, Chaos Castle, Illusion Temple and the other mini games), as the Season 6/9 server suites
/// (CanEnterEventWithPK = 0).
/// </summary>
[TestFixture]
public class EnterMiniGameOutlawTest
{
    /// <summary>
    /// Only players without an outlaw state may enter an event which doesn't allow player killers.
    /// </summary>
    /// <param name="state">The hero state.</param>
    /// <param name="mayEnter">Whether the player may enter.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.New, true)]
    [TestCase(HeroState.Hero, true)]
    [TestCase(HeroState.Normal, true)]
    [TestCase(HeroState.PlayerKillWarning, false)]
    [TestCase(HeroState.PlayerKiller1stStage, false)]
    [TestCase(HeroState.PlayerKiller2ndStage, false)]
    public async Task OutlawsCantEnterAsync(HeroState state, bool mayEnter)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.State = state;
        var definition = new BasicModel.MiniGameDefinition { ArePlayerKillersAllowedToEnter = false };

        var method = typeof(EnterMiniGameAction).GetMethod("CheckPlayerKillState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (bool)method.Invoke(new EnterMiniGameAction(), [definition, player])!;

        Assert.That(result, Is.EqualTo(mayEnter));
    }

    /// <summary>
    /// An event which allows player killers lets every stage in.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EventAllowingPlayerKillersLetsThemInAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.State = HeroState.PlayerKiller2ndStage;
        var definition = new BasicModel.MiniGameDefinition { ArePlayerKillersAllowedToEnter = true };

        var method = typeof(EnterMiniGameAction).GetMethod("CheckPlayerKillState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (bool)method.Invoke(new EnterMiniGameAction(), [definition, player])!;

        Assert.That(result, Is.True);
    }
}
