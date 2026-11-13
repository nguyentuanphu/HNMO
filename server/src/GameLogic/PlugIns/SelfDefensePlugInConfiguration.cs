// <copyright file="SelfDefensePlugInConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

/// <summary>
/// Configuration for the <see cref="SelfDefensePlugIn"/>.
/// </summary>
public class SelfDefensePlugInConfiguration
{
    /// <summary>
    /// Gets or sets the self defense timeout.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.SelfDefensePlugInConfiguration_SelfDefenseTimeOut_Name))]
    public TimeSpan SelfDefenseTimeOut { get; set; } = TimeSpan.FromMinutes(1);
}