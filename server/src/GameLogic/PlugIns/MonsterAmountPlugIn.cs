// <copyright file="MonsterAmountPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Multiplies the monster amounts of maps (×0.5 to ×5), without changing the stored spawn quantities.
/// The <see cref="MapInitializer"/> reads it when a map spawns its monsters, and applies a change at once
/// to the running maps: missing monsters spawn, extra ones are removed.
/// The admin panel sets it on the map editor ("Monster amount").
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.MonsterAmountPlugIn_Name), Description = nameof(Properties.PlugInResources.MonsterAmountPlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("8C2F4E71-3B6A-4D95-A0E8-6F1D2C7B9A34")]
public class MonsterAmountPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<MonsterAmountConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public MonsterAmountConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new MonsterAmountConfiguration();
    }
}
