// <copyright file="MultiVaultAccountState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// Per-account active vault index and purchased extra vault count (ROADMAP 14.5).
/// </summary>
public sealed class MultiVaultAccountState
{
    /// <summary>Gets or sets active vault index (0-based).</summary>
    public int ActiveVaultIndex { get; set; }

    /// <summary>Gets or sets number of extra vaults permanently bought with coins.</summary>
    public int PurchasedExtraVaults { get; set; }
}
