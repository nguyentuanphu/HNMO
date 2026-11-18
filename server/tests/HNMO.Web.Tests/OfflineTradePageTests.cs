// <copyright file="OfflineTradePageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.OfflineTrade;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="OffTrade"/> and <see cref="OfflineTradePlugIn"/>.
/// </summary>
[TestFixture]
public class OfflineTradePageTests
{
    /// <summary>Verifies Off-Trade is off by default and round-trips updated settings via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsDefaultOffStateAndPersistsUpdatedSettings()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(OfflineTradePlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (_, activeBefore, savedBefore) = await PlugInSettings.ReadStoredAsync<OfflineTradeConfiguration>(
            persistence,
            typeof(OfflineTradePlugIn),
            default).ConfigureAwait(false);
        Assert.That(activeBefore, Is.False);
        Assert.That(savedBefore, Is.Null);

        using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                .First(r => r.TypeId == typeof(OfflineTradePlugIn).GUID);
            stored.IsActive = true;
            stored.CustomConfiguration = JsonSerializer.Serialize(new OfflineTradeConfiguration
            {
                SafeZoneOnly = false,
                AllowCoinCurrencies = true,
                MaxDurationHours = 72,
                OpenFeeZen = 25000,
                StopWhenSoldOut = false,
                MinimumLevel = 50,
                AllowedMapNumbers = [0, 2, 3],
            });
            await editCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, activeAfter, savedAfter) = await PlugInSettings.ReadStoredAsync<OfflineTradeConfiguration>(
            persistence,
            typeof(OfflineTradePlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(activeAfter, Is.True);
        Assert.That(savedAfter, Is.Not.Null);
        Assert.That(savedAfter!.SafeZoneOnly, Is.False);
        Assert.That(savedAfter.AllowCoinCurrencies, Is.True);
        Assert.That(savedAfter.MaxDurationHours, Is.EqualTo(72));
        Assert.That(savedAfter.OpenFeeZen, Is.EqualTo(25000));
        Assert.That(savedAfter.StopWhenSoldOut, Is.False);
        Assert.That(savedAfter.MinimumLevel, Is.EqualTo(50));
        Assert.That(savedAfter.AllowedMapNumbers, Is.EqualTo(new short[] { 0, 2, 3 }));
    }
}
