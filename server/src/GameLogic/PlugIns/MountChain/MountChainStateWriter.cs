// <copyright file="MountChainStateWriter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HNMO.DataModel;
using HNMO.DataModel.Entities;

/// <summary>
/// Serializes Mount Chain tiers, Golden Boss drop config, and player/public collection state into plain-ASCII JSON.
/// </summary>
public static class MountChainStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Reads non-equipped main bag items.</summary>
    /// <param name="player">Player.</param>
    /// <param name="isLocked">Optional Account Safety lock check.</param>
    /// <returns>Mount bag descriptors.</returns>
    public static List<MountBagItem> ReadBag(Player player, Func<Item, bool>? isLocked = null)
    {
        if (player.Inventory is not { } inv)
        {
            return [];
        }

        return inv.Items
            .Where(i => i.Definition is not null
                && i.ItemSlot >= InventoryConstants.EquippableSlotsCount
                && i.ItemSlot < InventoryConstants.FirstStoreItemSlotIndex)
            .Select(i => new MountBagItem(i.ItemSlot, i.Definition!.Group, i.Definition.Number, i.Level, isLocked is not null && isLocked(i)))
            .ToList();
    }

    /// <summary>Serializes public mount chain catalog and collector counts (<c>/api/website/mounts</c>).</summary>
    /// <param name="engine">Engine.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(MountChainEngine engine) =>
        WritePayload(engine, login: null, bag: null, playerZen: null, notice: null);

    /// <summary>Serializes per-player mount chain progress, bag duplicate counts, and optional action notice.</summary>
    /// <param name="engine">Engine.</param>
    /// <param name="player">Player.</param>
    /// <param name="bag">Bag items.</param>
    /// <param name="notice">Optional action notice.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WriteForPlayer(
        MountChainEngine engine,
        Player player,
        IReadOnlyList<MountBagItem> bag,
        (string Status, string MessageEn, string MessageVi)? notice = null) =>
        WritePayload(engine, player.Account?.LoginName, bag, player.Money, notice);

    private static string WritePayload(
        MountChainEngine engine,
        string? login,
        IReadOnlyList<MountBagItem>? bag,
        long? playerZen,
        (string Status, string MessageEn, string MessageVi)? notice)
    {
        var snap = engine.Store.Read(d =>
        {
            var collectorCounts = engine.Configuration.Tiers.ToDictionary(
                t => t.Id,
                t => d.Accounts.Values.Count(a => a.Unlocked.ContainsKey(t.Id)),
                StringComparer.OrdinalIgnoreCase);

            MountAccountState? acc = null;
            if (!string.IsNullOrWhiteSpace(login) && d.Accounts.TryGetValue(login, out var found))
            {
                acc = found;
            }

            return (
                CollectorCounts: collectorCounts,
                TotalCollectors: d.Accounts.Count(kv => kv.Value.Unlocked.Count > 0),
                Shards: acc?.Shards ?? 0,
                ActiveTierId: acc?.ActiveTierId ?? string.Empty,
                Unlocked: acc is null
                    ? new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, DateTime>(acc.Unlocked, StringComparer.OrdinalIgnoreCase));
        });

        var tiers = engine.Configuration.Tiers.Select(t =>
        {
            var bagCount = bag is null
                ? 0
                : bag.Count(b => !b.Locked && b.Group == t.ItemGroup && b.Number == t.ItemNumber && b.Level == t.ItemLevel);
            var isUnlocked = snap.Unlocked.TryGetValue(t.Id, out var unlockedAt);
            var canCraft = bag is not null && !string.IsNullOrWhiteSpace(login)
                && engine.PlanCraft(login, t.Id, bag, playerZen ?? 0, out _) == MountActionResult.Ok;

            return new
            {
                id = t.Id,
                nameEn = t.NameEn,
                nameVi = t.NameVi,
                itemGroup = t.ItemGroup,
                itemNumber = t.ItemNumber,
                itemLevel = t.ItemLevel,
                shardsRequired = t.ShardsRequired,
                previousTierId = t.PreviousTierId,
                previousMountsRequired = t.PreviousMountsRequired,
                zenCost = t.ZenCost,
                shardsOnSalvage = t.ShardsOnSalvage,
                pveDefenseBonus = t.PveDefenseBonus,
                bonusExpPercent = t.BonusExpPercent,
                bonusZenPercent = t.BonusZenPercent,
                collectorsCount = snap.CollectorCounts.TryGetValue(t.Id, out var cnt) ? cnt : 0,
                unlocked = isUnlocked,
                unlockedAt = isUnlocked ? ToMs(unlockedAt) : (long?)null,
                bagDuplicates = bagCount,
                canCraft,
            };
        }).ToList();

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            goldenDropChancePercent = engine.Configuration.GoldenDropChancePercent,
            shardsPerGoldenBoss = engine.Configuration.ShardsPerGoldenBoss,
            shards = snap.Shards,
            activeTierId = snap.ActiveTierId,
            totalCollectors = snap.TotalCollectors,
            notice = notice is { } n ? new { status = n.Status, messageEn = n.MessageEn, messageVi = n.MessageVi } : null,
            tiers,
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
