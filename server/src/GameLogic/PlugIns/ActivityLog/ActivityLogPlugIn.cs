// <copyright file="ActivityLogPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views.NPC;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Records trades, valuable drops/pickups, Chaos mixes, NPC buys/sells, personal store sales, and
/// external Item Market actions into individual PostgreSQL rows (ROADMAP 14.9).
/// </summary>
[PlugIn]
[Display(Name = "Player activity log", Description = "Records trades, valuable drops/pickups, Chaos mixes, NPC shops, personal store, and Item Market actions.")]
[Guid("6E2B8D14-4C93-4F81-9A70-5D3E1C8A2B04")]
public sealed class ActivityLogPlugIn :
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<ActivityLogConfiguration>,
    ISupportDefaultCustomConfiguration
{
    private DateTime _lastArchiveUtc = DateTime.MinValue;

    /// <inheritdoc />
    public ActivityLogConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="ActivityLogPlugIn"/> for <paramref name="gameContext"/>.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static ActivityLogPlugIn? Get(IGameContext? gameContext) =>
        gameContext?.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<ActivityLogPlugIn>().FirstOrDefault();

    /// <summary>
    /// Determines whether an item should be treated as valuable for drop/pickup logging.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="minLevel">Minimum upgrade level considered valuable for standard items.</param>
    /// <returns><c>true</c> if valuable.</returns>
    public static bool IsValuableItem(Item? item, int minLevel = 7)
    {
        if (item?.Definition is null)
        {
            return false;
        }

        if (item.IsExcellent() || item.IsAncient() || item.SocketCount > 0 || item.Level >= minLevel)
        {
            return true;
        }

        // Wings/cloaks/jewels (group 12), special pets/rings/tickets (group 13 with options/level).
        if (item.Definition.Group == 12)
        {
            return true;
        }

        var name = item.Definition.Name.ValueInNeutralLanguage ?? string.Empty;
        return name.Contains("Jewel", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Box", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Fenrir", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Kondor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Feather", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Gemstone", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Talisman", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Allows companion plug-ins (such as Item Market 13.9) to append an activity entry cleanly.
    /// </summary>
    /// <param name="entry">The activity entry.</param>
    /// <param name="fileName">Optional state store key.</param>
    /// <param name="retentionDays">Retention days.</param>
    public static void RecordExternal(ActivityLogEntry entry, string? fileName = null, int retentionDays = 30) =>
        ActivityLogStore.Open(fileName).Append(entry, retentionDays);

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ActivityLogConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._lastArchiveUtc = DateTime.MinValue;

    /// <inheritdoc />
    public ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var cfg = this.Configuration ?? new ActivityLogConfiguration();
        var now = DateTime.UtcNow;
        if (!cfg.Enabled || now - this._lastArchiveUtc < TimeSpan.FromHours(6))
        {
            return ValueTask.CompletedTask;
        }

        this._lastArchiveUtc = now;
        ActivityLogStore.Open(cfg.FileName).ArchiveExpired(now);
        return ValueTask.CompletedTask;
    }

    /// <summary>Records a completed direct player trade.</summary>
    /// <param name="trader">Initiating trader.</param>
    /// <param name="partner">Partner trader.</param>
    /// <param name="traderItems">Items given by <paramref name="trader"/>.</param>
    /// <param name="partnerItems">Items given by <paramref name="partner"/>.</param>
    /// <param name="traderZen">Zen offered by <paramref name="trader"/>.</param>
    /// <param name="partnerZen">Zen offered by <paramref name="partner"/>.</param>
    public void RecordTrade(ITrader trader, ITrader partner, IReadOnlyList<Item> traderItems, IReadOnlyList<Item> partnerItems, long traderZen, long partnerZen)
    {
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled)
        {
            return;
        }

        var gaveSummary = SummarizeItems(traderItems);
        var gotSummary = SummarizeItems(partnerItems);
        var allIds = JoinItemIds(traderItems.Concat(partnerItems));
        var summary = $"Gave: {(string.IsNullOrEmpty(gaveSummary) ? "-" : gaveSummary)} | Got: {(string.IsNullOrEmpty(gotSummary) ? "-" : gotSummary)}";

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.Trade,
            Account = LoginOf(trader),
            Character = trader.Name,
            PartnerAccount = LoginOf(partner),
            PartnerCharacter = partner.Name,
            ItemSummary = summary,
            ItemIds = allIds,
            ZenDelta = partnerZen - traderZen,
            Details = $"ZenGave={traderZen};ZenGot={partnerZen}",
        });
    }

    /// <summary>Records a personal store sale/purchase.</summary>
    /// <param name="seller">Store owner.</param>
    /// <param name="buyer">Buyer.</param>
    /// <param name="item">Sold item.</param>
    /// <param name="zen">Zen price if paid in zen.</param>
    /// <param name="otherPrice">Formatted alternate currency price.</param>
    public void RecordPersonalStore(Player seller, Player buyer, Item item, long zen, string? otherPrice)
    {
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled)
        {
            return;
        }

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.PersonalStore,
            Account = LoginOf(seller),
            Character = seller.Name,
            PartnerAccount = LoginOf(buyer),
            PartnerCharacter = buyer.Name,
            ItemSummary = AccountSafetyPlugIn.Describe(item),
            ItemIds = ItemIdString(item),
            ZenDelta = zen,
            Details = string.IsNullOrEmpty(otherPrice) ? "Currency=Zen" : $"Price={otherPrice}",
        });
    }

    /// <summary>Records an item drop onto the floor (filtered by value when configured).</summary>
    /// <param name="player">Dropping player.</param>
    /// <param name="item">Dropped item.</param>
    /// <param name="mapName">Map name.</param>
    public void RecordDrop(Player player, Item item, string mapName)
    {
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled || (cfg.OnlyValuableDropsAndPickups && !IsValuableItem(item, cfg.MinValuableDropLevel)))
        {
            return;
        }

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.Drop,
            Account = LoginOf(player),
            Character = player.Name,
            ItemSummary = AccountSafetyPlugIn.Describe(item),
            ItemIds = ItemIdString(item),
            Details = $"Map={mapName}",
        });
    }

    /// <summary>Records an item pickup from the floor (filtered by value when configured).</summary>
    /// <param name="player">Picking player.</param>
    /// <param name="item">Picked-up or target stacked item.</param>
    /// <param name="mapName">Map name.</param>
    public void RecordPickup(Player player, Item item, string mapName)
    {
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled || (cfg.OnlyValuableDropsAndPickups && !IsValuableItem(item, cfg.MinValuableDropLevel)))
        {
            return;
        }

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.Pickup,
            Account = LoginOf(player),
            Character = player.Name,
            ItemSummary = AccountSafetyPlugIn.Describe(item),
            ItemIds = ItemIdString(item),
            Details = $"Map={mapName}",
        });
    }

    /// <summary>Records a Chaos Machine or NPC crafting attempt and output.</summary>
    /// <param name="player">Crafter.</param>
    /// <param name="result">Crafting result.</param>
    /// <param name="resultItem">Created output item, if any.</param>
    /// <param name="npcName">Crafting NPC or mix name.</param>
    public void RecordChaosMix(Player player, CraftingResult result, Item? resultItem, string npcName)
    {
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled)
        {
            return;
        }

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.ChaosMix,
            Account = LoginOf(player),
            Character = player.Name,
            PartnerCharacter = npcName,
            ItemSummary = resultItem is not null ? AccountSafetyPlugIn.Describe(resultItem) : result.ToString(),
            ItemIds = resultItem is not null ? ItemIdString(resultItem) : string.Empty,
            Details = $"Result={result}",
        });
    }

    /// <summary>Records a purchase from an NPC merchant.</summary>
    /// <param name="player">Buyer.</param>
    /// <param name="item">Purchased item.</param>
    /// <param name="npcName">Merchant NPC name.</param>
    /// <param name="price">Zen paid.</param>
    public void RecordNpcBuy(Player player, Item item, string npcName, long price)
    {
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled)
        {
            return;
        }

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.NpcBuy,
            Account = LoginOf(player),
            Character = player.Name,
            PartnerCharacter = npcName,
            ItemSummary = AccountSafetyPlugIn.Describe(item),
            ItemIds = ItemIdString(item),
            ZenDelta = -price,
            Details = $"Npc={npcName}",
        });
    }

    /// <summary>Records a sale to an NPC merchant.</summary>
    /// <param name="player">Seller.</param>
    /// <param name="item">Sold item.</param>
    /// <param name="npcName">Merchant NPC name.</param>
    /// <param name="price">Zen received.</param>
    public void RecordNpcSell(Player player, Item item, string npcName, long price)
    {
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled)
        {
            return;
        }

        this.AppendEntry(new ActivityLogEntry
        {
            Kind = ActivityKind.NpcSell,
            Account = LoginOf(player),
            Character = player.Name,
            PartnerCharacter = npcName,
            ItemSummary = AccountSafetyPlugIn.Describe(item),
            ItemIds = ItemIdString(item),
            ZenDelta = price,
            Details = $"Npc={npcName}",
        });
    }

    private static string LoginOf(ITrader trader) =>
        trader is Player { IsTemplatePlayer: false, Account: { IsBot: false, LoginName: { Length: > 0 } login } }
            ? login
            : trader.Name;

    private static string ItemIdString(Item item) =>
        item is IIdentifiable identifiable && identifiable.Id != Guid.Empty
            ? identifiable.Id.ToString("N")
            : string.Empty;

    private static string JoinItemIds(IEnumerable<Item> items) =>
        string.Join(",", items.Select(ItemIdString).Where(id => !string.IsNullOrEmpty(id)));

    private static string SummarizeItems(IReadOnlyList<Item> items) =>
        string.Join(", ", items.Select(AccountSafetyPlugIn.Describe));

    private ActivityLogConfiguration EffectiveConfig() => this.Configuration ?? new ActivityLogConfiguration();

    private void AppendEntry(ActivityLogEntry entry)
    {
        var cfg = this.EffectiveConfig();
        ActivityLogStore.Open(cfg.FileName).Append(entry, cfg.RetentionDays);
    }
}
