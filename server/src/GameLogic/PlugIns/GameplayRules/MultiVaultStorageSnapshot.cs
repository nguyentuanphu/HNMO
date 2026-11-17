// <copyright file="MultiVaultStorageSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// Serialized contents (zen and items) of a single numbered vault slot (ROADMAP 14.5).
/// </summary>
public sealed class MultiVaultStorageSnapshot
{
    /// <summary>Gets or sets stored zen.</summary>
    public int Money { get; set; }

    /// <summary>Gets or sets stored items.</summary>
    public List<MultiVaultItemSnapshot> Items { get; set; } = [];
}
