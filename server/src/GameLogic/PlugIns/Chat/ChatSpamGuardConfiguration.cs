// <copyright file="ChatSpamGuardConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The configuration of the <see cref="ChatSpamGuardPlugIn"/>.
/// </summary>
public class ChatSpamGuardConfiguration
{
    /// <summary>
    /// Gets or sets how often the same text may be sent within <see cref="RepeatSeconds"/>; 0 doesn't check it.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ChatSpamGuardConfiguration_MaximumRepeats_Name), Description = nameof(PlugInResources.ChatSpamGuardConfiguration_MaximumRepeats_Description))]
    [Range(0, 100)]
    public int MaximumRepeats { get; set; } = ChatSpamLimits.Default.MaximumRepeats;

    /// <summary>
    /// Gets or sets the seconds of <see cref="MaximumRepeats"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ChatSpamGuardConfiguration_RepeatSeconds_Name))]
    [Range(1, 3600)]
    public int RepeatSeconds { get; set; } = (int)ChatSpamLimits.Default.RepeatWindow.TotalSeconds;

    /// <summary>
    /// Gets or sets how many lines may be sent within <see cref="LineSeconds"/>; 0 doesn't check it.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ChatSpamGuardConfiguration_MaximumLines_Name), Description = nameof(PlugInResources.ChatSpamGuardConfiguration_MaximumLines_Description))]
    [Range(0, 100)]
    public int MaximumLines { get; set; } = ChatSpamLimits.Default.MaximumLines;

    /// <summary>
    /// Gets or sets the seconds of <see cref="MaximumLines"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ChatSpamGuardConfiguration_LineSeconds_Name))]
    [Range(1, 3600)]
    public int LineSeconds { get; set; } = (int)ChatSpamLimits.Default.LineWindow.TotalSeconds;

    /// <summary>
    /// Gets the limits.
    /// </summary>
    /// <returns>The limits.</returns>
    public ChatSpamLimits ToLimits() => new(this.MaximumRepeats, TimeSpan.FromSeconds(Math.Max(1, this.RepeatSeconds)), this.MaximumLines, TimeSpan.FromSeconds(Math.Max(1, this.LineSeconds)));
}
