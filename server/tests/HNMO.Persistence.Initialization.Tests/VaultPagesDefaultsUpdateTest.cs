// <copyright file="VaultPagesDefaultsUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// The update which switches the vault pages on with two free pages replaces only the earlier defaults.
/// </summary>
[TestFixture]
internal class VaultPagesDefaultsUpdateTest
{
    /// <summary>The earlier defaults (off, one page, 100 coins) become on, two pages, 200 coins.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EarlierDefaultsBecomeTwoFreePagesAsync()
    {
        var (configuration, plugIn) = CreateWith(new MultiVaultConfiguration { Enabled = false, DefaultVaultCount = 1, PurchaseExtraVaultCost = 100 });
        await new VaultPagesDefaultsUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<MultiVaultConfiguration>(null)!;
        Assert.That(result.Enabled, Is.True);
        Assert.That(result.DefaultVaultCount, Is.EqualTo(2));
        Assert.That(result.PurchaseExtraVaultCost, Is.EqualTo(200));
        Assert.That(result.VipExtraVaults, Is.EqualTo(1));
    }

    /// <summary>Settings the host changed stay as they are.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostSettingsStayAsync()
    {
        var (configuration, plugIn) = CreateWith(new MultiVaultConfiguration { Enabled = false, DefaultVaultCount = 4, PurchaseExtraVaultCost = 100 });
        await new VaultPagesDefaultsUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<MultiVaultConfiguration>(null)!;
        Assert.That(result.Enabled, Is.False);
        Assert.That(result.DefaultVaultCount, Is.EqualTo(4));
    }

    private static (GameConfiguration Configuration, PlugInConfiguration PlugIn) CreateWith(MultiVaultConfiguration settings)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(MultiVaultPlugIn).GUID, IsActive = true };
        plugIn.SetConfiguration(settings, null);
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }
}
