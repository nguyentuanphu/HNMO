// <copyright file="BannedWordsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Character;
using HNMO.GameLogic.PlayerActions.Chat;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Character;
using HNMO.GameLogic.Views.Guild;
using HNMO.Interfaces;
using HNMO.PlugIns;
using Moq;

/// <summary>
/// Unit and integration tests for <see cref="BannedWordsEngine"/> and <see cref="BannedWordsPlugIn"/>.
/// </summary>
[TestFixture]
public class BannedWordsTests
{
    /// <summary>Verifies EN and VI chat censoring preserves safe everyday Vietnamese words when diacritics matter.</summary>
    [Test]
    public void EngineCensorsEnglishAndVietnameseWordsWithoutFalsePositives()
    {
        var cfg = new BannedWordsConfiguration();

        var clean = BannedWordsEngine.EvaluateChat("Classic các bạn lớn lên đi chơi nhé", cfg);
        Assert.That(clean.IsMatched, Is.False);
        Assert.That(clean.FilteredText, Is.EqualTo("Classic các bạn lớn lên đi chơi nhé"));

        var mixed = BannedWordsEngine.EvaluateChat("Hello fuck con mẹ mày dkm!", cfg);
        Assert.That(mixed.IsMatched, Is.True);
        Assert.That(mixed.FilteredText, Is.EqualTo("Hello **** ********** ***!"));
    }

    /// <summary>Verifies <see cref="BannedWordsConfiguration.IgnoreVietnameseDiacritics"/> catches unaccented variants.</summary>
    [Test]
    public void EngineSupportsOptionalDiacriticFolding()
    {
        var cfg = new BannedWordsConfiguration
        {
            EnglishWords = [],
            VietnameseWords = ["chó chết"],
            IgnoreVietnameseDiacritics = true,
        };

        var result = BannedWordsEngine.EvaluateChat("Nói cho chet là bị lọc", cfg);
        Assert.That(result.IsMatched, Is.True);
        Assert.That(result.MatchedTerm, Is.EqualTo("chó chết"));
        Assert.That(result.FilteredText, Is.EqualTo("Nói ******** là bị lọc"));
    }

    /// <summary>Verifies list parsing handles newlines, commas, semicolons, comments, and deduplication.</summary>
    [Test]
    public void EngineParsesEditableWordListsCleanly()
    {
        var parsed = BannedWordsEngine.ParseWordList("# comment\n  Alpha , beta; ALPHA\r\nCụm Từ\n\n");
        Assert.That(parsed, Is.EqualTo(new[] { "Alpha", "beta", "Cụm Từ" }));
    }

    /// <summary>Verifies character and guild name evaluation catches CamelCase, digit tokens, and substrings while allowing Admin and Classic.</summary>
    [Test]
    public void EngineEvaluatesNamesWithCamelCaseAndSubstringSafety()
    {
        var cfg = new BannedWordsConfiguration();

        Assert.That(BannedWordsEngine.EvaluateName("Admin", cfg).IsMatched, Is.False);
        Assert.That(BannedWordsEngine.EvaluateName("Classic", cfg).IsMatched, Is.False);
        Assert.That(BannedWordsEngine.EvaluateName("MinhHieu", cfg).IsMatched, Is.False);

        Assert.That(BannedWordsEngine.EvaluateName("ProDkm", cfg).IsMatched, Is.True);
        Assert.That(BannedWordsEngine.EvaluateName("Dm123", cfg).IsMatched, Is.True);
        Assert.That(BannedWordsEngine.EvaluateName("PlayerFuck", cfg).IsMatched, Is.True);
        Assert.That(BannedWordsEngine.EvaluateName("du_ma_pro", new BannedWordsConfiguration { EnglishWords = [], VietnameseWords = ["du ma"] }).IsMatched, Is.True);
    }

    /// <summary>Verifies disabled plug-in leaves chat untouched and active plug-in censors normal chat and /post.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask PlugInDisabledPassesThroughAndActiveCensorsChatAsync()
    {
        // Explicitly disabled (as initialized by DataInitializationBase via IDisabledByDefault)
        var disabledCfg = new PlugInConfiguration
        {
            TypeId = typeof(BannedWordsPlugIn).GUID,
            IsActive = false,
        };
        var offCtx = GameContextTestHelper.CreateGameContext([disabledCfg]);
        var offSender = await PlayerTestHelper.CreatePlayerAsync(offCtx).ConfigureAwait(false);
        var offObserver = await PlayerTestHelper.CreatePlayerAsync(offCtx).ConfigureAwait(false);
        offSender.Observers.Add(offObserver);

        await new ChatMessageAction().ChatMessageAsync(offSender, offSender.SelectedCharacter!.Name, "fuck", false).ConfigureAwait(false);
        Mock.Get(offObserver.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync("fuck", offSender.SelectedCharacter.Name, ChatMessageType.Normal), Times.Once);

        // Active Censor mode
        var pluginCfg = CreatePlugInConfig(new BannedWordsConfiguration
        {
            ChatAction = BannedWordChatAction.Censor,
        });
        var onCtx = GameContextTestHelper.CreateGameContext([pluginCfg]);
        var sender = await PlayerTestHelper.CreatePlayerAsync(onCtx).ConfigureAwait(false);
        var observer = await PlayerTestHelper.CreatePlayerAsync(onCtx).ConfigureAwait(false);
        sender.Observers.Add(observer);

        await new ChatMessageAction().ChatMessageAsync(sender, sender.SelectedCharacter!.Name, "What the fuck vcl", false).ConfigureAwait(false);
        Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync("What the **** ***", sender.SelectedCharacter.Name, ChatMessageType.Normal), Times.Once);

