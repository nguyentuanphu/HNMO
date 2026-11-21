// <copyright file="WarpCostPlayerKillerTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Reflection;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests that the warp of the M menu (and /move) costs 50 times as much from PK level 2 up, as the
/// Season 6/9 server suites (WarpChargeMultiplier 50 for PK level 2 and murderer).
/// </summary>
[TestFixture]
public class WarpCostPlayerKillerTest
{
    private const int Costs = 1_000;

    /// <summary>
    /// The warp takes the cost times the multiplier of the hero state.
    /// </summary>
    /// <param name="state">The hero state.</param>
    /// <param name="expectedCosts">The expected costs.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.Normal, Costs)]
    [TestCase(HeroState.PlayerKillWarning, Costs)]
    [TestCase(HeroState.PlayerKiller1stStage, Costs * 50)]
    [TestCase(HeroState.PlayerKiller2ndStage, Costs * 50)]
    public async Task WarpCostsByHeroStateAsync(HeroState state, int expectedCosts)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.State = state;
        player.Money = 100_000;

        var allowed = CheckRequirements(player, CreateWarpInfo(player), out var errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.True, errorMessage);
            Assert.That(player.Money, Is.EqualTo(100_000 - expectedCosts));
        });
    }

    /// <summary>
    /// A murderer without 50 times the cost can't warp and keeps the zen.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task MurdererWithoutEnoughZenCantWarpAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.State = HeroState.PlayerKiller2ndStage;
        player.Money = 10_000;

        var allowed = CheckRequirements(player, CreateWarpInfo(player), out var errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.False);
            Assert.That(errorMessage, Does.Contain("50000"));
            Assert.That(player.Money, Is.EqualTo(10_000));
        });
    }

    private static BasicModel.WarpInfo CreateWarpInfo(Player player)
    {
        return new BasicModel.WarpInfo
        {
            Costs = Costs,
            LevelRequirement = 0,
            Gate = new BasicModel.ExitGate { Map = player.CurrentMap!.Definition, X1 = 130, Y1 = 130, X2 = 131, Y2 = 131 },
        };
    }

    private static bool CheckRequirements(Player player, DataModel.Configuration.WarpInfo warpInfo, out string? errorMessage)
    {
        var method = typeof(WarpAction).GetMethod("CheckRequirements", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = new object?[] { player, warpInfo, null };
        var result = (bool)method.Invoke(new WarpAction(), arguments)!;
        errorMessage = arguments[2] as string;
        return result;
    }
}
