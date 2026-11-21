// <copyright file="LegacyGuildRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Guild row read from <c>Guild</c>.</summary>
public sealed class LegacyGuildRow
{
    /// <summary>Gets or sets guild name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets guild master character name.</summary>
    public string MasterName { get; set; } = string.Empty;

    /// <summary>Gets or sets 32-byte guild emblem bitmap.</summary>
    public byte[] Logo { get; set; } = [];

    /// <summary>Gets or sets guild score.</summary>
    public int Score { get; set; }

    /// <summary>Gets or sets guild notice text.</summary>
    public string Notice { get; set; } = string.Empty;
}
