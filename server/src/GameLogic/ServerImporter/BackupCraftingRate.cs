// <copyright file="BackupCraftingRate.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

/// <summary>The success rate of a chaos machine crafting before an import changed it.</summary>
public sealed class BackupCraftingRate
{
    /// <summary>Gets or sets the crafting number.</summary>
    public byte CraftingNumber { get; set; }

    /// <summary>Gets or sets the success percent.</summary>
    public byte SuccessPercent { get; set; }

    /// <summary>Gets or sets the percent added for an item with luck.</summary>
    public int LuckBonusPercent { get; set; }

    /// <summary>Gets or sets the highest success percent.</summary>
    public byte MaximumSuccessPercent { get; set; }
}
