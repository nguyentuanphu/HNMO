// <copyright file="BufferNpcPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.BufferNpc;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="BufferNpc"/> and <see cref="BufferNpcPlugIn"/>.
/// </summary>
[TestFixture]
public class BufferNpcPageTests
{
    /// <summary>Verifies Buffer NPC is off by default and round-trips configuration via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsDefaultOffStateAndPersistsUpdatedSettings()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(BufferNpcPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (_, activeBefore, savedBefore) = await PlugInSettings.ReadStoredAsync<BufferNpcConfiguration>(
            persistence,
            typeof(BufferNpcPlugIn),
            default).ConfigureAwait(false);
        Assert.That(activeBefore, Is.False);
        Assert.That(savedBefore, Is.Null);

        using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                .First(r => r.TypeId == typeof(BufferNpcPlugIn).GUID);
            stored.IsActive = true;
            stored.CustomConfiguration = JsonSerializer.Serialize(new BufferNpcConfiguration
            {
                MaxLevel = 300,
                MaxResets = 5,
                DurationSeconds = 2400,
                ZenCost = 2000,
                NpcNumbers = [257, 240],
                BuffEffectNumbers = [3, 1, 2, 4],
            });
            await editCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, activeAfter, savedAfter) = await PlugInSettings.ReadStoredAsync<BufferNpcConfiguration>(
            persistence,
            typeof(BufferNpcPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(activeAfter, Is.True);
        Assert.That(savedAfter, Is.Not.Null);
        Assert.That(savedAfter!.MaxLevel, Is.EqualTo(300));
        Assert.That(savedAfter.MaxResets, Is.EqualTo(5));
        Assert.That(savedAfter.DurationSeconds, Is.EqualTo(2400));
        Assert.That(savedAfter.ZenCost, Is.EqualTo(2000));
        Assert.That(savedAfter.NpcNumbers, Is.EqualTo(new short[] { 257, 240 }));
        Assert.That(savedAfter.BuffEffectNumbers, Is.EqualTo(new short[] { 3, 1, 2, 4 }));
    }
}
