// <copyright file="PostCommandMuteTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

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
}
