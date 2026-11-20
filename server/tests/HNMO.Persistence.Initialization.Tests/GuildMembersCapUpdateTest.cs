// <copyright file="GuildMembersCapUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// The update which caps a guild at 80 members replaces only the earlier default (0: no cap).
/// </summary>
[TestFixture]
internal class GuildMembersCapUpdateTest
{
    /// <summary>The earlier default becomes 80, the creation level stays, and applying it twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EarlierDefaultBecomes80Async()
    {
        var (configuration, plugIn) = CreateWith("""{"$id":"1","MinimumCreateLevel":100,"MaximumMembers":0}""");

        await ApplyAsync(configuration).ConfigureAwait(false);
        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<GuildLimitsConfiguration>(null)!;
        Assert.That(result.MaximumMembers, Is.EqualTo(80));
        Assert.That(result.MinimumCreateLevel, Is.EqualTo(100));
    }

    /// <summary>A cap the host chose stays as it is.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostCapStaysAsync()
    {
        var (configuration, plugIn) = CreateWith("""{"MinimumCreateLevel":0,"MaximumMembers":50}""");

        await ApplyAsync(configuration).ConfigureAwait(false);

        Assert.That(plugIn.GetConfiguration<GuildLimitsConfiguration>(null)!.MaximumMembers, Is.EqualTo(50));
    }

    private static async Task ApplyAsync(GameConfiguration configuration)
    {
        await new GuildMembersCapUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);
    }

    private static (GameConfiguration Configuration, PlugInConfiguration PlugIn) CreateWith(string savedSettings)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(GuildLimitsPlugIn).GUID, IsActive = true, CustomConfiguration = savedSettings };
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }
}
