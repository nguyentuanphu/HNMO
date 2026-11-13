// <copyright file="FixEventItemsDropFromMonstersUpdatePlugInSeason6.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update fixes event items that have DropsFromMonsters set to true,
/// but should use dedicated DropItemGroups instead.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixEventItemsDropFromMonstersUpdatePlugInSeason6_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixEventItemsDropFromMonstersUpdatePlugInSeason6_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("A8B5C2D1-3E4F-5A6B-7C8D-9E0F1A2B3C4D")]
public class FixEventItemsDropFromMonstersUpdatePlugInSeason6 : FixEventItemsDropFromMonstersUpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Fix Event Items DropsFromMonsters Season 6";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "This update fixes event items that have DropsFromMonsters set to true, causing them to drop at level 0 instead of using their dedicated DropItemGroups.";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;
}