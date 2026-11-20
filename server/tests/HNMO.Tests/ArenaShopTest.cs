// <copyright file="ArenaShopTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.Arena;
using HNMO.GameLogic.PlugIns.CustomRanks;
using HNMO.Persistence;

/// <summary>
/// The HNMO Arena points shop on the home servers, and the arena titles of the custom ranks.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ArenaShopTest
{
    private IArenaShared? _previous;

    /// <summary>Keeps the program's shared Arena data.</summary>
    [SetUp]
    public void SetUp() => this._previous = ArenaShared.Current;

    /// <summary>Puts it back.</summary>
    [TearDown]
    public void TearDown() => ArenaShared.Current = this._previous;

    /// <summary>
    /// Buying takes the points and puts a new item in the bag; without enough points nothing changes; the daily cap
    /// of the stand-in holds the earning.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task BuyingTakesThePointsAndGivesTheItemAsync()
    {
        var shared = new FakeShared();
        ArenaShared.Current = shared;
        var context = GameContextTestHelper.CreateGameContext([]);
        context.Configuration.Items.Add(new HNMO.Persistence.BasicModel.ItemDefinition { Group = 14, Number = 13, Name = "Jewel of Bless", Width = 1, Height = 1, Durability = 1 });
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "buyer" };
        var shop = new ArenaShopPlugIn();
        var bless = shop.Configuration?.Items ?? ArenaShopConfiguration.DefaultItems();
        var index = bless.FindIndex(i => i.Group == 14 && i.Number == 13);

        var (refused, _) = await shop.BuyAsync(player, index).ConfigureAwait(false);
        Assert.That(refused, Does.Contain("costs 50"));
        Assert.That(player.Inventory!.Items, Is.Empty);

        Assert.That(shared.AddPoints("buyer", 80, 0, "2026-11-20"), Is.EqualTo(80));
        var (bought, _) = await shop.BuyAsync(player, index).ConfigureAwait(false);
        Assert.That(bought, Does.StartWith("Bought Jewel of Bless"));
        Assert.That(shared.GetPoints("buyer"), Is.EqualTo(30));
        Assert.That(player.Inventory.Items.Single().Definition!.Number, Is.EqualTo(13));

        Assert.That(shop.StateOf(player), Is.Not.Null);
        Assert.That((await shop.BuyAsync(player, 999).ConfigureAwait(false)).En, Does.Contain("isn't in the shop"));
    }

    /// <summary>A ranked duel's result gives the winner 5 points and the loser 2, and keeps both ratings for the titles.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ADuelResultGivesPointsAndRatingsAsync()
    {
        var shared = new FakeShared();
        ArenaShared.Current = shared;
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hnmo-arena-{Guid.NewGuid():N}.json");
        try
        {
            var context = GameContextTestHelper.CreateGameContext([]);
            var winner = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            var loser = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            winner.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "winner" };
            loser.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "loser" };

            await new ArenaDuels(file, new ArenaConfiguration()).RecordAsync(winner, loser).ConfigureAwait(false);

            Assert.That((shared.GetPoints("winner"), shared.GetPoints("loser")), Is.EqualTo((5, 2)));
            Assert.That((shared.GetRating("winner"), shared.GetRating("loser")), Is.EqualTo(((int?)1016, (int?)984)));
        }
        finally
        {
            System.IO.File.Delete(file);
        }
    }

    /// <summary>The arena titles follow the rating: 1300 is Silver and Gold, not Platinum; no ranked duel, no arena title.</summary>
    [Test]
    public void ArenaTitlesFollowTheRating()
    {
        var tiers = new CustomRankConfiguration().Tiers.Where(t => t.Id.StartsWith("arena-", StringComparison.Ordinal)).ToList();
        Assert.That(tiers.Select(t => t.Id), Is.EqualTo(new[] { "arena-bronze", "arena-silver", "arena-gold", "arena-platinum", "arena-diamond" }));
        var rated = new CustomRankSubject("Hero", 1, 0, 0, 0, 0, 1300);
        var unrated = rated with { ArenaRating = 0 };
        Assert.That(tiers.Where(t => CustomRankEngine.Qualifies(t, rated, null)).Select(t => t.Id), Is.EqualTo(new[] { "arena-bronze", "arena-silver", "arena-gold" }));
        Assert.That(tiers.Any(t => CustomRankEngine.Qualifies(t, unrated, null)), Is.False);
    }

    private sealed class FakeShared : IArenaShared
    {
        private readonly Dictionary<string, int> _points = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ratings = new(StringComparer.OrdinalIgnoreCase);

        public int AddPoints(string login, int points, int dailyCap, string dayKey)
        {
            this._points[login] = this.GetPoints(login) + points;
            return points;
        }

        public bool TrySpendPoints(string login, int points)
        {
            if (this.GetPoints(login) < points)
            {
                return false;
            }

            this._points[login] -= points;
            return true;
        }

        public void RefundPoints(string login, int points) => this._points[login] = this.GetPoints(login) + points;

        public int GetPoints(string login) => this._points.TryGetValue(login, out var p) ? p : 0;

        public void SetRating(string login, int rating) => this._ratings[login] = rating;

        public int? GetRating(string login) => this._ratings.TryGetValue(login, out var r) ? r : null;
    }
}
