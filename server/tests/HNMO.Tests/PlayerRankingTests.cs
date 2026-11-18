// <copyright file="PlayerRankingTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.Text.Json;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.PlayerRanking;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;
using Moq;

/// <summary>
/// Unit and integration tests for <see cref="PlayerRankingEngine"/>, <see cref="PlayerRankingPlugIn"/>, and <see cref="RankingChatCommandPlugIn"/>.
/// </summary>
[TestFixture]
public class PlayerRankingTests
{
    /// <summary>Verifies bulk stat allocation validation rules (valid, insufficient points, negatives, Command class gate, invalid JSON).</summary>
    [Test]
    public void EngineValidatesBulkAddStatsPlans()
    {
        var ok = PlayerRankingEngine.PlanBulkAddStats("""{"str":120,"agi":80,"vit":0,"ene":50,"cmd":0}""", 300, allowsCommand: false);
        Assert.That(ok.Valid, Is.True);
        Assert.That(ok.Strength, Is.EqualTo(120));
        Assert.That(ok.Agility, Is.EqualTo(80));
        Assert.That(ok.Energy, Is.EqualTo(50));
        Assert.That(ok.TotalPoints, Is.EqualTo(250));

        var noCmd = PlayerRankingEngine.PlanBulkAddStats("""{"cmd":10}""", 100, allowsCommand: false);
        Assert.That(noCmd.Valid, Is.False);
        Assert.That(noCmd.ErrorCode, Is.EqualTo("no_command"));

        var withCmd = PlayerRankingEngine.PlanBulkAddStats("""{"cmd":10,"ene":15}""", 100, allowsCommand: true);
        Assert.That(withCmd.Valid, Is.True);
        Assert.That(withCmd.Command, Is.EqualTo(10));
        Assert.That(withCmd.TotalPoints, Is.EqualTo(25));

        var over = PlayerRankingEngine.PlanBulkAddStats("""{"str":60,"agi":60}""", 100, allowsCommand: false);
        Assert.That(over.Valid, Is.False);
        Assert.That(over.ErrorCode, Is.EqualTo("insufficient_points"));

        var negative = PlayerRankingEngine.PlanBulkAddStats("""{"str":-5,"agi":10}""", 100, allowsCommand: false);
        Assert.That(negative.Valid, Is.False);
        Assert.That(negative.ErrorCode, Is.EqualTo("negative"));

        Assert.That(PlayerRankingEngine.PlanBulkAddStats("{bad", 100, false).ErrorCode, Is.EqualTo("invalid_json"));
    }

    /// <summary>Verifies online player ranking sorts by resets, master level, and level while excluding template players.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask EngineRanksOnlinePlayersCleanlyAsync()
    {
        var ctx = GameContextTestHelper.CreateGameContext([]);
        var p1 = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        p1.SelectedCharacter!.Name = "Beta";
        p1.Attributes![Stats.Level] = 350;
        p1.Attributes[Stats.Resets] = 3;

        var p2 = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        p2.SelectedCharacter!.Name = "Alpha";
        p2.Attributes![Stats.Level] = 200;
        p2.Attributes[Stats.Resets] = 5;

        var template = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        template.SelectedCharacter!.Name = "HiddenBot";
        template.Attributes![Stats.Resets] = 99;
        template.Account!.IsTemplate = true;

        var rows = PlayerRankingEngine.BuildOnlineRanking([p1, p2, template], 10);
        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(rows[0].Name, Is.EqualTo("Alpha"));
        Assert.That(rows[0].Resets, Is.EqualTo(5));
        Assert.That(rows[1].Name, Is.EqualTo("Beta"));
    }

    /// <summary>Verifies <see cref="PlayerRankingPlugIn"/> distributes multiple stats, notifies player, and rejects over-budget requests.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask BulkAddStatsAppliesMultiStatAllocationsAndRefusesInsufficientAsync()
    {
        var pluginCfg = new PlugInConfiguration
        {
            TypeId = typeof(PlayerRankingPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new PlayerRankingConfiguration()),
        };
        var ctx = GameContextTestHelper.CreateGameContext([pluginCfg]);
        var player = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        player.Culture = CultureInfo.GetCultureInfo("vi-VN");
        player.SelectedCharacter!.LevelUpPoints = 500;

        var strBefore = (int)player.Attributes![Stats.BaseStrength];
        var agiBefore = (int)player.Attributes[Stats.BaseAgility];
        var eneBefore = (int)player.Attributes[Stats.BaseEnergy];

        var plugIn = PlayerRankingPlugIn.Get(ctx)!;
        Assert.That(plugIn, Is.Not.Null);

        await plugIn.HandleBulkAddStatsAsync(player, """{"str":200,"agi":150,"vit":0,"ene":50,"cmd":0}""").ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.BaseStrength], Is.EqualTo(strBefore + 200));
        Assert.That((int)player.Attributes[Stats.BaseAgility], Is.EqualTo(agiBefore + 150));
        Assert.That((int)player.Attributes[Stats.BaseEnergy], Is.EqualTo(eneBefore + 50));
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(100));

        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("Còn lại: 100")), MessageType.BlueNormal), Times.Once);

        // Over remaining points (100) must refuse cleanly without modifying attributes.
        await plugIn.HandleBulkAddStatsAsync(player, """{"str":120}""").ConfigureAwait(false);
        Assert.That((int)player.Attributes[Stats.BaseStrength], Is.EqualTo(strBefore + 200));
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(100));
    }

    /// <summary>Verifies <c>/rank</c> and <see cref="PlayerRankingPlugIn.SendStateAsync"/> deliver valid ranking state payloads.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask RankingCommandAndViewPushValidSnapshotAsync()
    {
        var pluginCfg = new PlugInConfiguration
        {
            TypeId = typeof(PlayerRankingPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new PlayerRankingConfiguration { TopCount = 10 }),
        };
        var ctx = GameContextTestHelper.CreateGameContext([pluginCfg]);
        var player = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        await ctx.AddPlayerAsync(player).ConfigureAwait(false);
        player.SelectedCharacter!.Name = "RankHero";
        player.SelectedCharacter.LevelUpPoints = 42;

        var rankingView = Mock.Get(player.ViewPlugIns.GetPlugIn<IHnmoRankingViewPlugIn>()!);
        string? capturedJson = null;
        rankingView
            .Setup(v => v.ShowRankingStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((json, _) => capturedJson = json)
            .Returns(ValueTask.CompletedTask);

        await new RankingChatCommandPlugIn().HandleCommandAsync(player, "/rank").ConfigureAwait(false);

        Assert.That(capturedJson, Is.Not.Null);
        using var doc = JsonDocument.Parse(capturedJson!);
        Assert.That(doc.RootElement.GetProperty("enableRankings").GetBoolean(), Is.True);
        Assert.That(doc.RootElement.GetProperty("enableBulkAddStats").GetBoolean(), Is.True);
        Assert.That(doc.RootElement.GetProperty("myRank").GetProperty("name").GetString(), Is.EqualTo("RankHero"));
        Assert.That(doc.RootElement.GetProperty("myRank").GetProperty("levelUpPoints").GetInt32(), Is.EqualTo(42));
        Assert.That(doc.RootElement.GetProperty("online").GetArrayLength(), Is.EqualTo(1));
    }
}
