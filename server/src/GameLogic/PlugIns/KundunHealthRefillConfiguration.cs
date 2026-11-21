// <copyright file="KundunHealthRefillConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

/// <summary>
/// The configuration of the <see cref="KundunHealthRefillPlugIn"/>. The defaults are the values of the Season 6/9
/// server suites.
/// </summary>
public class KundunHealthRefillConfiguration
{
    /// <summary>
    /// Gets or sets the number of the monster which gets health back (Kundun, 275).
    /// </summary>
    [Display(Name = "Monster number", Description = "The monster which gets health back; 275 is Kundun of Kalima 7.")]
    public short MonsterNumber { get; set; } = 275;

    /// <summary>
    /// Gets or sets the health which the monster gets back every second (KundunRefillHPSec).
    /// </summary>
    [Display(Name = "Health per second", Description = "The health Kundun gets back every second (KundunRefillHPSec: 400).")]
    public int HealthPerSecond { get; set; } = 400;

    /// <summary>
    /// Gets or sets the health which the monster gets back at each <see cref="Interval"/> (KundunRefillHP).
    /// </summary>
    [Display(Name = "Health per interval", Description = "The health Kundun gets back at each interval (KundunRefillHP: 15,000).")]
    public int HealthPerInterval { get; set; } = 15_000;

    /// <summary>
    /// Gets or sets the interval of the <see cref="HealthPerInterval"/> (KundunRefillHPTime).
    /// </summary>
    [Display(Name = "Interval", Description = "How often Kundun gets the health per interval (KundunRefillHPTime: 600 seconds). Zero turns it off.")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(600);
}
