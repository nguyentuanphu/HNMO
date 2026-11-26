// <copyright file="JewelBankStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.IO;
using System.Text.Json;

/// <summary>
/// Serializable state stored under section <c>jewelBank</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class JewelBankStoreData
{
    /// <summary>Gets or sets account balances keyed by login name -> jewel key -> count.</summary>
    public Dictionary<string, Dictionary<string, int>> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets pending website withdrawals.</summary>
    public List<JewelBankPendingWithdrawal> PendingWithdrawals { get; set; } = [];

    /// <summary>Gets or sets the in-game deposits and withdrawals whose bag save isn't confirmed yet.</summary>
    public List<JewelBankPendingOperation> PendingOperations { get; set; } = [];

    /// <summary>Gets or sets newest-first log entries.</summary>
    public List<JewelBankLogEntry> Log { get; set; } = [];
}
