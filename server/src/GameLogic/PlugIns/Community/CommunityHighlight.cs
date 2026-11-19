// <copyright file="CommunityHighlight.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// An automatic post (a highlight): what happened, in both languages.
/// </summary>
public sealed class CommunityHighlight
{
    /// <summary>Gets or sets when it happened.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the kind.</summary>
    public CommunityPostKind Kind { get; set; }

    /// <summary>Gets or sets the English text.</summary>
    public string TextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text.</summary>
    public string TextVi { get; set; } = string.Empty;
}
