// <copyright file="LegacyVaultRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Warehouse row read from <c>warehouse</c>.</summary>
public sealed class LegacyVaultRow
{
    /// <summary>Gets or sets owning account ID.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets vault zen.</summary>
    public int Money { get; set; }

    /// <summary>Gets or sets raw vault binary blob.</summary>
    public byte[] ItemsBlob { get; set; } = [];
}
