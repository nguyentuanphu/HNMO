// <copyright file="TalismansPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.Talismans;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="Talismans"/> and <see cref="TalismanPlugIn"/>.
/// </summary>
[TestFixture]
public class TalismansPageTests
{
    /// <summary>Verifies Talismans is off by default and round-trips rules via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsDefaultOffStateAndPersistsTalismanRulesAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(TalismanPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (_, activeBefore, savedBefore) = await PlugInSettings.ReadStoredAsync<TalismanConfiguration>(
            persistence,
            typeof(TalismanPlugIn),
            default).ConfigureAwait(false);
        Assert.That(activeBefore, Is.False);
        Assert.That(savedBefore, Is.Null);

        using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                .First(r => r.TypeId == typeof(TalismanPlugIn).GUID);
            stored.IsActive = true;
            stored.CustomConfiguration = JsonSerializer.Serialize(new TalismanConfiguration
            {
                GlobalStatPointsCap = 8000,
                Talismans =
                [
                    new TalismanDefinitionConfig
                    {
                        ItemGroup = 13,
                        ItemNumber = 81,
                        NameEn = "Custom Figurine",
                        NameVi = "Tượng Thử Nghiệm",
                        BonusDefense = 120,
                    },
                ],
                StatConsumables =
                [
                    new StatConsumableDefinitionConfig
                    {
                        ItemGroup = 14,
                        ItemNumber = 95,
                        NameEn = "Hero Tome",
                        NameVi = "Bí Kíp Anh Hùng",
                        PointsPerUse = 25,
                        TargetBucket = "str",
                    },
                ],
            });
            await editCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, activeAfter, savedAfter) = await PlugInSettings.ReadStoredAsync<TalismanConfiguration>(
            persistence,
            typeof(TalismanPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(activeAfter, Is.True);
        Assert.That(savedAfter, Is.Not.Null);
        Assert.That(savedAfter!.GlobalStatPointsCap, Is.EqualTo(8000));
        Assert.That(savedAfter.Talismans[0].BonusDefense, Is.EqualTo(120));
        Assert.That(savedAfter.StatConsumables[0].TargetBucket, Is.EqualTo("str"));
        Assert.That(savedAfter.StatConsumables[0].PointsPerUse, Is.EqualTo(25));
    }
}
