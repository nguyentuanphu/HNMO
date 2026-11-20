// <copyright file="ExcellentDropChanceUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// This update raises the chance of the common excellent item drop group from 0.01 % to 0.2 % per kill, as in the
/// Season 6/9 server suites (owner's choice, 20 Nov 2026). A chance the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5D2A9C63-7E14-4B08-9F3D-A61C2E7B4F90")]
public class ExcellentDropChanceUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Excellent item drop: 0.2 %";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Raises the chance of the common excellent item drop from 0.01 % to 0.2 % per kill.";

    /// <summary>The earlier chance.</summary>
    internal const double OldChance = 0.0001;

    /// <summary>The new chance.</summary>
    internal const double NewChance = 0.002;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var group in gameConfiguration.DropItemGroups.Where(g => g.ItemType == SpecialItemType.Excellent
                     && Math.Abs(g.Chance - OldChance) < 0.0000001))
        {
            group.Chance = NewChance;
            group.Description = LocalizedString.FromResource(() => DropGroupDescriptions.TheCommonDropItemGroupForRandomExcellentItems001PercentDropChance);
        }

        return ValueTask.CompletedTask;
    }
}
