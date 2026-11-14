// <copyright file="OfflineWhisperConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The configuration of the <see cref="OfflineWhisperPlugIn"/>.
/// </summary>
public class OfflineWhisperConfiguration
{
    /// <summary>
    /// Gets or sets the maximum of waiting whispers per receiver.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.OfflineWhisperConfiguration_MaximumPerReceiver_Name), Description = nameof(PlugInResources.OfflineWhisperConfiguration_MaximumPerReceiver_Description))]
    [Range(1, 200)]
    public int MaximumPerReceiver { get; set; } = 20;

    /// <summary>
    /// Gets or sets the number of days a whisper waits at most.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.OfflineWhisperConfiguration_MaximumAgeDays_Name), Description = nameof(PlugInResources.OfflineWhisperConfiguration_MaximumAgeDays_Description))]
    [Range(1, 90)]
    public int MaximumAgeDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the file which keeps the waiting whispers over restarts, relative to the server's folder; empty keeps them in memory only.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.OfflineWhisperConfiguration_FileName_Name), Description = nameof(PlugInResources.OfflineWhisperConfiguration_FileName_Description))]
    [StringLength(260)]
    public string FileName { get; set; } = "offline-whispers.json";
}
