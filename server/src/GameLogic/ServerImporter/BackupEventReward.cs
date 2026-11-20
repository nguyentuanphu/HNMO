// <copyright file="BackupEventReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System.Collections.Generic;

/// <summary>The item reward of a mini game level before an import changed it.</summary>
public sealed class BackupEventReward
{
    /// <summary>Gets or sets the mini game type (its number).</summary>
    public int MiniGameType { get; set; }

    /// <summary>Gets or sets the mini game level.</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets the chance of the reward's item group.</summary>
    public double Chance { get; set; }

    /// <summary>Gets or sets the reward's items as "group:number" keys.</summary>
    public List<string> ItemKeys { get; set; } = [];
}
