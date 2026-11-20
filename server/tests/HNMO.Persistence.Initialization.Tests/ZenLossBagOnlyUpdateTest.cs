// <copyright file="ZenLossBagOnlyUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.GameLogic.PlugIns;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// Saved zen loss settings with the earlier default vault losses stop taking zen from the vault.
/// </summary>
[TestFixture]
internal class ZenLossBagOnlyUpdateTest
{
    /// <summary>The earlier default (1-4 % from bag and vault) keeps the bag losses and takes nothing from the vault.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EarlierDefaultVaultLossIsRemovedAsync()
    {
        var (configuration, plugIn) = Create(new PlayerLosesMoneyAfterDeathPlugInConfiguration
        {
            LossesPerLevel = [new(10, false, 1, 1), new(150, false, 2, 2), new(220, false, 3, 3), new(0, true, 4, 4)],
        });

        await ApplyAsync(configuration).ConfigureAwait(false);
        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<PlayerLosesMoneyAfterDeathPlugInConfiguration>(null)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.LossesPerLevel.Select(l => l.VaultMoneyLossPercentage), Is.All.Zero);
            Assert.That(result.LossesPerLevel.OrderBy(l => l.IsMaster).ThenBy(l => l.MinimumLevel).Select(l => l.InventoryMoneyLossPercentage), Is.EqualTo(new double[] { 1, 2, 3, 4 }));
        });
    }

    /// <summary>The settings saved by the admin panel (with "$id"/"$values" markers) are read as well.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AdminPanelSaveIsReadAsync()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration
        {
            TypeId = typeof(PlayerLosesMoneyAfterDeathPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = """
                {"$id":"1","LossesPerLevel":{"$id":"2","$values":[
                {"$id":"3","MinimumLevel":10,"IsMaster":false,"InventoryMoneyLossPercentage":1,"VaultMoneyLossPercentage":1},
                {"$id":"4","MinimumLevel":150,"IsMaster":false,"InventoryMoneyLossPercentage":2,"VaultMoneyLossPercentage":2},
                {"$id":"5","MinimumLevel":220,"IsMaster":false,"InventoryMoneyLossPercentage":3,"VaultMoneyLossPercentage":3},
                {"$id":"6","MinimumLevel":0,"IsMaster":true,"InventoryMoneyLossPercentage":4,"VaultMoneyLossPercentage":4}]}}
                """,
        };
        configuration.PlugInConfigurations.Add(plugIn);

        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<PlayerLosesMoneyAfterDeathPlugInConfiguration>(null)!;
        Assert.That(result.LossesPerLevel.Select(l => l.VaultMoneyLossPercentage), Is.All.Zero);
    }

    /// <summary>Vault losses the host chose stay.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostVaultLossIsKeptAsync()
    {
        var (configuration, plugIn) = Create(new PlayerLosesMoneyAfterDeathPlugInConfiguration
        {
            LossesPerLevel = [new(10, false, 1, 5), new(150, false, 2, 2), new(220, false, 3, 3), new(0, true, 4, 4)],
        });

        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<PlayerLosesMoneyAfterDeathPlugInConfiguration>(null)!;
        Assert.That(result.LossesPerLevel.First(l => l.MinimumLevel == 10).VaultMoneyLossPercentage, Is.EqualTo(5));
    }

    private static (HNMO.Persistence.BasicModel.GameConfiguration Configuration, HNMO.Persistence.BasicModel.PlugInConfiguration PlugIn) Create(PlayerLosesMoneyAfterDeathPlugInConfiguration saved)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(PlayerLosesMoneyAfterDeathPlugIn).GUID, IsActive = true };
        plugIn.SetConfiguration(saved, null);
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }

    private static ValueTask ApplyAsync(HNMO.Persistence.BasicModel.GameConfiguration configuration)
        => new ZenLossBagOnlyUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration);
}
