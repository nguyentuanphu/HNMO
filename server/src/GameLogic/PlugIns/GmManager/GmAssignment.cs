// <copyright file="GmAssignment.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GmManager;

/// <summary>Persisted GM permission assignment for a character.</summary>
public sealed class GmAssignment
{
    /// <summary>Gets or sets the canonical character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning account login name.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the assigned GM permission flags.</summary>
    public GmPermission Permissions { get; set; }

    /// <summary>Gets or sets who last updated the assignment.</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Gets or sets the reason or notes for this assignment.</summary>
    public string Notes { get; set; } = string.Empty;
}
