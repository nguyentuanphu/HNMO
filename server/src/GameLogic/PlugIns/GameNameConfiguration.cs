// <copyright file="GameNameConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The configuration of the <see cref="GameNamePlugIn"/>.
/// </summary>
public class GameNameConfiguration
{
    /// <summary>
    /// The name players see when nothing else is configured.
    /// </summary>
    public const string DefaultName = "HenryN Mu Online";

    /// <summary>
    /// Gets or sets the game's name which players see.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_Name_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_Name_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(60, MinimumLength = 1)]
    public string Name { get; set; } = DefaultName;

    /// <summary>
    /// Gets or sets the Zalo link shown to players on the login screen and in the guide (empty: not shown).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_ZaloLink_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_ZaloLink_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string ZaloLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord link shown to players on the login screen and in the guide (empty: not shown).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_DiscordLink_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_DiscordLink_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string DiscordLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Facebook link shown to players on the login screen and in the guide (empty: not shown).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_FacebookLink_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_FacebookLink_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string FacebookLink { get; set; } = string.Empty;
}
