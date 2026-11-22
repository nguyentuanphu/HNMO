// <copyright file="MossGambleConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MossMerchant;

using System.ComponentModel.DataAnnotations;
using HNMO.GameLogic.Properties;

/// <summary>
/// Admin-configurable settings for <see cref="MossGamblePlugIn"/> (Moss the Merchant gambling in Elbeland).
/// </summary>
public class MossGambleConfiguration
{
    /// <summary>Gets or sets a value indicating whether buying Moss gamble vouchers (13-71..75) is enabled.</summary>
    [Display(Name = nameof(PlugInResources.MossGambleConfiguration_Enabled_Name), Description = nameof(PlugInResources.MossGambleConfiguration_Enabled_Description), ResourceType = typeof(PlugInResources))]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the Zen price charged per gamble roll at Moss the Merchant (default 2,000,000 Zen).</summary>
    [Range(0, 2_000_000_000)]
    [Display(Name = nameof(PlugInResources.MossGambleConfiguration_PriceZen_Name), Description = nameof(PlugInResources.MossGambleConfiguration_PriceZen_Description), ResourceType = typeof(PlugInResources))]
    public long PriceZen { get; set; } = 2_000_000;
}
