// <copyright file="VipRuleResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>Machine-readable rule outcomes; presentation layers must localize them.</summary>
public enum VipRuleResult
{
    /// <summary>The feature is not enabled.</summary>
    Disabled,

    /// <summary>The request or configured values are invalid.</summary>
    Invalid,

    /// <summary>The configured level is unavailable.</summary>
    UnknownLevel,

    /// <summary>The requested duration is not offered.</summary>
    UnknownDuration,

    /// <summary>The quoted balance cannot cover the price.</summary>
    InsufficientCoins,

    /// <summary>An active subscription has a different level.</summary>
    DifferentActiveLevel,

    /// <summary>The quote is valid.</summary>
    Ok,
}
