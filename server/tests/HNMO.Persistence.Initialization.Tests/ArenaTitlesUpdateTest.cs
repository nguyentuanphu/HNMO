// <copyright file="ArenaTitlesUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.GameLogic.PlugIns.CustomRanks;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// The update which adds the Arena titles to saved custom rank settings adds the missing ones once.
/// </summary>
[TestFixture]
internal class ArenaTitlesUpdateTest
{
    /// <summary>Saved settings without the titles get all five; a title the host already has keeps its settings.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task MissingTitlesAreAddedOnceAsync()
    {
        var saved = new CustomRankConfiguration
        {
            Tiers = [new CustomRankTierConfig { Id = "veteran", MinLevel = 400 }, new CustomRankTierConfig { Id = "arena-gold", MinArenaRating = 1300 }],
        };
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(CustomRanksPlugIn).GUID, IsActive = true };
        plugIn.SetConfiguration(saved, null);
        configuration.PlugInConfigurations.Add(plugIn);

        await new ArenaTitlesUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);
        await new ArenaTitlesUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        var tiers = plugIn.GetConfiguration<CustomRankConfiguration>(null)!.Tiers;
        Assert.That(tiers.Select(t => t.Id), Is.EquivalentTo(new[] { "veteran", "arena-gold", "arena-bronze", "arena-silver", "arena-platinum", "arena-diamond" }));
        Assert.That(tiers.Single(t => t.Id == "arena-gold").MinArenaRating, Is.EqualTo(1300), "the host's own setting stays");
    }
}
