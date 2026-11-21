// <copyright file="GuildCreateLevelUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// The update which sets the guild creation level to 150 replaces only the earlier default (0: any level).
/// </summary>
[TestFixture]
internal class GuildCreateLevelUpdateTest
{
    /// <summary>The earlier default becomes 150, the member cap stays, and applying it twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EarlierDefaultBecomes150Async()
    {
        var (configuration, plugIn) = CreateWith("""{"$id":"1","MinimumCreateLevel":0,"MaximumMembers":60}""");

        await ApplyAsync(configuration).ConfigureAwait(false);
        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<GuildLimitsConfiguration>(null)!;
        Assert.That(result.MinimumCreateLevel, Is.EqualTo(150));
        Assert.That(result.MaximumMembers, Is.EqualTo(60));
    }

    /// <summary>A level the host chose stays as it is.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostLevelStaysAsync()
    {
        var (configuration, plugIn) = CreateWith("""{"MinimumCreateLevel":120,"MaximumMembers":80}""");

        await ApplyAsync(configuration).ConfigureAwait(false);

        Assert.That(plugIn.GetConfiguration<GuildLimitsConfiguration>(null)!.MinimumCreateLevel, Is.EqualTo(120));
    }

    /// <summary>New settings default to the level of the Season 6/9 server suites.</summary>
    [Test]
    public void DefaultIs150()
    {
        Assert.That(new GuildLimitsConfiguration().MinimumCreateLevel, Is.EqualTo(150));
    }

    private static async Task ApplyAsync(GameConfiguration configuration)
    {
        await new GuildCreateLevelUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);
    }

    private static (GameConfiguration Configuration, PlugInConfiguration PlugIn) CreateWith(string savedSettings)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(GuildLimitsPlugIn).GUID, IsActive = true, CustomConfiguration = savedSettings };
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }
}
