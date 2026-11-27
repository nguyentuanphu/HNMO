// <copyright file="QolConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Qol;

using System.Collections.Generic;

/// <summary>
/// Configuration for <see cref="QolPlugIn"/> (disabled by default):
/// <c>/requests on|off</c>, <c>/srvinfo</c>, and <c>/mine</c> excavation mini-system.
/// </summary>
public sealed class QolConfiguration
{
    /// <summary>Gets or sets a value indicating whether QoL plug-in features are active.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets save state file path.</summary>
    public string FileName { get; set; } = "save/hnmo-qol.json";

    /// <summary>Gets or sets a value indicating whether <c>/requests on|off</c> is enabled.</summary>
    public bool RequestsToggleEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether <c>/srvinfo</c> is enabled.</summary>
    public bool ServerInfoEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether <c>/mine</c> excavation is enabled.</summary>
    public bool MiningEnabled { get; set; }

    /// <summary>Gets or sets cooldown seconds between mining attempts (3..3600).</summary>
    public int MiningCooldownSeconds { get; set; } = 15;

    /// <summary>Gets or sets required mining pick item group (-1 = no item required).</summary>
    public int MiningRequiredItemGroup { get; set; } = -1;

    /// <summary>Gets or sets required mining pick item number.</summary>
    public short MiningRequiredItemNumber { get; set; }

    /// <summary>Gets or sets allowed map numbers for mining (empty = all maps).</summary>
    public List<short> MiningAllowedMapNumbers { get; set; } = [0, 2, 3];

    /// <summary>Gets or sets weighted reward table for <c>/mine</c>.</summary>
    public List<MiningRewardEntry> MiningRewards { get; set; } = CreateDefaultMiningRewards();

    /// <summary>Creates default excavation mineral rewards (Zen + Jewels).</summary>
    /// <returns>Initialized list of weighted outcomes.</returns>
    public static List<MiningRewardEntry> CreateDefaultMiningRewards() =>
    [
        new() { Weight = 55, NameEn = "Small Gold Ore (50,000 Zen)", NameVi = "Quặng Vàng Nhỏ (50,000 Zen)", Zen = 50_000 },
        new() { Weight = 20, NameEn = "Rich Ore Vein (200,000 Zen)", NameVi = "Mạch Quặng Lớn (200,000 Zen)", Zen = 200_000 },
        new() { Weight = 10, NameEn = "Jewel of Chaos", NameVi = "Ngọc Hỗn Nguyên (Chaos)", ItemGroup = 12, ItemNumber = 15 },
        new() { Weight = 8, NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện (Bless)", ItemGroup = 14, ItemNumber = 13 },
        new() { Weight = 5, NameEn = "Jewel of Soul", NameVi = "Ngọc Tâm Linh (Soul)", ItemGroup = 14, ItemNumber = 14 },
        new() { Weight = 2, NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh (Life)", ItemGroup = 14, ItemNumber = 16 },
    ];
}
