// <copyright file="SummonPartyPlayerKillerTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Skills;

/// <summary>
/// Tests that a Dark Lord can't summon party members from PK level 2 up, as the Season 6/9 server suites
/// (CanSummonByDarkLord is off for PK level 2 and murderer).
/// </summary>
[TestFixture]
public class SummonPartyPlayerKillerTest
{
    /// <summary>
    /// Party members of PK level 2 and above are not summoned; Warning and below are.
    /// </summary>
    /// <param name="state">The hero state of the party member.</param>
    /// <param name="canBeSummoned">Whether the member can be summoned.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.Normal, true)]
    [TestCase(HeroState.Hero, true)]
    [TestCase(HeroState.PlayerKillWarning, true)]
    [TestCase(HeroState.PlayerKiller1stStage, false)]
    [TestCase(HeroState.PlayerKiller2ndStage, false)]
    public async Task OutlawsFromPkLevelTwoAreNotSummonedAsync(HeroState state, bool canBeSummoned)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var darkLord = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var member = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var party = new Party(new PartyManager(5, new NullLogger<Party>()), 5, new NullLogger<Party>());
        await party.AddAsync(darkLord).ConfigureAwait(false);
        await party.AddAsync(member).ConfigureAwait(false);
        member.SelectedCharacter!.State = state;

        var method = typeof(SummonPartySkillPlugin).GetMethod("CanPlayerSummonTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (bool)method.Invoke(new SummonPartySkillPlugin(), [darkLord, member])!;

        Assert.That(result, Is.EqualTo(canBeSummoned));
    }
}
