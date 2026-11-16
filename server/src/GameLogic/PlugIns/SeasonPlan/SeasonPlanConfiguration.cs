// <copyright file="SeasonPlanConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonPlan;

using System.ComponentModel.DataAnnotations;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Gameplay features gated by Season Plan stages until their stage unlocks.
/// </summary>
public enum SeasonFeatureGate
{
    /// <summary>Upgrading items beyond +9 (+10..+15 in Chaos Machine).</summary>
    ItemPlus10To15 = 1,

    /// <summary>Master experience gain and Master Level progression.</summary>
    MasterLevels = 2,

    /// <summary>Jewel of Harmony and Refining Stones.</summary>
    Harmony = 3,

    /// <summary>Level 2 Wings Chaos Mix.</summary>
    Wing2 = 4,

    /// <summary>Horn of Fenrir (the wolf) Chaos Mix.</summary>
    Fenrir = 5,

    /// <summary>Level 380 items &amp; Guardian Chaos option.</summary>
    Items380 = 6,

    /// <summary>Level 3 Wings Chaos Mix.</summary>
    Wing3 = 7,
}

/// <summary>
/// One progression stage in a server's Season Plan.
/// A stage opens automatically when its date is reached OR when its boss/player-level condition is met (or when opened by an admin).
/// </summary>
public sealed class SeasonStageDefinition
{
    /// <summary>Gets or sets unique stage ID.</summary>
    [StringLength(32)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets English stage title.</summary>
    [StringLength(80)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese stage title.</summary>
    [StringLength(80)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets English description shown on website and F8 window.</summary>
    [StringLength(240)]
    public string DescriptionEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese description shown on website and F8 window.</summary>
    [StringLength(240)]
    public string DescriptionVi { get; set; } = string.Empty;

    /// <summary>Gets or sets feature gates unlocked by this stage.</summary>
    public List<SeasonFeatureGate> Unlocks { get; set; } = [];

    /// <summary>Gets or sets optional scheduled UTC time when this stage automatically unlocks.</summary>
    public DateTime? UnlockAtUtc { get; set; }

    /// <summary>Gets or sets optional World Boss ID (e.g. <c>red-dragon</c>, <c>kundun</c>) whose kill unlocks this stage.</summary>
    [StringLength(64)]
    public string RequiredBossId { get; set; } = string.Empty;

    /// <summary>Gets or sets how many kills of <see cref="RequiredBossId"/> are needed (default 1).</summary>
    [Range(0, 1000)]
    public int RequiredBossKills { get; set; } = 1;

    /// <summary>Gets or sets optional target character level that <see cref="RequiredPlayersCount"/> distinct players must reach.</summary>
    [Range(0, 1000)]
    public int RequiredPlayerLevel { get; set; }

    /// <summary>Gets or sets how many distinct players must reach <see cref="RequiredPlayerLevel"/>.</summary>
    [Range(0, 10000)]
    public int RequiredPlayersCount { get; set; }

    /// <summary>Gets or sets optional max lucky/leftover box level unlocked at this stage (for combining leftovers).</summary>
    [Range(0, 15)]
    public int MaxBoxLevel { get; set; }
}

/// <summary>
/// Configuration for <see cref="SeasonPlanPlugIn"/> (OFF by default).
/// </summary>
public sealed class SeasonPlanConfiguration
{
    /// <summary>Gets or sets shared HNMO state file key.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets time zone for display and date scheduling.</summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets a value indicating whether unlocking a stage broadcasts an in-game announcement.</summary>
    public bool AnnounceInGame { get; set; } = true;

    /// <summary>Gets or sets ordered Season Plan stages.</summary>
    public List<SeasonStageDefinition> Stages { get; set; } = DefaultStages();

    /// <summary>Creates the canonical 7-stage progressive season template matching ROADMAP 13 item 12.</summary>
    /// <returns>Default stage list.</returns>
    public static List<SeasonStageDefinition> DefaultStages() =>
    [
        new()
        {
            Id = "stage-1-plus9",
            NameEn = "Stage 1: Classic Launch (+9 Max, Wing 1)",
            NameVi = "Giai đoạn 1: Khởi đầu cổ điển (Tối đa +9, Cánh C1)",
            DescriptionEn = "Opening phase: items capped at +9, classic farming and Chaos Weapon / Wing 1 crafting.",
            DescriptionVi = "Giai đoạn mở màn: giới hạn ép đồ +9, cày cuốc cổ điển và xoay Cánh cấp 1.",
            Unlocks = [],
            UnlockAtUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            MaxBoxLevel = 2,
        },
        new()
        {
            Id = "stage-2-plus15-master",
            NameEn = "Stage 2: +10..+15 & Master Levels",
            NameVi = "Giai đoạn 2: Ép +10..+15 & Cấp Master",
            DescriptionEn = "Unlocks +10 to +15 Chaos upgrading and Master experience.",
            DescriptionVi = "Mở khóa nâng cấp đồ +10..+15 và cày cấp Master.",
            Unlocks = [SeasonFeatureGate.ItemPlus10To15, SeasonFeatureGate.MasterLevels],
            RequiredPlayerLevel = 400,
            RequiredPlayersCount = 10,
            MaxBoxLevel = 3,
        },
        new()
        {
            Id = "stage-3-harmony",
            NameEn = "Stage 3: Jewel of Harmony",
            NameVi = "Giai đoạn 3: Đá Tạo Hóa (Harmony)",
            DescriptionEn = "Unlocks Refining Stones and Jewel of Harmony equipment options.",
            DescriptionVi = "Mở khóa ép Đá tinh luyện và gia cường Đá Tạo Hóa.",
            Unlocks = [SeasonFeatureGate.Harmony],
            MaxBoxLevel = 3,
        },
        new()
        {
            Id = "stage-4-wing2",
            NameEn = "Stage 4: Second Wings",
            NameVi = "Giai đoạn 4: Cánh Cấp 2",
            DescriptionEn = "Unlocks Level 2 Wings Chaos Machine crafting.",
            DescriptionVi = "Mở khóa xoay Cánh cấp 2 tại Chaos Goblin.",
            Unlocks = [SeasonFeatureGate.Wing2],
            MaxBoxLevel = 4,
        },
        new()
        {
            Id = "stage-5-fenrir",
            NameEn = "Stage 5: Horn of Fenrir (The Wolf)",
            NameVi = "Giai đoạn 5: Sói Tinh (Fenrir)",
            DescriptionEn = "Unlocks Fenrir crafting and upgrades.",
            DescriptionVi = "Mở khóa chế tạo và nâng cấp Sói Tinh.",
            Unlocks = [SeasonFeatureGate.Fenrir],
            MaxBoxLevel = 4,
        },
        new()
        {
            Id = "stage-6-380",
            NameEn = "Stage 6: 380 PvP Items",
            NameVi = "Giai đoạn 6: Đồ 380 PvP",
            DescriptionEn = "Unlocks Level 380 Guardian Chaos Machine option crafting.",
            DescriptionVi = "Mở khóa gia cường dòng tím đồ 380.",
            Unlocks = [SeasonFeatureGate.Items380],
            MaxBoxLevel = 5,
        },
        new()
        {
            Id = "stage-7-wing3",
            NameEn = "Stage 7: Third Wings",
            NameVi = "Giai đoạn 7: Cánh Cấp 3",
            DescriptionEn = "Unlocks Level 3 Wings Chaos Machine crafting.",
            DescriptionVi = "Mở khóa xoay Cánh cấp 3.",
            Unlocks = [SeasonFeatureGate.Wing3],
            MaxBoxLevel = 5,
        },
    ];
}
