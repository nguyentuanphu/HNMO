// <copyright file="ArenaTitlesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.CustomRanks;
using HNMO.PlugIns;

/// <summary>
/// This update adds the HNMO Arena titles (owner's choice, 2026-11-20: Bronze, Silver, Gold, Platinum, Diamond by the
/// ranked rating) to saved custom rank settings. Titles the save already has, by id, are left as they are.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("9D3A6F28-4B71-4C0E-8E52-A7F1C3D90B64")]
public class ArenaTitlesUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Arena titles";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Adds the Arena titles (Bronze to Diamond, by the ranked rating) to the custom ranks.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(CustomRanksPlugIn).GUID) is not { } plugInConfiguration
            || plugInConfiguration.GetConfiguration<CustomRankConfiguration>(null) is not { } configuration)
        {
            // No saved settings: the plug-in's defaults have the titles.
            return ValueTask.CompletedTask;
        }

        configuration.Tiers ??= [];
        var missing = CustomRankConfiguration.CreateDefaultTiers()
            .Where(t => t.MinArenaRating > 0 && !configuration.Tiers.Any(saved => string.Equals(saved.Id, t.Id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count > 0)
        {
            configuration.Tiers.AddRange(missing);
            plugInConfiguration.SetConfiguration(configuration, null);
        }

        return ValueTask.CompletedTask;
    }
}
