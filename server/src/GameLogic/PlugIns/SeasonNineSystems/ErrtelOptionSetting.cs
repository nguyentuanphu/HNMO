// <copyright file="ErrtelOptionSetting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>One errtel option (admin setting): which errtel and rank it belongs to and its value at levels 0, 5 and 10.</summary>
public sealed class ErrtelOptionSetting
{
    /// <summary>Gets or sets the option index (kept on the items; never renumber).</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the errtel's item number (12/221..261).</summary>
    public short ErrtelNumber { get; set; }

    /// <summary>Gets or sets the rank (1..5).</summary>
    public byte Rank { get; set; }

    /// <summary>Gets or sets the weight of this option among the options of its errtel and rank.</summary>
    public int Rate { get; set; }

    /// <summary>Gets or sets the value at level 0.</summary>
    public int ValueLv0 { get; set; }

    /// <summary>Gets or sets the value at level 5.</summary>
    public int ValueLv5 { get; set; }

    /// <summary>Gets or sets the value at level 10.</summary>
    public int ValueLv10 { get; set; }

    /// <summary>Gets or sets the shown text, with <c>%d</c> for the value.</summary>
    public string Name { get; set; } = string.Empty;
}
