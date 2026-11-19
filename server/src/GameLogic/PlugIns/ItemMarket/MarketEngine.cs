// <copyright file="MarketEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

/// <summary>Outcome of creating a market listing.</summary>
public enum MarketListResult
{
    /// <summary>Listing created and item escrowed.</summary>
    Success,

    /// <summary>Market is disabled.</summary>
    Disabled,

    /// <summary>Selected currency is disabled by admin.</summary>
    CurrencyDisabled,

    /// <summary>Item is on the banned items list.</summary>
    ItemBanned,

    /// <summary>Asking price must be strictly positive.</summary>
    InvalidPrice,

    /// <summary>Seller reached active listing limit.</summary>
    ListingLimitReached,
}

/// <summary>Outcome of claiming a listing for atomic purchase.</summary>
public enum MarketClaimResult
{
    /// <summary>Listing claimed atomically by this buyer.</summary>
    Claimed,

    /// <summary>Market is disabled.</summary>
    Disabled,

    /// <summary>Listing no longer exists (already bought, canceled, or expired).</summary>
    AlreadySoldOrMissing,

    /// <summary>Buyer cannot buy their own listing.</summary>
    CannotBuyOwnListing,
}

/// <summary>
/// Pure, concurrency-safe escrow engine for the Item Market.
/// All transitions acquire <see cref="MarketStore"/>'s lock so concurrent buys never duplicate items or payouts.
/// </summary>
public sealed class MarketEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarketEngine"/> class.
    /// </summary>
    /// <param name="configuration">Configuration rules.</param>
    /// <param name="store">Atomic state store.</param>
    /// <param name="enabled">Whether the market is active.</param>
    public MarketEngine(MarketConfiguration configuration, MarketStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets the market configuration.</summary>
    public MarketConfiguration Configuration { get; }

    /// <summary>Gets the escrow store.</summary>
    public MarketStore Store { get; }

    /// <summary>Gets a value indicating whether the market is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Returns whether a currency is currently enabled by admin settings.</summary>
    /// <param name="currency">The currency.</param>
    /// <returns><c>true</c> if enabled.</returns>
    public bool IsCurrencyAllowed(MarketCurrency currency) => currency switch
    {
        MarketCurrency.Zen => this.Configuration.AllowZen,
        MarketCurrency.WCoinC => this.Configuration.AllowWCoinC,
        MarketCurrency.WCoinP => this.Configuration.AllowWCoinP,
        MarketCurrency.GoblinPoints => this.Configuration.AllowGoblinPoints,
        _ => false,
    };

    /// <summary>Returns whether an item (<c>group:number</c>) is banned from the market.</summary>
    /// <param name="group">Item group.</param>
    /// <param name="number">Item number.</param>
    /// <returns><c>true</c> if banned.</returns>
    public bool IsItemBanned(byte group, short number)
    {
        var key = $"{group}:{number}";
        return this.Configuration.BannedItems.Any(b => string.Equals(b?.Trim(), key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Computes the market fee and net seller payout for a gross sale price.</summary>
    /// <param name="price">Gross price.</param>
    /// <returns>Fee deducted and net payout.</returns>
    public (int FeeAmount, int NetAmount) CalculateFee(int price)
    {
        if (price <= 0)
        {
            return (0, 0);
        }

        var pct = Math.Clamp(this.Configuration.FeePercent, 0.0, 50.0);
        var fee = (int)Math.Floor(price * (pct / 100.0));
        var net = Math.Max(0, price - fee);
        return (fee, net);
    }

    /// <summary>Validates and creates an escrowed listing atomically.</summary>
    /// <param name="sellerLogin">Seller account login.</param>
    /// <param name="sellerCharacter">Seller character name.</param>
    /// <param name="item">Item snapshot.</param>
    /// <param name="currency">Price currency.</param>
    /// <param name="price">Asking price.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <param name="listing">Created listing when successful.</param>
    /// <returns>Result code.</returns>
    public MarketListResult CreateListing(
        string sellerLogin,
        string sellerCharacter,
        MarketItemSnapshot item,
        MarketCurrency currency,
        int price,
        DateTime utcNow,
        out MarketListing? listing)
    {
        listing = null;
        if (!this.Enabled)
        {
            return MarketListResult.Disabled;
        }

        if (!this.IsCurrencyAllowed(currency))
        {
            return MarketListResult.CurrencyDisabled;
        }

        if (price <= 0)
        {
            return MarketListResult.InvalidPrice;
        }

        if (this.IsItemBanned(item.Group, item.Number))
        {
            return MarketListResult.ItemBanned;
        }

        var maxListings = Math.Max(1, this.Configuration.MaxListingsPerAccount);
        var expiryHours = Math.Max(1, this.Configuration.ExpiryHours);
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        var (outcome, created) = this.Store.Mutate(data =>
        {
            var activeCount = data.Listings.Count(l => string.Equals(l.SellerLogin, sellerLogin, StringComparison.OrdinalIgnoreCase));
            if (activeCount >= maxListings)
            {
                return (false, (MarketListResult.ListingLimitReached, (MarketListing?)null));
            }

            var entry = new MarketListing
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                SellerLogin = sellerLogin,
                SellerCharacter = sellerCharacter,
                Item = item,
                Currency = currency,
                Price = price,
                ListedAtUtc = now,
                ExpiresAtUtc = now.AddHours(expiryHours),
            };
            data.Listings.Add(entry);
            AddAudit(data, this.Configuration.MaxAuditEntries, new MarketAuditEntry
            {
                AtUtc = now,
                Action = "List",
                ActorLogin = sellerLogin,
                ListingId = entry.Id,
                ItemName = item.NameEn,
                Currency = currency.ToString(),
                GrossAmount = price,
                Source = "game",
            });
            return (true, (MarketListResult.Success, entry));
        });

        listing = created;
        return outcome;
    }

    /// <summary>
    /// Atomically removes a listing from the active market for a buyer so concurrent threads/requests cannot double-buy.
    /// Call <see cref="RestoreClaimedListing"/> if buyer payment fails afterwards, or <see cref="FinalizePurchase"/> on payment success.
    /// </summary>
    /// <param name="buyerLogin">Buyer account login.</param>
    /// <param name="listingId">Target listing ID.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Claim result with listing, fee, and net amount.</returns>
    public MarketBuyClaim TryClaimForBuy(string buyerLogin, string listingId, DateTime utcNow)
    {
        if (!this.Enabled)
        {
            return new MarketBuyClaim(MarketClaimResult.Disabled, null, 0, 0);
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return this.Store.Mutate(data =>
        {
            var listing = data.Listings.FirstOrDefault(l => string.Equals(l.Id, listingId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (listing is null || now >= listing.ExpiresAtUtc)
            {
                return (false, new MarketBuyClaim(MarketClaimResult.AlreadySoldOrMissing, null, 0, 0));
            }

            if (string.Equals(listing.SellerLogin, buyerLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, new MarketBuyClaim(MarketClaimResult.CannotBuyOwnListing, null, 0, 0));
            }

            data.Listings.Remove(listing);
            var (fee, net) = this.CalculateFee(listing.Price);
            return (true, new MarketBuyClaim(MarketClaimResult.Claimed, listing, fee, net));
        });
    }

    /// <summary>Restores a previously claimed listing when buyer payment validation fails (rollback).</summary>
    /// <param name="listing">Claimed listing to restore.</param>
    public void RestoreClaimedListing(MarketListing listing)
    {
        this.Store.Mutate(data =>
        {
            if (data.Listings.Any(l => l.Id == listing.Id))
            {
                return (false, false);
            }

            data.Listings.Add(listing);
            return (true, true);
        });
    }

    /// <summary>
    /// Completes a claimed purchase: queues purchased item for the buyer (if <paramref name="enqueueItemDelivery"/>),
    /// queues net Zen payout (when currency is Zen), and writes the audit log.
    /// </summary>
    /// <param name="buyerLogin">Buyer login.</param>
    /// <param name="claim">Claim result.</param>
    /// <param name="source">Origin (<c>game</c> or <c>website</c>).</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="enqueueItemDelivery">Whether to queue the item into <see cref="MarketStoreData.PendingDeliveries"/>.</param>
    /// <param name="enqueueZenPayout">Whether to queue Zen payout for the seller.</param>
    public void FinalizePurchase(
        string buyerLogin,
        MarketBuyClaim claim,
        string source,
        DateTime utcNow,
        bool enqueueItemDelivery,
        bool enqueueZenPayout)
    {
        var listing = claim.Listing!;
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        this.Store.Mutate(data =>
        {
            if (enqueueItemDelivery)
            {
                data.PendingDeliveries.Add(new MarketPendingDelivery
                {
                    Id = $"buy:{listing.Id}",
                    RecipientLogin = buyerLogin,
                    Item = listing.Item,
                    Reason = "Purchased",
                    CreatedAtUtc = now,
                });
            }

            if (enqueueZenPayout && listing.Currency == MarketCurrency.Zen && claim.NetAmount > 0)
            {
                data.PendingZen.Add(new MarketPendingZen
                {
                    Id = $"zen:{listing.Id}",
                    SellerLogin = listing.SellerLogin,
                    NetZen = claim.NetAmount,
                    ListingId = listing.Id,
                    CreatedAtUtc = now,
                });
            }

            AddAudit(data, this.Configuration.MaxAuditEntries, new MarketAuditEntry
            {
                AtUtc = now,
                Action = "Buy",
                ActorLogin = buyerLogin,
                CounterpartyLogin = listing.SellerLogin,
                ListingId = listing.Id,
                ItemName = listing.Item.NameEn,
                Currency = listing.Currency.ToString(),
                GrossAmount = listing.Price,
                FeeAmount = claim.FeeAmount,
                NetAmount = claim.NetAmount,
                Source = source,
            });
            return (true, true);
        });
    }

    /// <summary>Cancels a listing and returns the item to escrow delivery for the seller.</summary>
    /// <param name="actorLogin">Caller login (or admin).</param>
    /// <param name="listingId">Listing ID.</param>
    /// <param name="isAdmin">Bypass seller ownership check when triggered by an admin.</param>
    /// <param name="source">Origin (<c>game</c>, <c>website</c>, <c>admin</c>).</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns><c>true</c> if canceled and moved to escrow delivery.</returns>
    public bool CancelListing(string actorLogin, string listingId, bool isAdmin, string source, DateTime utcNow)
    {
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return this.Store.Mutate(data =>
        {
            var listing = data.Listings.FirstOrDefault(l => string.Equals(l.Id, listingId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (listing is null)
            {
                return (false, false);
            }

            if (!isAdmin && !string.Equals(listing.SellerLogin, actorLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, false);
            }

            data.Listings.Remove(listing);
            data.PendingDeliveries.Add(new MarketPendingDelivery
            {
                Id = $"cancel:{listing.Id}",
                RecipientLogin = listing.SellerLogin,
                Item = listing.Item,
                Reason = isAdmin ? "ReturnedByAdmin" : "Canceled",
                CreatedAtUtc = now,
            });
            AddAudit(data, this.Configuration.MaxAuditEntries, new MarketAuditEntry
            {
                AtUtc = now,
                Action = "Cancel",
                ActorLogin = actorLogin,
                CounterpartyLogin = listing.SellerLogin,
                ListingId = listing.Id,
                ItemName = listing.Item.NameEn,
                Currency = listing.Currency.ToString(),
                GrossAmount = listing.Price,
                Source = source,
            });
            return (true, true);
        });
    }

    /// <summary>Moves expired listings back into seller escrow delivery.</summary>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Number of expired listings.</returns>
    public int ExpireDueListings(DateTime utcNow)
    {
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return this.Store.Mutate(data =>
        {
            var expired = data.Listings.Where(l => now >= l.ExpiresAtUtc).ToList();
            if (expired.Count == 0)
            {
                return (false, 0);
            }

            foreach (var listing in expired)
            {
                data.Listings.Remove(listing);
                data.PendingDeliveries.Add(new MarketPendingDelivery
                {
                    Id = $"exp:{listing.Id}",
                    RecipientLogin = listing.SellerLogin,
                    Item = listing.Item,
                    Reason = "Expired",
                    CreatedAtUtc = now,
                });
                AddAudit(data, this.Configuration.MaxAuditEntries, new MarketAuditEntry
                {
                    AtUtc = now,
                    Action = "Expire",
                    ActorLogin = "system",
                    CounterpartyLogin = listing.SellerLogin,
                    ListingId = listing.Id,
                    ItemName = listing.Item.NameEn,
                    Currency = listing.Currency.ToString(),
                    GrossAmount = listing.Price,
                    Source = "system",
                });
            }

            return (true, expired.Count);
        });
    }

    /// <summary>Peeks all pending item deliveries for an account.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <returns>Pending deliveries.</returns>
    public IReadOnlyList<MarketPendingDelivery> PeekDeliveries(string loginName) =>
        this.Store.Read(data => data.PendingDeliveries
            .Where(d => string.Equals(d.RecipientLogin, loginName, StringComparison.OrdinalIgnoreCase))
            .ToList());

    /// <summary>Removes a delivered escrow item after placing it in the bag.</summary>
    /// <param name="deliveryId">Delivery ID.</param>
    public void CompleteDelivery(string deliveryId)
    {
        this.Store.Mutate(data =>
        {
            var removed = data.PendingDeliveries.RemoveAll(d => d.Id == deliveryId) > 0;
            return (removed, removed);
        });
    }

    /// <summary>Peeks pending Zen payouts for a seller.</summary>
    /// <param name="loginName">Seller login.</param>
    /// <returns>Pending Zen payouts.</returns>
    public IReadOnlyList<MarketPendingZen> PeekZenPayouts(string loginName) =>
        this.Store.Read(data => data.PendingZen
            .Where(z => string.Equals(z.SellerLogin, loginName, StringComparison.OrdinalIgnoreCase))
            .ToList());

    /// <summary>Marks a pending Zen payout as paid.</summary>
    /// <param name="payoutId">Payout ID.</param>
    public void CompleteZenPayout(string payoutId)
    {
        this.Store.Mutate(data =>
        {
            var removed = data.PendingZen.RemoveAll(z => z.Id == payoutId) > 0;
            return (removed, removed);
        });
    }

    private static void AddAudit(MarketStoreData data, int cap, MarketAuditEntry entry)
    {
        data.AuditLog.Insert(0, entry);
        var max = Math.Max(10, cap);
        if (data.AuditLog.Count > max)
        {
            data.AuditLog.RemoveRange(max, data.AuditLog.Count - max);
        }
    }
}
