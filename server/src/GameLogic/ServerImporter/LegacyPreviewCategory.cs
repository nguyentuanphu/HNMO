// <copyright file="LegacyPreviewCategory.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>
/// Summary of a single category in a dry-run preview or completed import.
/// </summary>
public sealed class LegacyPreviewCategory
{
    /// <summary>Gets or sets the English category title.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese category title.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets how many entries map cleanly into HNMO.</summary>
    public int MappedCount { get; set; }

    /// <summary>Gets or sets how many entries cannot be mapped or are skipped.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Gets or sets brief change details (before -> after or counts).</summary>
    public List<string> Details { get; set; } = [];
}
