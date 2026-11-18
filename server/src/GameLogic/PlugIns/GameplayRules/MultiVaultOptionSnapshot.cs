// <copyright file="MultiVaultOptionSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// Serialized item option snapshot for secondary vault storage (ROADMAP 14.5).
/// </summary>
public sealed class MultiVaultOptionSnapshot
{
    /// <summary>Gets or sets the option definition ID.</summary>
    public Guid OptionId { get; set; }

    /// <summary>Gets or sets the option level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the option index.</summary>
    public int Index { get; set; }
}
