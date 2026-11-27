// <copyright file="EconomyDashboardPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.PlugIns;

/// <summary>
/// Configuration host plug-in for the Economy Dashboard (ROADMAP 14.25).
/// </summary>
[PlugIn]
[Display(Name = "Economy dashboard", Description = "Tracks daily zen, coins, jewels, top holders, drop/sink rates, and sudden-jump anomalies.")]
[Guid("5B9A1F83-2E4D-4A7C-9D31-8E6F0C2B5A19")]
public sealed class EconomyDashboardPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<EconomyConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public EconomyConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EconomyConfiguration();
}
