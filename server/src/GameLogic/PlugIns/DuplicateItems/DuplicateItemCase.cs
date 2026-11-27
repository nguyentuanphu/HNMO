// <copyright file="DuplicateItemCase.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.DuplicateItems;

/// <summary>
/// Items found with one serial: frozen (while <see cref="Resolved"/> is <c>false</c>) until an administrator decides.
/// </summary>
public sealed class DuplicateItemCase
{
    /// <summary>Gets or sets the serial the items share.</summary>
    public Guid Serial { get; set; }

    /// <summary>Gets or sets when the scan found them.</summary>
    public DateTime FoundAtUtc { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public List<DuplicateItemCaseEntry> Items { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether an administrator has decided (the items aren't frozen then).</summary>
    public bool Resolved { get; set; }

    /// <summary>Gets or sets when it was decided.</summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Gets or sets who decided.</summary>
    public string ResolvedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the administrator's note.</summary>
    public string Note { get; set; } = string.Empty;
}
