// <copyright file="VipPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>Stable VIP configuration identity; gameplay registration follows the authenticated adapter integration.</summary>
[Guid("B5EE9346-9ED8-4CC0-8CA2-2D0E27CA2081")]
public sealed class VipPlugIn : ISupportCustomConfiguration<VipConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc />
    public VipConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new VipConfiguration();
}
