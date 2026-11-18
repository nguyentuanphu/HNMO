// <copyright file="QolTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Duel;
using HNMO.GameLogic.PlayerActions.Party;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.Qol;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using Moq;

/// <summary>
/// Unit tests for <see cref="QolPlugIn"/> (/requests, /srvinfo, /mine) and <c>/lock</c>.
/// </summary>
[TestFixture]
public class QolTests
{
    private string _statePath = string.Empty;

    /// <summary>Creates a fresh temp state file path per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._statePath = Path.Combine(Path.GetTempPath(), $"hnmo-qol-{Guid.NewGuid():N}.json");
    }

    /// <summary>Deletes temp state file after test.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._statePath))
        {
            File.Delete(this._statePath);
        }
    }

    /// <summary>Verifies /requests on|off blocks trade, party, and duel requests when enabled.</summary>
    [Test]
    public async ValueTask RequestsToggleBlocksIncomingTradePartyAndDuelAsync()
    {
        var sender = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var receiver = await PlayerTestHelper.CreatePlayerAsync(sender.GameContext).ConfigureAwait(false);
        receiver.SelectedCharacter!.Name = "NoInviteHero";

        var qol = new QolPlugIn
        {
            Configuration = new QolConfiguration
            {
                Enabled = true,
                RequestsToggleEnabled = true,
            },
        };
        sender.GameContext.FeaturePlugIns.AddPlugIn(qol, true);

        Assert.That(QolPlugIn.AreRequestsBlocked(receiver), Is.False);
        var blocked = await qol.HandleRequestsCommandAsync(receiver, "off").ConfigureAwait(false);
        Assert.That(blocked, Is.True);
        Assert.That(QolPlugIn.AreRequestsBlocked(receiver), Is.True);

        var tradeOk = await new TradeRequestAction().RequestTradeAsync(sender, receiver).ConfigureAwait(false);
        Assert.That(tradeOk, Is.False);

        await new PartyRequestAction().HandlePartyRequestAsync(sender, receiver).ConfigureAwait(false);
        Assert.That(receiver.Party, Is.Null);

        await new DuelActions().HandleDuelRequestAsync(sender, receiver).ConfigureAwait(false);
        Assert.That(receiver.DuelRoom, Is.Null);

        var unblocked = await qol.HandleRequestsCommandAsync(receiver, "on").ConfigureAwait(false);
        Assert.That(unblocked, Is.False);
        Assert.That(QolPlugIn.AreRequestsBlocked(receiver), Is.False);
    }

    /// <summary>Verifies /srvinfo displays live server rates and coin balances without error.</summary>
    [Test]
    public async ValueTask ServerInfoOutputsLiveRatesAndCoinsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.WCoinC = 120;
        var messages = new List<string>();
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Setup(m => m.ShowMessageAsync(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Callback<string, MessageType>((msg, _) => messages.Add(msg))
            .Returns(ValueTask.CompletedTask);

        var qol = new QolPlugIn
        {
            Configuration = new QolConfiguration
            {
                Enabled = true,
                ServerInfoEnabled = true,
            },
        };
        await qol.HandleServerInfoCommandAsync(player).ConfigureAwait(false);
        Assert.That(messages, Has.Count.EqualTo(1));
        Assert.That(messages[0], Does.Contain("120 WCC"));
    }

    /// <summary>Verifies /mine enforces cooldown, allowed map, pick requirement, and records yields in QolStore.</summary>
    [Test]
    public async ValueTask MiningEnforcesCooldownPickAndRecordsStoreAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.Name = "MinerOne";

        var fakeClock = new ManualClock(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        var qol = new QolPlugIn(fakeClock)
        {
            Configuration = new QolConfiguration
            {
                Enabled = true,
                FileName = this._statePath,
                MiningEnabled = true,
                MiningCooldownSeconds = 10,
                MiningAllowedMapNumbers = [0],
                MiningRequiredItemGroup = 14,
                MiningRequiredItemNumber = 199,
                MiningRewards =
                [
                    new MiningRewardEntry { Weight = 100, NameEn = "Copper Ore (25k)", NameVi = "Quặng Đồng (25k)", Zen = 25_000 },
                ],
            },
        };

        // Missing pick -> refused cleanly.
        Assert.That(await qol.HandleMineCommandAsync(player).ConfigureAwait(false), Is.False);

        // Give pick item -> succeeds!
        var pick = player.PersistenceContext.CreateNew<HNMO.DataModel.Entities.Item>();
        pick.Definition = new ItemDefinition { Group = 14, Number = 199, Durability = 10, Width = 1, Height = 2 };
        pick.Durability = 10;
        await player.Inventory!.AddItemAsync(12, pick).ConfigureAwait(false);

        Assert.That(await qol.HandleMineCommandAsync(player).ConfigureAwait(false), Is.True);
        Assert.That(player.Money, Is.EqualTo(25_000));

        // Cooldown active 5s later -> refused; 11s later -> succeeds!
        fakeClock.Advance(TimeSpan.FromSeconds(5));
        Assert.That(await qol.HandleMineCommandAsync(player).ConfigureAwait(false), Is.False);

        fakeClock.Advance(TimeSpan.FromSeconds(6));
        Assert.That(await qol.HandleMineCommandAsync(player).ConfigureAwait(false), Is.True);
        Assert.That(player.Money, Is.EqualTo(50_000));
        Assert.That(QolStore.Open(this._statePath).Snapshot().TotalMinesCompleted, Is.EqualTo(2));
    }

    /// <summary>Verifies AccountSafetyPlugIn.LockPinAsync revokes unlocked PIN session state immediately.</summary>
    [Test]
    public async ValueTask LockPinRevokesUnlockedSessionAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.LoginName = "safetylocktest";
        player.Account.VaultPassword = "4321";

        var safety = new AccountSafetyPlugIn
        {
            Configuration = new AccountSafetyConfiguration
            {
                FileName = this._statePath,
                UnlockedMinutes = 30,
            },
        };

        var check = await safety.EnterPinAsync(player, "4321", tell: false).ConfigureAwait(false);
        Assert.That(check.Result, Is.EqualTo(PinCheckResult.Ok));
        Assert.That(safety.IsEntered(player), Is.True);

        var wrongLock = await safety.LockPinAsync(player, "9999").ConfigureAwait(false);
        Assert.That(wrongLock, Is.False);
        Assert.That(safety.IsEntered(player), Is.True);

        var okLock = await safety.LockPinAsync(player, "4321").ConfigureAwait(false);
        Assert.That(okLock, Is.True);
        Assert.That(safety.IsEntered(player), Is.False);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualClock(DateTimeOffset startUtc)
        {
            this._utcNow = startUtc;
        }

        public void Advance(TimeSpan delta) => this._utcNow = this._utcNow.Add(delta);

        public override DateTimeOffset GetUtcNow() => this._utcNow;
    }
}
