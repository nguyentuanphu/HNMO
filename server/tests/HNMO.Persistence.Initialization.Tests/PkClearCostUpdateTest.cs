// <copyright file="PkClearCostUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// The cost of /pkclear is 1,000,000 zen per kill (Season 6/9 server suites); saved settings with the old default get it.
/// </summary>
[TestFixture]
internal class PkClearCostUpdateTest
{
    /// <summary>The plug-in's default is 1,000,000 zen per kill.</summary>
    [Test]
    public void DefaultIsOneMillion()
    {
        Assert.That(new PkClearChatCommandPlugIn.PKClearConfiguration().ZenCostPerKill, Is.EqualTo(1_000_000));
    }

    /// <summary>Saved settings with 10,000,000 get 1,000,000; twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OldDefaultIsLoweredAsync()
    {
        var (configuration, plugIn) = Create(new PkClearChatCommandPlugIn.PKClearConfiguration { ZenCostPerKill = 10_000_000 });

        await ApplyAsync(configuration).ConfigureAwait(false);
        await ApplyAsync(configuration).ConfigureAwait(false);

        Assert.That(plugIn.GetConfiguration<PkClearChatCommandPlugIn.PKClearConfiguration>(null)!.ZenCostPerKill, Is.EqualTo(1_000_000));
    }

    /// <summary>A cost the host chose stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostCostIsKeptAsync()
    {
        var (configuration, plugIn) = Create(new PkClearChatCommandPlugIn.PKClearConfiguration { ZenCostPerKill = 5_000_000 });

        await ApplyAsync(configuration).ConfigureAwait(false);

        Assert.That(plugIn.GetConfiguration<PkClearChatCommandPlugIn.PKClearConfiguration>(null)!.ZenCostPerKill, Is.EqualTo(5_000_000));
    }

    /// <summary>Settings the admin panel saved (with "$id" markers) get the new cost too.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SettingsSavedByTheAdminPanelAreLoweredAsync()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration
        {
            TypeId = typeof(PkClearChatCommandPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = """{ "$id": "1", "ZenCostPerKill": 10000000, "AllowRegularPlayers": true }""",
        };
        configuration.PlugInConfigurations.Add(plugIn);

        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<PkClearChatCommandPlugIn.PKClearConfiguration>(null)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.ZenCostPerKill, Is.EqualTo(1_000_000));
            Assert.That(result.AllowRegularPlayers, Is.True);
        });
    }

    private static (HNMO.Persistence.BasicModel.GameConfiguration Configuration, HNMO.Persistence.BasicModel.PlugInConfiguration PlugIn) Create(PkClearChatCommandPlugIn.PKClearConfiguration saved)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(PkClearChatCommandPlugIn).GUID, IsActive = true };
        plugIn.SetConfiguration(saved, null);
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }

    private static ValueTask ApplyAsync(HNMO.Persistence.BasicModel.GameConfiguration configuration)
        => new PkClearCostUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration);
}
