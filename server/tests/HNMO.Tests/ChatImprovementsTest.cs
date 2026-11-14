// <copyright file="ChatImprovementsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Runtime.InteropServices;
using Moq;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Chat;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Chat;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;

/// <summary>
/// Tests for the offline whispers, the spam guard and the chat log.
/// </summary>
[TestFixture]
public class ChatImprovementsTest
{
    private static readonly DateTime Start = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The store keeps at most the maximum per receiver, and hands out the whispers oldest first, only once.
    /// </summary>
    [Test]
    public void StoreKeepsLimitPerReceiverAndTakesOnce()
    {
        var store = new OfflineWhisperStore { MaximumPerReceiver = 3 };
        for (var i = 0; i < 3; i++)
        {
            Assert.That(store.Add("Alice", "Bob", "hi " + i, Start.AddMinutes(i)), Is.EqualTo(OfflineWhisperStoreResult.Stored));
        }

        Assert.That(store.Add("Alice", "Bob", "too many", Start.AddMinutes(5)), Is.EqualTo(OfflineWhisperStoreResult.ReceiverFull));
        Assert.That(store.Add("Alice", "Carl", "other receiver", Start.AddMinutes(5)), Is.EqualTo(OfflineWhisperStoreResult.Stored));
        Assert.That(store.Count("BOB", Start.AddMinutes(6)), Is.EqualTo(3), "Names are case insensitive.");

        var taken = store.Take("bob", Start.AddMinutes(6));
        Assert.That(taken.Select(w => w.Text), Is.EqualTo(new[] { "hi 0", "hi 1", "hi 2" }));
        Assert.That(taken.All(w => w.Sender == "Alice"), Is.True);
        Assert.That(store.Take("Bob", Start.AddMinutes(7)), Is.Empty);
        Assert.That(store.Count("Carl", Start.AddMinutes(7)), Is.EqualTo(1));
    }

    /// <summary>
    /// Whispers older than the maximum age are dropped, and don't count for the limit.
    /// </summary>
    [Test]
    public void StoreDropsOldWhispers()
    {
        var store = new OfflineWhisperStore { MaximumPerReceiver = 1, MaximumAge = TimeSpan.FromDays(7) };
        store.Add("Alice", "Bob", "old", Start);
        Assert.That(store.Add("Alice", "Bob", "blocked", Start.AddDays(6)), Is.EqualTo(OfflineWhisperStoreResult.ReceiverFull));
        Assert.That(store.Count("Bob", Start.AddDays(8)), Is.EqualTo(0));
        Assert.That(store.Add("Alice", "Bob", "new", Start.AddDays(8)), Is.EqualTo(OfflineWhisperStoreResult.Stored));
        Assert.That(store.Take("Bob", Start.AddDays(8)).Select(w => w.Text), Is.EqualTo(new[] { "new" }));
    }

