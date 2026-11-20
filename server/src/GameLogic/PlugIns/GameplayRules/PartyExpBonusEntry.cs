// <copyright file="PartyExpBonusEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>The EXP a party of a size gets for a kill, in percent of the kill's EXP.</summary>
public sealed class PartyExpBonusEntry
{
    /// <summary>Gets or sets the number of party members in the kill's range.</summary>
    public int PartySize { get; set; }

    /// <summary>Gets or sets the EXP percent of a party.</summary>
    public int ExpPercent { get; set; } = 100;

    /// <summary>Gets or sets the EXP percent of a party whose members all have different classes (0: same as <see cref="ExpPercent"/>).</summary>
    public int DifferentClassesExpPercent { get; set; }
}
