// <copyright file="ArenaShopPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// The HNMO Arena points shop on the players' home servers (owner's choice 2026-11-20): arena points, earned in the
/// Arena's ranked duels and kept in the shared database, buy items which are made new here. Not in the Arena itself.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.ArenaShopPlugIn_Name), Description = nameof(Properties.PlugInResources.ArenaShopPlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("2B6F9D41-7E83-4C25-A9D0-5F1C8E3B6A72")]
public sealed class ArenaShopPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<ArenaShopConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public ArenaShopConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="ArenaShopPlugIn"/> of a game context.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in, or <c>null</c>.</returns>
    public static ArenaShopPlugIn? Get(IGameContext? gameContext) => gameContext?.FeaturePlugIns.GetPlugIn<ArenaShopPlugIn>();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ArenaShopConfiguration();

    /// <summary>The shop's part of the Arena window's state: the account's points and the items.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The values to put in the JSON, or <c>null</c> when there's no shop (the Arena, or no shared database).</returns>
    public object? StateOf(Player player)
    {
        if (ArenaRules.Active || ArenaShared.Current is not { } shared || player.Account?.LoginName is not { Length: > 0 } login)
        {
            return null;
        }

        int points;
        try
        {
            points = shared.GetPoints(login);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Arena shop: the points couldn't be read.");
            return null;
        }

        var items = (this.Configuration ?? new ArenaShopConfiguration()).Items;
        return new
        {
            points,
            items = items.Select((item, index) => new { index, item.Category, item.NameEn, item.NameVi, item.Price, item.Group, item.Number, item.Level }).ToList(),
        };
    }

    /// <summary>Buys an item: takes the points (in one step in the shared database) and puts the new item in the bag.</summary>
    /// <param name="player">The player.</param>
    /// <param name="index">The item's index in the shop.</param>
    /// <returns>The EN and VI message.</returns>
    public async ValueTask<(string En, string Vi)> BuyAsync(Player player, int index)
    {
        var items = (this.Configuration ?? new ArenaShopConfiguration()).Items;
        if (ArenaRules.Active || ArenaShared.Current is not { } shared || player.Account?.LoginName is not { Length: > 0 } login || player.Inventory is not { } inventory)
        {
            return ("The Arena shop isn't available here.", "Cửa hàng Đấu trường không có ở đây.");
        }

        if (index < 0 || index >= items.Count)
        {
            return ("That item isn't in the shop.", "Cửa hàng không có món này.");
        }

        var entry = items[index];
        if (player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == entry.Group && d.Number == entry.Number) is not { } definition)
        {
            return ("That item isn't in this server's game data.", "Máy chủ không có món này.");
        }

        var temporary = GameMasterItemFactory.CreateItem(definition, new ItemSpecification(entry.Group, entry.Number, Math.Min(entry.Level, definition.MaximumItemLevel)));
        if (inventory.CheckInvSpace(temporary) is not { } slot)
        {
            return ("Free some bag space first.", "Hãy dọn chỗ trong túi đồ trước.");
        }

        if (!shared.TrySpendPoints(login, entry.Price))
        {
            return ($"{entry.NameEn} costs {entry.Price} arena points.", $"{entry.NameVi} cần {entry.Price} điểm Đấu trường.");
        }

        var item = temporary.MakePersistent(player.PersistenceContext);
        if (!await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
        {
            player.PersistenceContext.Detach(item);
            shared.RefundPoints(login, entry.Price);
            return ("The bag changed; nothing was bought.", "Túi đồ vừa thay đổi; chưa mua gì.");
        }

        if (!await player.SaveProgressAsync().ConfigureAwait(false))
        {
            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            player.PersistenceContext.Detach(item);
            shared.RefundPoints(login, entry.Price);
            return ("The purchase couldn't be saved; your points are back.", "Không lưu được giao dịch; điểm đã được hoàn lại.");
        }

        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        player.Logger.LogInformation("Arena shop: {Name} bought {Item} for {Price} arena points.", player.Name, entry.NameEn, entry.Price);
        return ($"Bought {entry.NameEn} for {entry.Price} arena points.", $"Đã mua {entry.NameVi} với {entry.Price} điểm Đấu trường.");
    }
}
