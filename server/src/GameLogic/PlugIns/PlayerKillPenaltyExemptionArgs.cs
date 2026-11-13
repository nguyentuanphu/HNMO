// <copyright file="PlayerKillPenaltyExemptionArgs.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

/// <summary>
/// The event args of the <see cref="IPlayerKillPenaltyExemptionPlugIn"/>.
/// </summary>
public class PlayerKillPenaltyExemptionArgs
{
    /// <summary>
    /// Gets or sets a value indicating whether the attacks and kills are exempted from the player killer penalty.
    /// </summary>
    public bool IsExempted { get; set; }
}
