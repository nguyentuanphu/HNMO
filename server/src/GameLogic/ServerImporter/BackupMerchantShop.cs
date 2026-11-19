// <copyright file="BackupMerchantShop.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>Saved merchant store snapshot before shop import.</summary>
public sealed class BackupMerchantShop
{
    /// <summary>Gets or sets merchant NPC monster number.</summary>
    public int NpcNumber { get; set; }

    /// <summary>Gets or sets whether a <c>MerchantStore</c> existed before import.</summary>
    public bool HadMerchantStore { get; set; }

    /// <summary>Gets or sets previous items.</summary>
    public List<BackupItemSlot> PreviousItems { get; set; } = [];
}
