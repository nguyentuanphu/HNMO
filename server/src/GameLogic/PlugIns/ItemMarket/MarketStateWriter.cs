// <copyright file="MarketStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Item Market state into plain-ASCII JSON for packet <c>F5 39</c> and website APIs.
/// </summary>
public static class MarketStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Writes public market catalog and rules.</summary>
    /// <param name="engine">Market engine.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(MarketEngine engine, DateTime utcNow) =>
        WriteInternal(engine, loginName: null, bagItems: null, utcNow, null, null, null);

    /// <summary>Writes account/player-specific market state.</summary>
    /// <param name="engine">Market engine.</param>
    /// <param name="loginName">Account login name.</param>
    /// <param name="bagItems">Optional in-game listable bag items (slot + snapshot).</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WriteAccountState(
        MarketEngine engine,
        string loginName,
        IReadOnlyList<(byte Slot, MarketItemSnapshot Item)>? bagItems,
        DateTime utcNow,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null) =>
        WriteInternal(engine, loginName, bagItems, utcNow, actionResult, messageEn, messageVi);

    private static string WriteInternal(
        MarketEngine engine,
        string? loginName,
        IReadOnlyList<(byte Slot, MarketItemSnapshot Item)>? bagItems,
        DateTime utcNow,
        string? actionResult,
        string? messageEn,
        string? messageVi)
    {
        var cfg = engine.Configuration;
        var data = engine.Store.Read(d => new
        {
            listings = d.Listings
                .OrderByDescending(l => l.ListedAtUtc)
                .Take(120)
                .Select(l => new
                {
                    id = l.Id,
                    sellerName = l.SellerCharacter,
                    isMine = !string.IsNullOrWhiteSpace(loginName) && string.Equals(l.SellerLogin, loginName, StringComparison.OrdinalIgnoreCase),
                    currency = l.Currency.ToString(),
                    currencyCode = (int)l.Currency,
                    price = l.Price,
                    listedAt = ToMs(l.ListedAtUtc),
                    expiresAt = ToMs(l.ExpiresAtUtc),
                    item = new
                    {
                        l.Item.Group,
                        l.Item.Number,
                        l.Item.Level,
                        l.Item.Durability,
                        l.Item.Skill,
                        l.Item.Luck,
                        l.Item.OptionLevel,
                        l.Item.ExcellentOptions,
                        l.Item.NameEn,
                        l.Item.NameVi,
                    },
                }).ToList(),
            deliveries = string.IsNullOrWhiteSpace(loginName)
                ? []
                : d.PendingDeliveries
                    .Where(x => string.Equals(x.RecipientLogin, loginName, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new { x.Id, x.Reason, x.Item.NameEn, x.Item.NameVi, x.Item.Level })
                    .ToList(),
            pendingZen = string.IsNullOrWhiteSpace(loginName)
                ? 0L
                : d.PendingZen
                    .Where(x => string.Equals(x.SellerLogin, loginName, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => (long)x.NetZen),
        });

        var bagList = (bagItems ?? [])
            .Select(b => new
            {
                slot = b.Slot,
                group = b.Item.Group,
                number = b.Item.Number,
                level = b.Item.Level,
                nameEn = b.Item.NameEn,
                nameVi = b.Item.NameVi,
                banned = engine.IsItemBanned(b.Item.Group, b.Item.Number),
            }).ToList();

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            accessPaid = string.IsNullOrWhiteSpace(loginName) ? (bool?)null : engine.HasAccess(loginName),
            allowWebsite = cfg.AllowWebsite,
            now = ToMs(utcNow),
            rules = new
            {
                allowZen = cfg.AllowZen,
                allowWCoinC = cfg.AllowWCoinC,
                allowWCoinP = cfg.AllowWCoinP,
                allowGoblinPoints = cfg.AllowGoblinPoints,
                feePercent = cfg.FeePercent,
                accessFee = cfg.AccessFee,
                accessCoin = cfg.AccessFeeCoinType.ToString(),
                listingZenFee = cfg.ListingZenFee,
                maxListings = cfg.MaxListingsPerAccount,
            },
            data.listings,
            data.deliveries,
            data.pendingZen,
            bagItems = bagList,
            @event = actionResult is null ? null : new { result = actionResult, textEn = messageEn, textVi = messageVi },
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static long ToMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u");
                sb.Append(((int)c).ToString("x4"));
            }
        }

        return sb.ToString();
    }
}
