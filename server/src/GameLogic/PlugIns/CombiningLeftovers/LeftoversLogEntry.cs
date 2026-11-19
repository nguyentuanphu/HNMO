// <copyright file="LeftoversLogEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>Audit entry for leftover combines and exchanges.</summary>
public sealed class LeftoversLogEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets actor login.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets action (<c>Combine</c>, <c>CombineFail</c>, <c>Exchange</c>).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets recipe or offer identifier.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Gets or sets display summary.</summary>
    public string Detail { get; set; } = string.Empty;
}
