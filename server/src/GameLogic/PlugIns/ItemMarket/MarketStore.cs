// <copyright file="MarketStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// Full serializable snapshot of an escrowed item.
/// </summary>
public sealed class MarketItemSnapshot
{
    /// <summary>Gets or sets the item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets upgrade level (+0..+15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability/stack count.</summary>
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has skill.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets the additional option level (0..7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets the excellent bitmask (bits 0..5).</summary>
    public byte ExcellentOptions { get; set; }

    /// <summary>Gets or sets the English item display name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese item display name.</summary>
    public string NameVi { get; set; } = string.Empty;
}

/// <summary>
/// An active escrow listing on the Item Market.
/// </summary>
public sealed class MarketListing
{
    /// <summary>Gets or sets the unique listing ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets seller account login name.</summary>
    public string SellerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets seller character name.</summary>
    public string SellerCharacter { get; set; } = string.Empty;

    /// <summary>Gets or sets the escrowed item snapshot.</summary>
    public MarketItemSnapshot Item { get; set; } = new();

    /// <summary>Gets or sets listing currency.</summary>
    public MarketCurrency Currency { get; set; }

    /// <summary>Gets or sets asking price.</summary>
    public int Price { get; set; }

    /// <summary>Gets or sets listing creation UTC timestamp.</summary>
    public DateTime ListedAtUtc { get; set; }

    /// <summary>Gets or sets listing expiry UTC timestamp.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}

/// <summary>
/// An item awaiting delivery to an account's in-game bag (bought item, canceled listing, or expired listing).
/// </summary>
public sealed class MarketPendingDelivery
{
    /// <summary>Gets or sets the delivery identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipient account login name.</summary>
    public string RecipientLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the item snapshot.</summary>
    public MarketItemSnapshot Item { get; set; } = new();

    /// <summary>Gets or sets origin reason (<c>Purchased</c>, <c>Canceled</c>, <c>Expired</c>).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Net Zen payout owed to an offline or bag-locked seller.
/// </summary>
public sealed class MarketPendingZen
{
    /// <summary>Gets or sets the payout identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the seller account login name.</summary>
    public string SellerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets net Zen amount after fee.</summary>
    public int NetZen { get; set; }

    /// <summary>Gets or sets associated listing ID.</summary>
    public string ListingId { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Audit log record for every Item Market action.
/// </summary>
public sealed class MarketAuditEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets action (<c>List</c>, <c>Buy</c>, <c>Cancel</c>, <c>Expire</c>, <c>Claim</c>).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets acting account login.</summary>
    public string ActorLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets counterparty login if any.</summary>
    public string CounterpartyLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets listing identifier.</summary>
    public string ListingId { get; set; } = string.Empty;

    /// <summary>Gets or sets item name.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets currency name.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets gross amount.</summary>
    public int GrossAmount { get; set; }

    /// <summary>Gets or sets deducted fee.</summary>
    public int FeeAmount { get; set; }

    /// <summary>Gets or sets net amount paid to seller.</summary>
    public int NetAmount { get; set; }

    /// <summary>Gets or sets origin (<c>game</c>, <c>website</c>, <c>admin</c>, <c>system</c>).</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Root store document saved under key <c>itemMarket</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class MarketStoreData
{
    /// <summary>Gets or sets active escrowed listings.</summary>
    public List<MarketListing> Listings { get; set; } = [];

    /// <summary>Gets or sets items awaiting in-game bag delivery.</summary>
    public List<MarketPendingDelivery> PendingDeliveries { get; set; } = [];

    /// <summary>Gets or sets net Zen payouts awaiting collection.</summary>
    public List<MarketPendingZen> PendingZen { get; set; } = [];

    /// <summary>Gets or sets newest-first audit log entries.</summary>
    public List<MarketAuditEntry> AuditLog { get; set; } = [];
}

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> providing atomic escrow transitions.
/// </summary>
public sealed class MarketStore
{
    private const string SectionKey = "itemMarket";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private MarketStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the market store for a file path.</summary>
    /// <param name="filePath">State file path, or null/blank for isolated temp storage.</param>
    /// <returns>The store.</returns>
    public static MarketStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-market-{Guid.NewGuid():N}.json"));
        return new MarketStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Projection function.</param>
    /// <returns>Projected result.</returns>
    public TResult Read<TResult>(Func<MarketStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MarketStoreData>(SectionKey, JsonOptions) ?? new MarketStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when modified.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Atomic mutator.</param>
    /// <returns>Outcome.</returns>
    public TResult Mutate<TResult>(Func<MarketStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MarketStoreData>(SectionKey, JsonOptions) ?? new MarketStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
