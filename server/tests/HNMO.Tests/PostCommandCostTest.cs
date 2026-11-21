// <copyright file="PostCommandCostTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;

/// <summary>
/// /post costs 1,000 zen and needs 5 seconds between two posts (Season 6/9 server suites).
/// </summary>
[TestFixture]
public class PostCommandCostTest
{
    /// <summary>A post takes 1,000 zen; a second post at once is refused and costs nothing.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask PostCostsZenAndWaitsAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var sender = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var observer = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        await context.AddPlayerAsync(observer).ConfigureAwait(false);
        sender.SelectedCharacter!.Name = "Hero";
        sender.Money = 5_000;
        var plugIn = new PostChatCommandPlugIn();

        await plugIn.HandleCommandAsync(sender, "/post hello").ConfigureAwait(false);
        await plugIn.HandleCommandAsync(sender, "/post again").ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(sender.Money, Is.EqualTo(4_000));
            Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
                .Verify(v => v.ChatMessageAsync("Hero: hello", "[POST]", ChatMessageType.Gens), Times.Once);
            Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
                .Verify(v => v.ChatMessageAsync("Hero: again", "[POST]", ChatMessageType.Gens), Times.Never);
        });
    }

    /// <summary>Without 1,000 zen nothing is posted.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask PostWithoutZenIsRefusedAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var sender = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var observer = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        await context.AddPlayerAsync(observer).ConfigureAwait(false);
        sender.Money = 999;

        await new PostChatCommandPlugIn().HandleCommandAsync(sender, "/post hello").ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(sender.Money, Is.EqualTo(999));
            Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
                .Verify(v => v.ChatMessageAsync(It.IsAny<string>(), "[POST]", It.IsAny<ChatMessageType>()), Times.Never);
        });
    }

    /// <summary>The host can make /post free and without a wait.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask HostCanTurnCostAndWaitOffAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var sender = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var observer = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        await context.AddPlayerAsync(observer).ConfigureAwait(false);
        sender.SelectedCharacter!.Name = "Hero";
        var plugIn = new PostChatCommandPlugIn { Configuration = new PostChatCommandConfiguration { ZenCost = 0, CooldownSeconds = 0 } };

        await plugIn.HandleCommandAsync(sender, "/post one").ConfigureAwait(false);
        await plugIn.HandleCommandAsync(sender, "/post two").ConfigureAwait(false);

        Mock.Get(observer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!)
            .Verify(v => v.ChatMessageAsync(It.IsAny<string>(), "[POST]", ChatMessageType.Gens), Times.Exactly(2));
    }
}
