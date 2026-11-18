// <copyright file="FixedOptionsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.FixedItemOptions;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="FixedOptions"/> and <see cref="FixedItemOptionsPlugIn"/>.
/// </summary>
[TestFixture]
public class FixedOptionsPageTests
{
    /// <summary>Verifies FixedItemOptions is off by default and round-trips rules via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsDefaultOffStateAndPersistsFixedOptionRulesAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(FixedItemOptionsPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (_, activeBefore, savedBefore) = await PlugInSettings.ReadStoredAsync<FixedItemOptionsConfiguration>(
            persistence,
            typeof(FixedItemOptionsPlugIn),
            default).ConfigureAwait(false);
        Assert.That(activeBefore, Is.False);
        Assert.That(savedBefore, Is.Null);

        using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                .First(r => r.TypeId == typeof(FixedItemOptionsPlugIn).GUID);
            stored.IsActive = true;
            stored.CustomConfiguration = JsonSerializer.Serialize(new FixedItemOptionsConfiguration
            {
                Enabled = true,
                Rules =
                [
                    new FixedItemOptionRule
                    {
                        Id = "ds_swords",
                        Label = "DS Box Swords Luck",
                        ItemGroup = 0,
                        ItemNumber = -1,
                        SourceFilter = "box_event",
                        PinLuck = true,
                        MinLevel = 7,
                    },
                ],
            });
            await editCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, activeAfter, savedAfter) = await PlugInSettings.ReadStoredAsync<FixedItemOptionsConfiguration>(
            persistence,
            typeof(FixedItemOptionsPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(activeAfter, Is.True);
        Assert.That(savedAfter, Is.Not.Null);
        Assert.That(savedAfter!.Rules[0].PinLuck, Is.True);
        Assert.That(savedAfter.Rules[0].MinLevel, Is.EqualTo(7));
        Assert.That(savedAfter.Rules[0].SourceFilter, Is.EqualTo("box_event"));
    }
}
