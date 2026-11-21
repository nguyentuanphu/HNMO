// <copyright file="ArenaShopConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>The configuration of the <see cref="ArenaShopPlugIn"/>: what arena points buy on the home servers.</summary>
public sealed class ArenaShopConfiguration
{
    /// <summary>Gets or sets the items (owner's choice 2026-11-20: jewels, charms, event tickets and cosmetics; no power gear).</summary>
    public List<ArenaShopItem> Items { get; set; } = DefaultItems();

    /// <summary>The default items.</summary>
    /// <returns>The items.</returns>
    public static List<ArenaShopItem> DefaultItems() =>
    [
        new() { Category = "jewels", Group = 14, Number = 13, Price = 50, NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện" },
        new() { Category = "jewels", Group = 14, Number = 14, Price = 80, NameEn = "Jewel of Soul", NameVi = "Ngọc Tâm Linh" },
        new() { Category = "jewels", Group = 12, Number = 15, Price = 60, NameEn = "Jewel of Chaos", NameVi = "Ngọc Hỗn Nguyên" },
        new() { Category = "jewels", Group = 14, Number = 16, Price = 120, NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh" },
        new() { Category = "jewels", Group = 14, Number = 22, Price = 150, NameEn = "Jewel of Creation", NameVi = "Ngọc Sáng Tạo" },
        new() { Category = "charms", Group = 14, Number = 53, Price = 200, NameEn = "Talisman of Luck", NameVi = "Bùa May Mắn" },
        new() { Category = "charms", Group = 14, Number = 96, Price = 250, NameEn = "Talisman of Chaos Assembly", NameVi = "Bùa Hỗn Nguyên" },
        new() { Category = "charms", Group = 13, Number = 82, Price = 300, NameEn = "Talisman of Item Protection", NameVi = "Bùa Bảo Vệ Vật Phẩm" },
        new() { Category = "tickets", Group = 13, Number = 47, Price = 40, NameEn = "Blood Castle Ticket", NameVi = "Vé Huyết Lâu" },
        new() { Category = "tickets", Group = 14, Number = 19, Price = 40, NameEn = "Devil's Invitation", NameVi = "Thư Mời Quảng Trường Quỷ" },
        new() { Category = "tickets", Group = 13, Number = 29, Price = 40, NameEn = "Armor of Guardsman", NameVi = "Áo Giáp Vệ Binh" },
        new() { Category = "cosmetic", Group = 14, Number = 63, Price = 10, NameEn = "Firecracker", NameVi = "Pháo Hoa" },
        new() { Category = "cosmetic", Group = 14, Number = 99, Price = 10, NameEn = "Christmas Firecracker", NameVi = "Pháo Hoa Giáng Sinh" },
    ];
}
