// <copyright file="ResetLogEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// A reset or relife (the admin panel's log).
/// </summary>
public sealed class ResetLogEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the character's id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets "reset" or "relife".</summary>
    public string Kind { get; set; } = "reset";

    /// <summary>Gets or sets where it was done.</summary>
    public ResetSource Source { get; set; }

    /// <summary>Gets or sets the level before.</summary>
    public int LevelBefore { get; set; }

    /// <summary>Gets or sets the resets after.</summary>
    public int ResetsAfter { get; set; }

    /// <summary>Gets or sets the relifes after.</summary>
    public int RelifesAfter { get; set; }

    /// <summary>Gets or sets the zen paid.</summary>
    public long Zen { get; set; }

    /// <summary>Gets or sets the coins paid (every coin type together; see <see cref="CoinsText"/>).</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coins paid by type, e.g. "10 GoblinPoints".</summary>
    public string CoinsText { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the VIP advantages counted.</summary>
    public bool Vip { get; set; }
}
