// <copyright file="GmCommandAuditEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GmManager;

/// <summary>Audit entry recorded for every in-game GM command invocation (allowed or denied).</summary>
public sealed class GmCommandAuditEntry
{
    /// <summary>Gets or sets the UTC timestamp.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>Gets or sets the invoking GM character name.</summary>
    public string GmCharacter { get; set; } = string.Empty;

    /// <summary>Gets or sets the invoking GM account login name.</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>Gets or sets the command key (e.g. /goldnotice).</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Gets or sets the full command text (truncated cleanly).</summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>Gets or sets the required permission flag.</summary>
    public GmPermission RequiredPermission { get; set; }

    /// <summary>Gets or sets a value indicating whether execution was permitted.</summary>
    public bool Allowed { get; set; }

    /// <summary>Gets or sets a short outcome note.</summary>
    public string Outcome { get; set; } = string.Empty;
}
