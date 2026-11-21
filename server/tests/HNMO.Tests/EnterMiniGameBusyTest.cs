// <copyright file="EnterMiniGameBusyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.MiniGames;
using HNMO.GameLogic.MiniGames;
using Moq;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// A mini game can't be entered while trading, in another NPC dialog, dead or duelling (owner, 21 Nov 2026).
/// The test game costs more zen than the player has, so a player who passes these checks is refused for the
/// fee instead (<see cref="EnterResult.NotEnoughMoney"/>) - without starting a game.
/// </summary>
[TestFixture]
public class EnterMiniGameBusyTest
{
    /// <summary>A trading player can't enter.</summary>
    [Test]
    public async Task TradingPlayerCantEnterAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false), Is.True, "test setup");

        await EnterAndAssertAsync(player, EnterResult.Failed).ConfigureAwait(false);
    }

    /// <summary>A dead player can't enter.</summary>
    [Test]
    public async Task DeadPlayerCantEnterAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        player.IsAlive = false;

        await EnterAndAssertAsync(player, EnterResult.Failed).ConfigureAwait(false);
    }

    /// <summary>A duelist can't enter.</summary>
    [Test]
    public async Task DuelistCantEnterAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var opponent = await PlayerTestHelper.CreatePlayerAsync(player.GameContext).ConfigureAwait(false);
        player.DuelRoom = new DuelRoom(new BasicModel.DuelArea(), player, opponent);

        await EnterAndAssertAsync(player, EnterResult.Failed).ConfigureAwait(false);
    }

    /// <summary>A player in the dialog of another NPC (here a merchant) can't enter.</summary>
    [Test]
    public async Task PlayerInAnotherNpcDialogCantEnterAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        await OpenNpcAsync(player, NpcWindow.Merchant).ConfigureAwait(false);

        await EnterAndAssertAsync(player, EnterResult.Failed).ConfigureAwait(false);
    }

    /// <summary>The dialog of the game's own entry NPC may be open.</summary>
    [Test]
    public async Task PlayerAtTheEntryNpcMayEnterAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        await OpenNpcAsync(player, NpcWindow.BloodCastle).ConfigureAwait(false);

        await EnterAndAssertAsync(player, EnterResult.NotEnoughMoney).ConfigureAwait(false);
    }

    /// <summary>A player who just stands in the world may enter.</summary>
    [Test]
    public async Task PlayerInTheWorldMayEnterAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);

        await EnterAndAssertAsync(player, EnterResult.NotEnoughMoney).ConfigureAwait(false);
    }

    private static async Task EnterAndAssertAsync(Player player, EnterResult expected)
    {
        await new EnterMiniGameAction().TryEnterMiniGameAsync(player, MiniGameType.BloodCastle, 1, 0xFF).ConfigureAwait(false);

        var view = Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMiniGameEnterResultPlugIn>()!);
        view.Verify(v => v!.ShowResultAsync(MiniGameType.BloodCastle, expected), Times.Once);
        view.Verify(v => v!.ShowResultAsync(MiniGameType.BloodCastle, It.Is<EnterResult>(r => r != expected)), Times.Never);
    }

    private static async Task OpenNpcAsync(Player player, NpcWindow window)
    {
        var definition = new BasicModel.MonsterDefinition { NpcWindow = window };
        player.OpenedNpc = new NonPlayerCharacter(null!, definition, player.CurrentMap!);
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false), Is.True, "test setup");
    }

    private static async Task<Player> CreatePlayerAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var map = player.CurrentMap!.Definition;
        context.Configuration.MiniGameDefinitions.Add(new BasicModel.MiniGameDefinition
        {
            Type = MiniGameType.BloodCastle,
            GameLevel = 1,
            MinimumCharacterLevel = 0,
            MaximumCharacterLevel = 400,
            MinimumSpecialCharacterLevel = 0,
            MaximumSpecialCharacterLevel = 400,
            EntranceFee = 1_000_000,
            ArePlayerKillersAllowedToEnter = true,
            Entrance = new BasicModel.ExitGate { Map = map, X1 = 10, X2 = 10, Y1 = 10, Y2 = 10 },
        });
        player.Money = 0;
        return player;
    }
}
