// <copyright file="ExperienceLossRowsUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// Saved experience loss settings with the earlier default rows get the rows of the Season 6/9 server suites.
/// </summary>
[TestFixture]
internal class ExperienceLossRowsUpdateTest
{
    /// <summary>The earlier default rows are replaced.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EarlierDefaultRowsAreReplacedAsync()
    {
        var (configuration, plugIn) = Create(ExperienceLossRowsUpdatePlugIn.CreateOldDefaults());

        await ApplyAsync(configuration).ConfigureAwait(false);
        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<PlayerLosesExperienceAfterDeathPlugInConfiguration>(null)!;
        double Loss(int minimumLevel, bool isMaster, HeroState state) => result.LossesPerLevel
            .Single(l => l.MinimumLevel == minimumLevel && l.IsMaster == isMaster)
            .Losses.Single(s => s.State == state).ExperienceLossPercentage;

        Assert.Multiple(() =>
        {
            Assert.That(Loss(11, false, HeroState.Normal), Is.EqualTo(3));
            Assert.That(Loss(151, false, HeroState.Normal), Is.EqualTo(2));
            Assert.That(Loss(221, false, HeroState.Normal), Is.EqualTo(1));
            Assert.That(Loss(0, true, HeroState.Normal), Is.EqualTo(1));
            Assert.That(Loss(11, false, HeroState.PlayerKillWarning), Is.EqualTo(5));
            Assert.That(Loss(151, false, HeroState.PlayerKiller1stStage), Is.EqualTo(10));
            Assert.That(Loss(0, true, HeroState.PlayerKiller2ndStage), Is.EqualTo(20));
        });
    }

    /// <summary>The settings saved by the admin panel (with "$id"/"$values" markers) are read as well.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AdminPanelSaveIsReadAsync()
    {
        var (configuration, plugIn) = Create(ExperienceLossRowsUpdatePlugIn.CreateOldDefaults());
        plugIn.CustomConfiguration = WithReferenceMarkers(plugIn.CustomConfiguration!);
        Assert.That(plugIn.CustomConfiguration, Does.Contain("$values"));

        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<PlayerLosesExperienceAfterDeathPlugInConfiguration>(null)!;
        Assert.That(result.LossesPerLevel.Select(l => l.MinimumLevel), Does.Contain(221));
    }

    /// <summary>Rows the host chose stay.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostRowsAreKeptAsync()
    {
        var saved = ExperienceLossRowsUpdatePlugIn.CreateOldDefaults();
        saved.LossesPerLevel.First().Losses.First(l => l.State == HeroState.Normal).ExperienceLossPercentage = 4;
        var (configuration, plugIn) = Create(saved);

        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<PlayerLosesExperienceAfterDeathPlugInConfiguration>(null)!;
        Assert.That(result.LossesPerLevel.Select(l => l.MinimumLevel), Is.EquivalentTo(new[] { 10, 150, 220, 0 }));
    }

    private static string WithReferenceMarkers(string json)
    {
        var id = 0;
        System.Text.Json.Nodes.JsonNode? Mark(System.Text.Json.Nodes.JsonNode? node)
        {
            switch (node)
            {
                case System.Text.Json.Nodes.JsonObject obj:
                    var result = new System.Text.Json.Nodes.JsonObject { ["$id"] = (++id).ToString(System.Globalization.CultureInfo.InvariantCulture) };
                    foreach (var (key, value) in obj)
                    {
                        result[key] = Mark(value);
                    }

                    return result;
                case System.Text.Json.Nodes.JsonArray array:
                    var list = new System.Text.Json.Nodes.JsonArray();
                    foreach (var item in array)
                    {
                        list.Add(Mark(item));
                    }

                    return new System.Text.Json.Nodes.JsonObject { ["$id"] = (++id).ToString(System.Globalization.CultureInfo.InvariantCulture), ["$values"] = list };
                default:
                    return node?.DeepClone();
            }
        }

        return Mark(System.Text.Json.Nodes.JsonNode.Parse(json))!.ToJsonString();
    }

    private static (HNMO.Persistence.BasicModel.GameConfiguration Configuration, HNMO.Persistence.BasicModel.PlugInConfiguration PlugIn) Create(PlayerLosesExperienceAfterDeathPlugInConfiguration saved)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(PlayerLosesExperienceAfterDeathPlugIn).GUID, IsActive = true };
        plugIn.SetConfiguration(saved, null);
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }

    private static ValueTask ApplyAsync(HNMO.Persistence.BasicModel.GameConfiguration configuration)
        => new ExperienceLossRowsUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration);
}
