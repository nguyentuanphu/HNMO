// <copyright file="CrossSeasonFriendsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Chat;
using HNMO.Persistence;

/// <summary>Friends across seasons: the friend list's names for the other programs, and a whisper sent to one.</summary>
[TestFixture]
public sealed class CrossSeasonFriendsTest
{
    private IArenaShared? _previous;

    /// <summary>Keeps the shared database of the process.</summary>
    [SetUp]
    public void SetUp() => this._previous = ArenaShared.Current;

    /// <summary>Puts it back.</summary>
    [TearDown]
    public void TearDown() => ArenaShared.Current = this._previous;

    /// <summary>Each program has its own "server" in the friend list and its own name.</summary>
    [Test]
    public void ProgramsHaveTheirOwnServerAndName()
    {
        Assert.That(CrossSeasonFriendsPlugIn.ServerOf("s6"), Is.EqualTo(0xE0));
        Assert.That(CrossSeasonFriendsPlugIn.ServerOf("s9"), Is.EqualTo(0xE1));
        Assert.That(CrossSeasonFriendsPlugIn.ServerOf("arena"), Is.EqualTo(0xE2));
        Assert.That(CrossSeasonFriendsPlugIn.NameOf("arena"), Is.EqualTo("Arena"));
        Assert.That(CrossSeasonFriendsPlugIn.ServerOf("s9"), Is.LessThan(0xFC), "below the web game's offline values");
    }

    /// <summary>A whisper to a character playing in another program goes there, by that character's name.</summary>
    [Test]
    public async Task AWhisperGoesToTheOtherProgramAsync()
    {
        var shared = new FakeShared();
        ArenaShared.Current = shared;
        var context = GameContextTestHelper.CreateGameContext();
        var sender = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        sender.SelectedCharacter!.Name = "HomeHero";
        CrossSeasonFriendsPlugIn.ForgetElsewhere();
        Assert.That(await CrossSeasonFriendsPlugIn.TrySendAsync(sender, "ArenaHero", "hi").ConfigureAwait(false), Is.True);
        Assert.That(shared.Sent, Is.EqualTo(new[] { ("HomeHero", "arena", "ArenaHero", "hi") }));
    }

    private sealed class FakeShared : IArenaShared
    {
        public List<(string From, string ToSeason, string To, string Text)> Sent { get; } = [];

        public int AddPoints(string login, int points, int dailyCap, string dayKey) => points;

        public bool TrySpendPoints(string login, int points) => false;

        public void RefundPoints(string login, int points)
        {
        }

        public int GetPoints(string login) => 0;

        public void SetRating(string login, int rating)
        {
        }

        public int? GetRating(string login) => null;

        public IReadOnlyList<OnlinePresence> OnlineElsewhere(string season) => [new("friend", "ArenaHero", "arena", 0)];

        public void SendCrossWhisper(string from, string fromSeason, string toSeason, string toCharacter, string text) => this.Sent.Add((from, toSeason, toCharacter, text));
    }
}
