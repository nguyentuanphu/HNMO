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
}