    /// <summary>
    /// With a file, the whispers outlast a new store (a restart of the server).
    /// </summary>
    [Test]
    public void StoreKeepsWhispersInFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hnmo-offline-whispers-{Guid.NewGuid():N}.json");
        try
        {
            new OfflineWhisperStore(path).Add("Alice", "Bob", "see you", Start);
            var reloaded = new OfflineWhisperStore(path);
            Assert.That(reloaded.Take("Bob", Start.AddHours(1)).Select(w => (w.Sender, w.Text)), Is.EqualTo(new[] { ("Alice", "see you") }));
            Assert.That(new OfflineWhisperStore(path).Count("Bob", Start.AddHours(1)), Is.EqualTo(0), "Taken whispers are removed from the file.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The same text more than 3 times within 10 seconds is dropped; after the 10 seconds it's fine again.
    /// </summary>
    [Test]
    public void SpamGuardLimitsRepeats()
    {
        var guard = new ChatSpamGuard();
        var limits = ChatSpamLimits.Default;
        Assert.That(guard.TryAccept("buy items", Start, limits), Is.True);
        Assert.That(guard.TryAccept("buy items", Start.AddSeconds(1), limits), Is.True);
        Assert.That(guard.TryAccept("BUY ITEMS ", Start.AddSeconds(2), limits), Is.True);
        Assert.That(guard.TryAccept("buy items", Start.AddSeconds(3), limits), Is.False);
        Assert.That(guard.TryAccept("something else", Start.AddSeconds(3), limits), Is.True);
        Assert.That(guard.TryAccept("buy items", Start.AddSeconds(10.5), limits), Is.True);
    }

    /// <summary>
    /// More than 8 lines within 5 seconds are dropped.
    /// </summary>
    [Test]
    public void SpamGuardLimitsLines()
    {
        var guard = new ChatSpamGuard();
        var limits = ChatSpamLimits.Default;
        for (var i = 0; i < 8; i++)
        {
            Assert.That(guard.TryAccept("line " + i, Start.AddMilliseconds(100 * i), limits), Is.True, $"line {i}");
        }

        Assert.That(guard.TryAccept("line 8", Start.AddSeconds(1), limits), Is.False);
        Assert.That(guard.TryAccept("line 9", Start.AddSeconds(5.5), limits), Is.True);
    }

    /// <summary>
    /// Through the chat, the 4th same line is dropped and the sender is warned; game masters aren't checked.
    /// </summary>
    [Test]
    public async Task SpamGuardDropsChatLinesButNotOfGameMastersAsync()
    {
        var (sender, recorder) = await CreateSenderAsync().ConfigureAwait(false);
        EnsureSpamGuard((GameContext)sender.GameContext);
        for (var i = 0; i < 5; i++)
        {
            await new ChatMessageAction().ChatMessageAsync(sender, sender.Name, "Hello", false).ConfigureAwait(false);
        }

        Assert.That(recorder.Messages.Count(m => m.Type == ChatMessageType.Normal), Is.EqualTo(3));
        Mock.Get(sender.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!).Verify(v => v.ShowMessageAsync(It.Is<string>(t => t.Contains("spam")), It.IsAny<MessageType>()), Times.Exactly(2));

        recorder.Messages.Clear();
        sender.SelectedCharacter!.CharacterStatus = CharacterStatus.GameMaster;
        for (var i = 0; i < 5; i++)
        {
            await new ChatMessageAction().ChatMessageAsync(sender, sender.Name, "Hello", false).ConfigureAwait(false);
        }

        Assert.That(recorder.Messages.Count(m => m.Type == ChatMessageType.Normal), Is.EqualTo(5));
    }

    /// <summary>
    /// A whisper to an existing character which isn't in the game is kept, the sender is told so, and
    /// the receiver gets it marked "(offline)" when it enters the game. The chat log has the line.
    /// </summary>
    [Test]
    public async Task OfflineWhisperIsDeliveredWhenReceiverEntersAsync()
    {
        var (sender, _) = await CreateSenderAsync().ConfigureAwait(false);
        var gameContext = (GameContext)sender.GameContext;
        var plugIn = EnsureOfflineWhispers(gameContext);
        EnsureChatLog(gameContext);
        var receiverName = "Off" + Guid.NewGuid().ToString("N")[..8];
        using (var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration))
        {
            var account = context.CreateNew<Account>();
            account.LoginName = receiverName;
            var character = context.CreateNew<Character>();
            character.Name = receiverName;
            account.Characters.Add(character);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        await new ChatMessageAction().ChatMessageAsync(sender, receiverName, "See you later", true).ConfigureAwait(false);

        Mock.Get(sender.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!).Verify(v => v.ShowMessageAsync(It.Is<string>(t => t.Contains(receiverName) && t.Contains("offline")), It.IsAny<MessageType>()), Times.Once);
        Assert.That(ChatLog.Shared.Find(receiverName).Select(e => (e.Sender, e.Kind, e.Text)), Is.EqualTo(new[] { (sender.Name, ChatMessageType.Whisper, "See you later") }));

        var receiver = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        receiver.SelectedCharacter!.Name = receiverName;
        await plugIn.DeliverAsync(receiver, receiver.SelectedCharacter).ConfigureAwait(false);

        var view = Mock.Get(receiver.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!);
        view.Verify(v => v.ChatMessageAsync("(offline) See you later", sender.Name, ChatMessageType.Whisper), Times.Once);
        Assert.That(plugIn.GetStore().Count(receiverName, DateTime.UtcNow), Is.EqualTo(0));
    }

    /// <summary>
    /// A whisper to a character which doesn't exist isn't kept.
    /// </summary>
    [Test]
    public async Task WhisperToUnknownCharacterIsNotKeptAsync()
    {
        var (sender, _) = await CreateSenderAsync().ConfigureAwait(false);
        var plugIn = EnsureOfflineWhispers((GameContext)sender.GameContext);
        var name = "Nobody" + Guid.NewGuid().ToString("N")[..6];

        await new ChatMessageAction().ChatMessageAsync(sender, name, "Hello?", true).ConfigureAwait(false);

        Assert.That(plugIn.GetStore().Count(name, DateTime.UtcNow), Is.EqualTo(0));
    }

    /// <summary>
    /// A whisper reaches a character which is on another game server of the same process.
    /// </summary>
    [Test]
    public async Task WhisperReachesCharacterOfOtherGameContextAsync()
    {
        var (sender, _) = await CreateSenderAsync().ConfigureAwait(false);
        var otherPlayer = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var name = "Far" + Guid.NewGuid().ToString("N")[..8];
        otherPlayer.SelectedCharacter!.Name = name;
        OnlineCharacters.Add(name, otherPlayer);
        try
        {
            await new ChatMessageAction().ChatMessageAsync(sender, name, "Over there?", true).ConfigureAwait(false);
            Mock.Get(otherPlayer.ViewPlugIns.GetPlugIn<IChatViewPlugIn>()!).Verify(v => v.ChatMessageAsync("Over there?", sender.Name, ChatMessageType.Whisper), Times.Once);
        }
        finally
        {
            OnlineCharacters.Remove(name, otherPlayer);
        }
    }

    /// <summary>
    /// The chat log keeps the newest lines up to its capacity, newest first, and finds sender and text.
    /// </summary>
    [Test]
    public void ChatLogKeepsNewestLines()
    {
        var log = new ChatLog(3);
        for (var i = 0; i < 5; i++)
        {
            log.Add(Start.AddSeconds(i), 1, "Lorencia", i % 2 == 0 ? "Alice" : "Bob", Guid.Empty, ChatMessageType.Normal, null, "text " + i);
        }

        Assert.That(log.Find(null).Select(e => e.Text), Is.EqualTo(new[] { "text 4", "text 3", "text 2" }));
        Assert.That(log.Find("alice").Select(e => e.Text), Is.EqualTo(new[] { "text 4", "text 2" }));
        Assert.That(log.Find("TEXT 3").Select(e => e.Sender), Is.EqualTo(new[] { "Bob" }));
        Assert.That(log.LastId, Is.EqualTo(5));
    }

    /// <summary>
    /// Muting a character in the game sets the chat ban of its account, so that its next line isn't delivered.
    /// </summary>
    [Test]
    public async Task MuteBansOnlineCharacterAsync()
    {
        var (sender, recorder) = await CreateSenderAsync().ConfigureAwait(false);
        OnlineCharacters.Add(sender.Name, sender);
        try
        {
            var until = await ChatMute.MuteAsync(sender.GameContext.PersistenceContextProvider, sender.GameContext.Configuration, sender.Name, TimeSpan.FromMinutes(10)).ConfigureAwait(false);
            Assert.That(until, Is.Not.Null);
            Assert.That(sender.Account!.ChatBanUntil, Is.EqualTo(until));

            await new ChatMessageAction().ChatMessageAsync(sender, sender.Name, "Can you hear me?", false).ConfigureAwait(false);
            Assert.That(recorder.Messages, Is.Empty);
        }
        finally
        {
            OnlineCharacters.Remove(sender.Name, sender);
        }
    }

    private static OfflineWhisperPlugIn EnsureOfflineWhispers(GameContext gameContext)
    {
        var plugIn = OfflineWhisperPlugIn.Get(gameContext);
        if (plugIn is null)
        {
            plugIn = new OfflineWhisperPlugIn();
            gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(plugIn);
        }

        // In memory only, so that the test doesn't leave a file behind.
        plugIn.Configuration = new OfflineWhisperConfiguration { FileName = string.Empty };
        return plugIn;
    }

    private static void EnsureSpamGuard(GameContext gameContext)
    {
        if (gameContext.FeaturePlugIns.GetPlugIn<ChatSpamGuardPlugIn>() is null)
        {
            gameContext.FeaturePlugIns.AddPlugIn(new ChatSpamGuardPlugIn(), true);
        }
    }

    private static void EnsureChatLog(GameContext gameContext)
    {
        if (!gameContext.PlugInManager.GetActivePlugInsOf<IChatMessageSentPlugIn>().OfType<ChatLogPlugIn>().Any())
        {
            gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IChatMessageSentPlugIn>(new ChatLogPlugIn());
        }
    }

    private static async ValueTask<(Player Sender, RecordingPlugIn Recorder)> CreateSenderAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var gameContext = (GameContext)player.GameContext;
        var sender = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        sender.SelectedCharacter!.Name = "Snd" + Guid.NewGuid().ToString("N")[..8];
        await gameContext.AddPlayerAsync(sender).ConfigureAwait(false);
        gameContext.PlayersByCharacterName.TryAdd(sender.Name, sender);
        var recorder = new RecordingPlugIn();
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IChatMessageSentPlugIn>(recorder);
        return (sender, recorder);
    }

    [Guid("0B7C55E1-4D2A-4F3E-9A61-7D28C3E4B915")]
    private sealed class RecordingPlugIn : IChatMessageSentPlugIn
    {
        public List<(string Message, ChatMessageType Type)> Messages { get; } = new();

        public ValueTask ChatMessageSentAsync(Player sender, string message, ChatMessageType messageType, Player? receiver)
        {
            this.Messages.Add((message, messageType));
            return ValueTask.CompletedTask;
        }
    }
}
