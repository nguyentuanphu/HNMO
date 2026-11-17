// <copyright file="TeleportNpcPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.TeleportNpc;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="TeleportNpc"/> and <see cref="TeleportNpcPlugIn"/>.
/// </summary>
[TestFixture]
public class TeleportNpcPageTests
{
    /// <summary>Verifies TeleportNpc is off by default and round-trips destinations via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsDefaultOffStateAndPersistsTeleportDestinationsAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(TeleportNpcPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (_, activeBefore, savedBefore) = await PlugInSettings.ReadStoredAsync<TeleportNpcConfiguration>(
            persistence,
            typeof(TeleportNpcPlugIn),
            default).ConfigureAwait(false);
        Assert.That(activeBefore, Is.False);
        Assert.That(savedBefore, Is.Null);

        using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                .First(r => r.TypeId == typeof(TeleportNpcPlugIn).GUID);
            stored.IsActive = true;
            stored.CustomConfiguration = JsonSerializer.Serialize(new TeleportNpcConfiguration
            {
                Enabled = true,
                NpcNumbers = [247],
                CloseAfterWarp = true,
                Destinations =
                [
                    new TeleportDestinationConfig
                    {
                        Id = "atlans",
                        NameEn = "Atlans",
                        NameVi = "Atlans",
                        MapNumber = 7,
                        TargetX = 23,
                        TargetY = 19,
                        MinLevel = 60,
                        ZenCost = 15000,
                    },
                ],
            });
            await editCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, activeAfter, savedAfter) = await PlugInSettings.ReadStoredAsync<TeleportNpcConfiguration>(
            persistence,
            typeof(TeleportNpcPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(activeAfter, Is.True);
        Assert.That(savedAfter, Is.Not.Null);
        Assert.That(savedAfter!.NpcNumbers, Is.EqualTo(new short[] { 247 }));
        Assert.That(savedAfter.Destinations[0].ZenCost, Is.EqualTo(15000));
        Assert.That(savedAfter.Destinations[0].MapNumber, Is.EqualTo(7));
    }
}
