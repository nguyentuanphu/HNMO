// <copyright file="LegacyAccountRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Account row read from <c>MEMB_INFO</c> (without reading password plaintext/hashes).</summary>
public sealed class LegacyAccountRow
{
    /// <summary>Gets or sets login name (<c>memb___id</c>).</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets email address (<c>mail_addr</c>).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the account is blocked (<c>bloc_code</c>).</summary>
    public bool IsBanned { get; set; }
}
