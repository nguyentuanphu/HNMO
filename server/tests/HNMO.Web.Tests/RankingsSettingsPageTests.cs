// <copyright file="RankingsSettingsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.PlayerRanking;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="RankingsSettings"/> and <see cref="PlayerRankingPlugIn"/>.
/// </summary>
[TestFixture]
public class RankingsSettingsPageTests
{
    /// <summary>Verifies <see cref="PlayerRankingConfiguration"/> round-trips cleanly via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task RoundTripsPlayerRankingConfigurationAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(PlayerRankingPlugIn).GUID;
            row.IsActive = true;
            row.CustomConfiguration = JsonSerializer.Serialize(new PlayerRankingConfiguration
            {
                EnableRankings = true,
                EnableBulkAddStats = false,
                TopCount = 35,
                CacheSeconds = 45,
                ShowOnlineLeaderboard = true,
                ShowGuildRankings = false,
                ShowEventRankings = true,
                ShowOnlineStatus = true,
            });
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<PlayerRankingConfiguration>(
            persistence,
            typeof(PlayerRankingPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(isActive, Is.True);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.EnableRankings, Is.True);
        Assert.That(saved.EnableBulkAddStats, Is.False);
        Assert.That(saved.TopCount, Is.EqualTo(35));
        Assert.That(saved.CacheSeconds, Is.EqualTo(45));
        Assert.That(saved.ShowGuildRankings, Is.False);
    }
}
