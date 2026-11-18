// <copyright file="ServerSettingsModel.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>
/// Plain-language editable server settings model rendered on <c>/servers</c>.
/// </summary>
public sealed class ServerSettingsModel
{
    /// <summary>Gets or sets the zero-based server identifier.</summary>
    public byte ServerId { get; set; }

    /// <summary>Gets or sets the server display name.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether active VIP access (<c>Stats.IsVip</c>) is required to join.</summary>
    public bool VipOnly { get; set; }

    /// <summary>Gets or sets the server experience rate multiplier.</summary>
    public float ExperienceRate { get; set; } = 1.0f;

    /// <summary>Gets or sets the per-server item drop rate multiplier.</summary>
    public float DropRate { get; set; } = 1.0f;

    /// <summary>Gets or sets the per-server Zen drop amount multiplier.</summary>
    public float ZenRate { get; set; } = 1.0f;

    /// <summary>Gets or sets a value indicating whether PvP combat is enabled.</summary>
    public bool PvpEnabled { get; set; } = true;

    /// <summary>Gets or sets the maximum number of concurrent players allowed.</summary>
    public short MaximumPlayers { get; set; } = 100;

    /// <summary>Gets or sets the current connected player count.</summary>
    public int OnlinePlayers { get; set; }
}
