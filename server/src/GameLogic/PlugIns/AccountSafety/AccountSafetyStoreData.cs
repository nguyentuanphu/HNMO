// <copyright file="AccountSafetyStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// The saved state of the account safety, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class AccountSafetyStoreData
{
    /// <summary>Gets or sets the accounts, by login name in lower case.</summary>
    public Dictionary<string, SafetyAccountRecord> Accounts { get; set; } = new(StringComparer.Ordinal);
}
