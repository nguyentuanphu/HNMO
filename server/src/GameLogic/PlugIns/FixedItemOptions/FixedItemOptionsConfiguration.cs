// <copyright file="FixedItemOptionsConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.FixedItemOptions;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="FixedItemOptionsPlugIn"/> (OFF by default via <see cref="IDisabledByDefault"/>).
/// </summary>
public sealed class FixedItemOptionsConfiguration
{
    /// <summary>Gets or sets a value indicating whether configured rules apply when the plug-in is active.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the list of admin-defined fixed item option rules.</summary>
    [Display(Name = "Rules")]
    public List<FixedItemOptionRule> Rules { get; set; } = CreateDefaultRules();

    /// <summary>Creates starter fixed item option rules (box/event swords always with Luck, Kris with Luck + Option).</summary>
    /// <returns>The starter rules list.</returns>
    public static List<FixedItemOptionRule> CreateDefaultRules() =>
    [
        new()
        {
            Id = "event_swords_luck",
            Enabled = true,
            Label = "Event/Box Swords Always Luck",
            ItemGroup = 0,
            ItemNumber = -1,
            SourceFilter = "box_event",
            PinLuck = true,
            PinSkill = false,
        },
        new()
        {
            Id = "kris_sample",
            Enabled = false,
            Label = "Kris Always Luck + Opt+4",
            ItemGroup = 0,
            ItemNumber = 0,
            SourceFilter = "all",
            PinLuck = true,
            MinOptionLevel = 1,
        },
    ];
}
