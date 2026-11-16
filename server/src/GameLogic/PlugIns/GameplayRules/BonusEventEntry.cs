// <copyright file="BonusEventEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Scheduled bonus event ("happy hour") definition evaluated in Sydney time (ROADMAP 14.2).
/// </summary>
public sealed class BonusEventEntry
{
    /// <summary>Gets or sets unique event ID.</summary>
    [StringLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Gets or sets English event title.</summary>
    [StringLength(80)]
    public string NameEn { get; set; } = "Happy Hour";

    /// <summary>Gets or sets Vietnamese event title.</summary>
    [StringLength(80)]
    public string NameVi { get; set; } = "Giờ Vàng";

    /// <summary>Gets or sets optional target map number; <c>null</c> applies to all maps.</summary>
    public short? MapNumber { get; set; }

    /// <summary>Gets or sets daily start time in Sydney local time (<c>HH:mm</c>).</summary>
    [StringLength(10)]
    public string StartTimeSydney { get; set; } = "20:00";

    /// <summary>Gets or sets event duration in minutes.</summary>
    [Range(1, 1440)]
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Gets or sets EXP bonus percentage while active.</summary>
    [Range(-90, 2000)]
    public int ExpBonusPercent { get; set; } = 50;

    /// <summary>Gets or sets item drop bonus percentage while active.</summary>
    [Range(-90, 2000)]
    public int ItemDropBonusPercent { get; set; } = 25;

    /// <summary>Gets or sets zen drop bonus percentage while active.</summary>
    [Range(-90, 2000)]
    public int ZenDropBonusPercent { get; set; } = 25;

    /// <summary>Gets or sets a value indicating whether this scheduled rule is enabled.</summary>
    public bool Enabled { get; set; } = true;
}
