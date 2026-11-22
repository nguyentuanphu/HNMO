// <copyright file="KundunHealthRefillConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using HNMO.GameLogic.Properties;

/// <summary>
/// The configuration of the <see cref="KundunHealthRefillPlugIn"/>. The defaults are the values of the Season 6/9
/// server suites.
/// </summary>
public class KundunHealthRefillConfiguration
{
    /// <summary>
    /// Gets or sets the number of the monster which gets health back (Kundun, 275).
    /// </summary>
    [Display(Name = nameof(PlugInResources.KundunHealthRefillConfiguration_MonsterNumber_Caption), Description = nameof(PlugInResources.KundunHealthRefillConfiguration_MonsterNumber_Description), ResourceType = typeof(PlugInResources))]
    public short MonsterNumber { get; set; } = 275;

    /// <summary>
    /// Gets or sets the health which the monster gets back every second (KundunRefillHPSec).
    /// </summary>
    [Display(Name = nameof(PlugInResources.KundunHealthRefillConfiguration_HealthPerSecond_Caption), Description = nameof(PlugInResources.KundunHealthRefillConfiguration_HealthPerSecond_Description), ResourceType = typeof(PlugInResources))]
    public int HealthPerSecond { get; set; } = 400;

    /// <summary>
    /// Gets or sets the health which the monster gets back at each <see cref="Interval"/> (KundunRefillHP).
    /// </summary>
    [Display(Name = nameof(PlugInResources.KundunHealthRefillConfiguration_HealthPerInterval_Caption), Description = nameof(PlugInResources.KundunHealthRefillConfiguration_HealthPerInterval_Description), ResourceType = typeof(PlugInResources))]
    public int HealthPerInterval { get; set; } = 15_000;

    /// <summary>
    /// Gets or sets the interval of the <see cref="HealthPerInterval"/> (KundunRefillHPTime).
    /// </summary>
    [Display(Name = nameof(PlugInResources.KundunHealthRefillConfiguration_Interval_Caption), Description = nameof(PlugInResources.KundunHealthRefillConfiguration_Interval_Description), ResourceType = typeof(PlugInResources))]
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(600);
}
