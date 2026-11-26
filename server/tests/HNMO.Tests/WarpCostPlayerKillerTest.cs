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

    /// <summary>
    /// Warping into a battle zone of the gens (owner, 26 Nov 2026): an outlaw from PK level 2 up pays twice the costs
    /// instead of 50 times; a warning or a normal hero pays the costs.
    /// </summary>
    /// <param name="state">The hero state.</param>
    /// <param name="expectedCosts">The expected costs.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.Normal, Costs)]
    [TestCase(HeroState.PlayerKillWarning, Costs)]
    [TestCase(HeroState.PlayerKiller1stStage, Costs * 2)]
    [TestCase(HeroState.PlayerKiller2ndStage, Costs * 2)]
    public async Task WarpIntoTheBattleZoneCostsByHeroStateAsync(HeroState state, int expectedCosts)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var warpInfo = CreateWarpInfo(player);
        MakeBattleZone(player, warpInfo.Gate!.Map!.Number);
        player.SelectedCharacter!.State = state;
        player.Money = 100_000;

        var allowed = CheckRequirements(player, warpInfo, out var errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.True, errorMessage);
            Assert.That(player.Money, Is.EqualTo(100_000 - expectedCosts));
        });
    }

    /// <summary>A map which is no battle zone keeps the 50 times, also while the gens system knows battle zones.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WarpOutOfTheBattleZoneKeepsFiftyTimesAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var warpInfo = CreateWarpInfo(player);
        MakeBattleZone(player, (short)(warpInfo.Gate!.Map!.Number + 1));
        player.SelectedCharacter!.State = HeroState.PlayerKiller2ndStage;
        player.Money = 100_000;

        Assert.That(CheckRequirements(player, warpInfo, out var errorMessage), Is.True, errorMessage);
        Assert.That(player.Money, Is.EqualTo(100_000 - (Costs * 50)));
    }

    /// <summary>One zen short of twice the costs refuses the warp and keeps the zen; exactly enough warps.</summary>
    /// <param name="money">The zen of the player.</param>
    /// <param name="allowedExpected">Whether the warp is allowed.</param>
    /// <returns>The task.</returns>
    [TestCase((Costs * 2) - 1, false)]
    [TestCase(Costs * 2, true)]
    public async Task OutlawIntoTheBattleZoneNeedsTwiceTheCostsAsync(int money, bool allowedExpected)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var warpInfo = CreateWarpInfo(player);
        MakeBattleZone(player, warpInfo.Gate!.Map!.Number);
        player.SelectedCharacter!.State = HeroState.PlayerKiller1stStage;
        player.Money = money;

        var allowed = CheckRequirements(player, warpInfo, out _);

        Assert.That(allowed, Is.EqualTo(allowedExpected));
        Assert.That(player.Money, Is.EqualTo(allowedExpected ? 0 : money));
    }

    private static void MakeBattleZone(Player player, short mapNumber)
    {
        var configuration = new GameLogic.Gens.GensConfiguration { BattleZoneMapNumbers = new List<short> { mapNumber } };
        if (player.GameContext.FeaturePlugIns.GetPlugIn<GameLogic.Gens.GensFeaturePlugIn>() is { } gens)
        {
            gens.Configuration = configuration;
        }
        else
        {
            player.GameContext.FeaturePlugIns.AddPlugIn(new GameLogic.Gens.GensFeaturePlugIn { Configuration = configuration }, true);
        }
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
