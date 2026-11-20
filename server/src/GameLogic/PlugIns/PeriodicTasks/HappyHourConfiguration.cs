// <copyright file="HappyHourConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PeriodicTasks;

using System;

/// <summary>
/// The Happy Hour event configuration.
/// </summary>
public class HappyHourConfiguration : PeriodicTaskConfiguration
{
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static HappyHourConfiguration Default => new()
    {
        TaskDuration = TimeSpan.FromHours(2),
        PreStartMessageDelay = TimeSpan.FromSeconds(0),
        StartMessage = "Happy Hour event has been started!",
        EndMessage = "Happy Hour event has ended!",
        Timetable = GenerateTimeSequence(TimeSpan.FromHours(6), new TimeOnly(2, 0)).ToList(), // 02:00, 08:00, 14:00, 20:00 server time (Season 6/9 server suites)
        ExperienceMultiplier = 1.5f,
        ItemDropBonusPercent = 15,
    };

    /// <summary>
    /// Gets or sets the experience multiplier.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.HappyHourConfiguration_ExperienceMultiplier_Name), Order = 100)]
    public float ExperienceMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Gets or sets the extra item drops during the happy hour, in percent (15 like the Season 6/9 server suites).
    /// It doesn't add to a bonus event's drop bonus: the larger one applies.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.HappyHourConfiguration_ItemDropBonusPercent_Name), Order = 101)]
    public int ItemDropBonusPercent { get; set; } = 15;
}