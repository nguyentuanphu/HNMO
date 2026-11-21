// <copyright file="ItemStackConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemStacks;

using HNMO.DataModel.Configuration.Items;

/// <summary>
/// The configuration of <see cref="ItemStackPlugIn"/>: which items stack, how many in one slot, and which kinds an account
/// unlocks with coins (owner's choice, 2026-11-20: potions, tickets and keys free; jewels, boxes and quest items, and
/// crafting materials after an unlock of 500 WCoin (C) each; up to 255 in one slot, in the vault and the bag).
/// </summary>
public sealed class ItemStackConfiguration
{
    /// <summary>Gets or sets a value indicating whether HNMO's stack rules apply (off: the items' own limits).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin which pays for unlocks.</summary>
    public CashShopCoinType UnlockCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the kinds.</summary>
    public List<ItemStackKind> Kinds { get; set; } = CreateDefaultKinds();

    /// <summary>Gets or sets the shared state file (key of the row backend which keeps the accounts' unlocks).</summary>
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>The default kinds.</summary>
    /// <returns>The kinds.</returns>
    public static List<ItemStackKind> CreateDefaultKinds() =>
    [
        new()
        {
            Key = "potions", NameEn = "Potions and scrolls", NameVi = "Bình thuốc và cuộn giấy", Free = true,
            Items = ["14:0-10", "14:35-40", "14:94"],
        },
        new()
        {
            Key = "tickets", NameEn = "Event tickets and keys", NameVi = "Vé và chìa khoá sự kiện", Free = true,
            Items = ["14:17-19", "13:16-18", "13:29", "13:49-51", "14:28-29", "14:101-111"],
        },
        new()
        {
            Key = "jewels", NameEn = "Jewels", NameVi = "Ngọc", UnlockPrice = 500,
            Items = ["14:13-14", "14:16", "14:22", "14:31", "12:15", "14:41-44"],
        },
        new()
        {
            Key = "boxes", NameEn = "Boxes and quest items", NameVi = "Hộp và vật phẩm nhiệm vụ", UnlockPrice = 500,
            Items = ["14:11-12", "14:20-21", "14:23-26", "12:32-34", "14:32-34", "14:45-52", "14:54-59", "14:85-93", "14:95-100"],
        },
        new()
        {
            Key = "materials", NameEn = "Crafting materials", NameVi = "Nguyên liệu chế tạo", UnlockPrice = 500,
            Items = ["13:14", "13:32-36", "13:52-53", "12:26", "14:53"],
        },
    ];
}
