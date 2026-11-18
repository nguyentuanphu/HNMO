// <copyright file="BuildCalculatorItemInput.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BuildCalculator;

/// <summary>
/// Equipped item specification passed into the build calculator.
/// </summary>
public sealed class BuildCalculatorItemInput
{
    /// <summary>Gets or sets inventory equipment slot index (0..11).</summary>
    public byte Slot { get; set; }

    /// <summary>Gets or sets item group id.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item definition number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item upgrade level (0..15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets additional option level (0..7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets a value indicating whether item skill is active.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether Luck is present.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets excellent option bitmask.</summary>
    public byte ExcellentOptions { get; set; }
}