        // /post command also uses BannedWordsPlugIn
        sender.SelectedCharacter.Name = "Hero";
        sender.Money = 10_000; // /post costs 1,000 zen
        await onCtx.AddPlayerAsync(observer).ConfigureAwait(false);
        var postPlugIn = new HNMO.GameLogic.PlugIns.ChatCommands.PostChatCommandPlugIn();
        await postPlugIn.HandleCommandAsync(sender, "/post sell fuck item").ConfigureAwait(false);
        Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync("Hero: sell **** item", "[POST]", ChatMessageType.Gens), Times.Once);
    }

    /// <summary>Verifies Block mode drops chat delivery, sends EN/VI warnings, and exempts GMs.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask BlockModeWarnsInPlayerLanguageAndExemptsGameMastersAsync()
    {
        var pluginCfg = CreatePlugInConfig(new BannedWordsConfiguration
        {
            ChatAction = BannedWordChatAction.Block,
            ExemptGameMasters = true,
        });
        var ctx = GameContextTestHelper.CreateGameContext([pluginCfg]);

        var viPlayer = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        viPlayer.Culture = CultureInfo.GetCultureInfo("vi-VN");
        var enPlayer = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        enPlayer.Culture = CultureInfo.GetCultureInfo("en-US");
        var gmPlayer = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        gmPlayer.SelectedCharacter!.CharacterStatus = CharacterStatus.GameMaster;

        viPlayer.Observers.Add(enPlayer);
        enPlayer.Observers.Add(viPlayer);
        gmPlayer.Observers.Add(enPlayer);

        await new ChatMessageAction().ChatMessageAsync(viPlayer, viPlayer.SelectedCharacter!.Name, "Chửi dkm", false).ConfigureAwait(false);
        Mock.Get(enPlayer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync(It.IsAny<string>(), viPlayer.SelectedCharacter.Name, ChatMessageType.Normal), Times.Never);
        Mock.Get(viPlayer.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("bị cấm")), MessageType.BlueNormal), Times.Once);

        await new ChatMessageAction().ChatMessageAsync(enPlayer, enPlayer.SelectedCharacter!.Name, "Say fuck", false).ConfigureAwait(false);
        Mock.Get(enPlayer.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("restricted words")), MessageType.BlueNormal), Times.Once);

        await new ChatMessageAction().ChatMessageAsync(gmPlayer, gmPlayer.SelectedCharacter!.Name, "Test fuck", false).ConfigureAwait(false);
        Mock.Get(enPlayer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync("Test fuck", gmPlayer.SelectedCharacter.Name, ChatMessageType.Normal), Times.Once);
    }

    /// <summary>Verifies <see cref="CreateCharacterAction"/> and <see cref="GuildCreateAction"/> reject banned names.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask RejectsBannedCharacterAndGuildNamesOnCreationAsync()
    {
        var pluginCfg = CreatePlugInConfig(new BannedWordsConfiguration());
        var ctx = GameContextTestHelper.CreateGameContext([pluginCfg]);

        var characterClass = new HNMO.Persistence.BasicModel.CharacterClass
        {
            Number = 1,
            CanGetCreated = true,
            IsActive = true,
            HomeMap = ctx.Configuration.Maps.Single(),
        };
        ctx.Configuration.CharacterClasses.Add(characterClass);
        ctx.Configuration.MaximumCharactersPerAccount = 5;

        var charCreator = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        await charCreator.SetSelectedCharacterAsync(null).ConfigureAwait(false);
        Mock.Get(charCreator.Account!).Setup(a => a.Characters).Returns(new List<Character>());
        charCreator.Culture = CultureInfo.GetCultureInfo("vi-VN");
        await charCreator.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);

        await new CreateCharacterAction().CreateCharacterAsync(charCreator, "ProFuck", characterClass.Number).ConfigureAwait(false);
        Assert.That(charCreator.Account!.Characters.Any(c => c.Name == "ProFuck"), Is.False);
        Mock.Get(charCreator.ViewPlugIns.GetPlugIn<IShowCharacterCreationFailedPlugIn>()!)
            .Verify(v => v.ShowCharacterCreationFailedAsync(), Times.Once);
        Mock.Get(charCreator.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("Tên nhân vật")), MessageType.BlueNormal), Times.Once);

        var guildCreator = await PlayerTestHelper.CreatePlayerAsync(ctx).ConfigureAwait(false);
        await guildCreator.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        guildCreator.Attributes![HNMO.GameLogic.Attributes.Stats.Level] = 100; // the guild master asks for level 100
        await new GuildCreateAction().CreateGuildAsync(guildCreator, "VclGuild", new byte[32]).ConfigureAwait(false);
        Mock.Get(guildCreator.ViewPlugIns.GetPlugIn<IShowGuildCreateResultPlugIn>()!)
            .Verify(v => v.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist), Times.Once);
        Mock.Get(guildCreator.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("restricted words")), MessageType.BlueNormal), Times.Once);
    }

    private static PlugInConfiguration CreatePlugInConfig(BannedWordsConfiguration cfg) =>
        new()
        {
            TypeId = typeof(BannedWordsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(cfg),
        };
}
