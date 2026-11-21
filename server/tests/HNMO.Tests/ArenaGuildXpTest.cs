// <copyright file="ArenaGuildXpTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.Arena;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Guild experience for the guild war: owed by the Arena, taken once by Season 6.3, and the week's champion.</summary>
[TestFixture]
public sealed class ArenaGuildXpTest
{
    private IArenaShared? _previous;

    /// <summary>Keeps the shared database of the process.</summary>
    [SetUp]
    public void SetUp() => this._previous = ArenaShared.Current;

    /// <summary>Puts it back.</summary>
    [TearDown]
    public void TearDown() => ArenaShared.Current = this._previous;

    /// <summary>The champion of last week gets its guild experience once, at the first tick of the new week.</summary>
    [Test]
    public void TheChampionIsPaidOnce()
    {
        var shared = new FakeShared();
        ArenaShared.Current = shared;
        var store = ArenaStore.Open(null);
        var now = new DateTime(2026, 11, 23, 2, 0, 0, DateTimeKind.Utc); // Monday 13:00 Sydney
        var lastWeek = ArenaRanking.WeekKey(now.AddDays(-7));
        var id = Guid.NewGuid();
        store.Mutate(d =>
        {
            d.Guilds["Red"] = new ArenaGuildRecord { Name = "Red", GuildId = id, Week = lastWeek, WeekWins = 2 };
            d.Guilds["Blue"] = new ArenaGuildRecord { Name = "Blue", GuildId = Guid.NewGuid(), Week = lastWeek, WeekWins = 1 };
            d.ChampionPaidWeek = string.Empty;
            return (true, 0);
        });
        var war = new ArenaGuildWar(new ArenaConfiguration(), store);
        war.PayChampion(now, NullLogger.Instance);
        war.PayChampion(now.AddMinutes(1), NullLogger.Instance);
        Assert.That(shared.Owed, Has.Count.EqualTo(1));
        Assert.That(shared.Owed[0], Is.EqualTo(($"guildwar-champion:{lastWeek}", id, "Red", 1000)));
    }

    /// <summary>Season 6.3 credits owed guild experience to the guild, without a member's daily cap.</summary>
    [Test]
    public void SeasonSixTakesTheOwedExperience()
    {
        var shared = new FakeShared();
        ArenaShared.Current = shared;
        var id = Guid.NewGuid();
        shared.OweGuildXp("guildwar:x:1", id, "Red", 500, "guild war won");
        shared.OweGuildXp("guildwar:x:1", id, "Red", 500, "guild war won"); // the same reference: once
        var engine = new GuildEngine(new GuildsConfiguration { DailyCapPerMember = 100 }, new GuildStore());
        new GuildsPlugIn().TakeArenaGuildXp(engine, DateTime.UtcNow, NullLogger.Instance);
        new GuildsPlugIn().TakeArenaGuildXp(engine, DateTime.UtcNow, NullLogger.Instance);
        Assert.That(engine.Progress(id, DateTime.UtcNow)?.Xp, Is.EqualTo(500));
    }

    private sealed class FakeShared : IArenaShared
    {
        private readonly Dictionary<string, (Guid Id, string Name, int Xp, string Reason, bool Taken)> _rows = [];

        public List<(string Reference, Guid Id, string Name, int Xp)> Owed { get; } = [];

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

        public void OweGuildXp(string reference, Guid guildId, string guildName, int xp, string reason)
        {
            if (this._rows.TryAdd(reference, (guildId, guildName, xp, reason, false)))
            {
                this.Owed.Add((reference, guildId, guildName, xp));
            }
        }

        public IReadOnlyList<(string Reference, Guid GuildId, string GuildName, int Xp, string Reason)> ClaimGuildXp(int max)
        {
            var taken = this._rows.Where(r => !r.Value.Taken).Take(max).ToList();
            foreach (var (key, value) in taken)
            {
                this._rows[key] = value with { Taken = true };
            }

            return taken.Select(r => (r.Key, r.Value.Id, r.Value.Name, r.Value.Xp, r.Value.Reason)).ToList();
        }
    }
}
