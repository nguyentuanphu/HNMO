// <copyright file="CustomRanksPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.CustomRanks;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="CustomRanks"/> and <see cref="CustomRanksPlugIn"/>.
/// </summary>
[TestFixture]
public class CustomRanksPageTests
{
    /// <summary>Verifies CustomRanks is off by default and round-trips tiers via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsDefaultOffStateAndPersistsCustomRankTiersAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(CustomRanksPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (_, activeBefore, savedBefore) = await PlugInSettings.ReadStoredAsync<CustomRankConfiguration>(
            persistence,
            typeof(CustomRanksPlugIn),
            default).ConfigureAwait(false);
        Assert.That(activeBefore, Is.False);
        Assert.That(savedBefore, Is.Null);

        using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                .First(r => r.TypeId == typeof(CustomRanksPlugIn).GUID);
            stored.IsActive = true;
            stored.CustomConfiguration = JsonSerializer.Serialize(new CustomRankConfiguration
            {
                Enabled = true,
                ShowByNameplate = true,
                ShowInChat = true,
                Tiers =
                [
                    new CustomRankTierConfig
                    {
                        Id = "champion",
                        Priority = 80,
                        TitleEn = "[Champion]",
                        TitleVi = "[Quán Quân]",
                        ColorHex = "#38bdf8",
                        MinResets = 5,
                    },
                ],
            });
            await editCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, activeAfter, savedAfter) = await PlugInSettings.ReadStoredAsync<CustomRankConfiguration>(
            persistence,
            typeof(CustomRanksPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(activeAfter, Is.True);
        Assert.That(savedAfter, Is.Not.Null);
        Assert.That(savedAfter!.Tiers[0].Id, Is.EqualTo("champion"));
        Assert.That(savedAfter.Tiers[0].TitleVi, Is.EqualTo("[Quán Quân]"));
        Assert.That(savedAfter.Tiers[0].MinResets, Is.EqualTo(5));
    }
}
