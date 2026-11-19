// <copyright file="LegacyCharacterRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Character row read from <c>Character</c>.</summary>
public sealed class LegacyCharacterRow
{
    /// <summary>Gets or sets owning account login name.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets legacy class byte (0, 1, 2, 16, ...).</summary>
    public byte ClassNumber { get; set; }

    /// <summary>Gets or sets character level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets remaining level-up stat points.</summary>
    public int LevelUpPoints { get; set; }

    /// <summary>Gets or sets total experience.</summary>
    public long Experience { get; set; }

    /// <summary>Gets or sets base strength.</summary>
    public int Strength { get; set; }

    /// <summary>Gets or sets base agility/dexterity.</summary>
    public int Agility { get; set; }

    /// <summary>Gets or sets base vitality.</summary>
    public int Vitality { get; set; }

    /// <summary>Gets or sets base energy.</summary>
    public int Energy { get; set; }

    /// <summary>Gets or sets base leadership/command.</summary>
    public int Leadership { get; set; }

    /// <summary>Gets or sets inventory zen.</summary>
    public int Money { get; set; }

    /// <summary>Gets or sets current map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets current X coordinate.</summary>
    public byte MapPosX { get; set; }

    /// <summary>Gets or sets current Y coordinate.</summary>
    public byte MapPosY { get; set; }

    /// <summary>Gets or sets raw inventory binary blob.</summary>
    public byte[] InventoryBlob { get; set; } = [];
}
