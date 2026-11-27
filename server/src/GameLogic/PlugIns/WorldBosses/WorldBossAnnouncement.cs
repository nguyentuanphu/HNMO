// <copyright file="WorldBossAnnouncement.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// An announcement of a world boss (shown on the website).
/// </summary>
public sealed class WorldBossAnnouncement
{
    /// <summary>Gets or sets when it was made.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the boss id.</summary>
    public string BossId { get; set; } = string.Empty;

    /// <summary>Gets or sets the phase: "soon", "spawn", "kill" or "escape".</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Gets or sets the English text.</summary>
    public string TextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text.</summary>
    public string TextVi { get; set; } = string.Empty;
}
