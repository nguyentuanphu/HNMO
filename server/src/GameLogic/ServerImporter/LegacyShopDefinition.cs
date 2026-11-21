// <copyright file="LegacyShopDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Parsed NPC shop entry.</summary>
public sealed class LegacyShopDefinition
{
    /// <summary>Gets or sets the NPC monster number.</summary>
    public int NpcNumber { get; set; }

    /// <summary>Gets or sets the map number.</summary>
    public int MapNumber { get; set; }

    /// <summary>Gets or sets items sold by the merchant.</summary>
    public List<LegacyShopItemEntry> Items { get; set; } = [];
}
