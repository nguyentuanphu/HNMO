// <copyright file="EnterGate.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration;

using HNMO.Annotations;

/// <summary>
/// Defines a gate which a player can enter to move to another <see cref="ExitGate"/>.
/// </summary>
[Cloneable]
public partial class EnterGate : Gate
{
    /// <summary>
    /// Gets or sets the target gate.
    /// </summary>
    [Required]
    public virtual ExitGate? TargetGate { get; set; }

    /// <summary>
    /// Gets or sets the level requirement which the player needs to move through the gate.
    /// </summary>
    public short LevelRequirement { get; set; }

    /// <summary>
    /// Gets or sets the number of the gate.
    /// </summary>
    public short Number { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(HNMO.DataModel.Properties.Resources.EnterGateSummary, base.ToString(), this.Number, this.LevelRequirement, this.TargetGate);
    }
}