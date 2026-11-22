// <copyright file="PostCommandMuteTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Chat;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using Moq;

/// <summary>
/// A muted player can't send /post to the whole server: the command went around the chat ban.
/// </summary>
[TestFixture]
public class PostCommandMuteTest
{
    /// <summary>A player with a chat ban posts nothing and is told why.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask MutedPlayerCantPostAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var sender = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var observer = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        await context.AddPlayerAsync(observer).ConfigureAwait(false);
        sender.SelectedCharacter!.Name = "Muted";
        sender.Account!.ChatBanUntil = DateTime.UtcNow.AddHours(1);

        await new PostChatCommandPlugIn().HandleCommandAsync(sender, "/post buy my items").ConfigureAwait(false);

        Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync(It.IsAny<string>(), "[POST]", It.IsAny<ChatMessageType>()), Times.Never);
        Mock.Get(sender.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(t => t.Contains("muted")), It.IsAny<MessageType>()), Times.Once);
    }

    /// <summary>A player without a chat ban still posts.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask PlayerWithoutBanPostsAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var sender = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var observer = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        await context.AddPlayerAsync(observer).ConfigureAwait(false);
        sender.SelectedCharacter!.Name = "Hero";
        sender.Money = 5_000; // a post costs 1,000 zen (#194)

        await new PostChatCommandPlugIn().HandleCommandAsync(sender, "/post hello all").ConfigureAwait(false);

        Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync("Hero: hello all", "[POST]", ChatMessageType.Gens), Times.Once);
    }

    /// <summary>A /post line is recorded in the admin panel's chat log when ChatLogPlugIn is active.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask PostIsRecordedInChatLogAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        if (!context.PlugInManager.GetActivePlugInsOf<IChatMessageSentPlugIn>().OfType<ChatLogPlugIn>().Any())
        {
            context.PlugInManager.RegisterPlugInAtPlugInPoint<IChatMessageSentPlugIn>(new ChatLogPlugIn());
        }

        var sender = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        sender.SelectedCharacter!.Name = "PostHero";
        sender.Money = 5_000;
        var token = $"post-log-{Guid.NewGuid():N}";

        await new PostChatCommandPlugIn().HandleCommandAsync(sender, $"/post {token}").ConfigureAwait(false);

        var logged = ChatLog.Shared.Find(token);
        Assert.That(logged, Has.Count.EqualTo(1));
        Assert.That(logged[0].Sender, Is.EqualTo("PostHero"));
    }
}
