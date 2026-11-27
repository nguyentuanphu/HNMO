// <copyright file="DuplicateItemsConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.DuplicateItems;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The configuration of the <see cref="DuplicateItemsPlugIn"/>.
/// </summary>
public class DuplicateItemsConfiguration
{
    /// <summary>Gets or sets how often the database is scanned for duplicates, in minutes.</summary>
    [Display(Name = nameof(Properties.PlugInResources.DuplicateItemsConfiguration_ScanIntervalMinutes_Caption), Description = nameof(Properties.PlugInResources.DuplicateItemsConfiguration_ScanIntervalMinutes_Description), ResourceType = typeof(Properties.PlugInResources))]
    [Range(5, 1440)]
    public int ScanIntervalMinutes { get; set; } = 60;

    /// <summary>Gets or sets a value indicating whether found duplicates are frozen until an administrator decides.</summary>
    [Display(Name = nameof(Properties.PlugInResources.DuplicateItemsConfiguration_FreezeDuplicates_Caption), Description = nameof(Properties.PlugInResources.DuplicateItemsConfiguration_FreezeDuplicates_Description), ResourceType = typeof(Properties.PlugInResources))]
    public bool FreezeDuplicates { get; set; } = true;

    /// <summary>Gets or sets the HNMO state file which keeps the found cases.</summary>
    public string FileName { get; set; } = HnmoStateStore.StateFileName;
}
