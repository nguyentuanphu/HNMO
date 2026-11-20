// <copyright file="MuHelperLevelUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.GameLogic.MuHelper;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// The MU helper needs level 80 (Season 6/9 server suites); VIP accounts level 1 (owner, 20 Nov 2026).
/// </summary>
[TestFixture]
internal class MuHelperLevelUpdateTest
{
    /// <summary>The defaults: 80 without VIP, 1 with VIP.</summary>
    [Test]
    public void DefaultsAreEightyAndOneForVip()
    {
        var configuration = new MuHelperConfiguration();
        Assert.Multiple(() =>
        {
            Assert.That(configuration.GetMinimumLevel(isVip: false), Is.EqualTo(80));
            Assert.That(configuration.GetMinimumLevel(isVip: true), Is.EqualTo(1));
        });
    }

    /// <summary>A VIP minimum above the normal one doesn't make VIP accounts wait longer.</summary>
    [Test]
    public void VipNeverNeedsMoreThanOthers()
    {
        var configuration = new MuHelperConfiguration { MinLevel = 10, VipMinLevel = 50 };
        Assert.That(configuration.GetMinimumLevel(isVip: true), Is.EqualTo(10));
    }

    /// <summary>Saved settings with the old minimum 1 get 80; the other settings stay; twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OldMinimumIsRaisedAsync()
    {
        var (configuration, plugIn) = Create(new MuHelperConfiguration { MinLevel = 1, MaxLevel = 350 });

        await ApplyAsync(configuration).ConfigureAwait(false);
        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<MuHelperConfiguration>(null)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.MinLevel, Is.EqualTo(80));
            Assert.That(result.VipMinLevel, Is.EqualTo(1));
            Assert.That(result.MaxLevel, Is.EqualTo(350));
        });
    }

    /// <summary>A minimum the host chose stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostMinimumIsKeptAsync()
    {
        var (configuration, plugIn) = Create(new MuHelperConfiguration { MinLevel = 20 });

        await ApplyAsync(configuration).ConfigureAwait(false);

        Assert.That(plugIn.GetConfiguration<MuHelperConfiguration>(null)!.MinLevel, Is.EqualTo(20));
    }

    /// <summary>Settings the admin panel saved (with "$id" and "$values" markers) are raised too.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SettingsSavedByTheAdminPanelAreRaisedAsync()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration
        {
            TypeId = typeof(MuHelperFeaturePlugIn).GUID,
            IsActive = true,
            CustomConfiguration = """{ "$id": "1", "CostPerStage": { "$id": "2", "$values": [ 100, 1000 ] }, "MinLevel": 1, "MaxLevel": 400 }""",
        };
        configuration.PlugInConfigurations.Add(plugIn);

        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<MuHelperConfiguration>(null)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.MinLevel, Is.EqualTo(80));
            Assert.That(result.CostPerStage, Is.EqualTo(new[] { 100, 1000 }));
        });
    }

    private static (HNMO.Persistence.BasicModel.GameConfiguration Configuration, HNMO.Persistence.BasicModel.PlugInConfiguration PlugIn) Create(MuHelperConfiguration saved)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(MuHelperFeaturePlugIn).GUID, IsActive = true };
        plugIn.SetConfiguration(saved, null);
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }

    private static ValueTask ApplyAsync(HNMO.Persistence.BasicModel.GameConfiguration configuration)
        => new MuHelperLevelUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration);
}
