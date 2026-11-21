// <copyright file="VaultPagesDefaultsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.PlugIns;

/// <summary>
/// This update switches the vault pages on with the owner's defaults (2026-11-20): two free pages per account, one
/// more for VIP, and more for 200 WCoin (C) each. A save whose vault page settings the host changed keeps them; only
/// the earlier defaults (off, one page, 100 coins) are replaced.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("6E2B8A41-0C7D-4F93-B5A6-1D84C3E9F270")]
public class VaultPagesDefaultsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Vault pages on: two free pages";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Switches the vault pages on with two free pages per account, one more for VIP and more for 200 WCoin (C) each, unless the host changed them.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(MultiVaultPlugIn).GUID) is not { } plugInConfiguration
            || plugInConfiguration.GetConfiguration<MultiVaultConfiguration>(null) is not { } configuration)
        {
            // No saved settings: the plug-in's defaults (on, two pages) apply.
            return ValueTask.CompletedTask;
        }

        if (configuration.Enabled || configuration.DefaultVaultCount != 1 || configuration.PurchaseExtraVaultCost != 100)
        {
            // The host chose these settings.
            return ValueTask.CompletedTask;
        }

        var defaults = new MultiVaultConfiguration();
        configuration.Enabled = defaults.Enabled;
        configuration.DefaultVaultCount = defaults.DefaultVaultCount;
        configuration.PurchaseExtraVaultCost = defaults.PurchaseExtraVaultCost;
        plugInConfiguration.SetConfiguration(configuration, null);
        return ValueTask.CompletedTask;
    }
}
