// <copyright file="MaximumConnectionsPerIpPlugInConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

/// <summary>
/// Configuration for the <see cref="MaximumConnectionsPerIpPlugIn"/>.
/// </summary>
public class MaximumConnectionsPerIpPlugInConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent connections per IP address.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(HNMO.GameLogic.Properties.PlugInResources.MaximumConnectionsPerIpPlugInConfiguration_MaximumConnectionsPerIp_Caption), ResourceType = typeof(HNMO.GameLogic.Properties.PlugInResources))]
    public int MaximumConnectionsPerIp { get; set; } = 3;
}
