// <copyright file="CombiningLeftoversStateWriter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HNMO.DataModel;

/// <summary>
/// Serializes Combining Leftovers recipes, Surplus Exchange offers, and bag readiness into plain-ASCII JSON
/// for <c>F5 41</c> and <c>/api/website/leftovers</c>.
/// </summary>
public static class CombiningLeftoversStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Reads non-equipped main-bag items belonging to <paramref name="player"/>.</summary>
    /// <param name="player">Player.</param>
    /// <param name="isLocked">Optional item-lock predicate (e.g. AccountSafety).</param>
    /// <returns>Bag descriptors.</returns>
    public static List<BagItemDescriptor> ReadBag(Player player, Func<Item, bool>? isLocked = null)
    {
        if (player.Inventory is not { } inventory)
        {
            return [];
        }

        return inventory.Items
            .Where(item => item.Definition is not null
                && item.ItemSlot >= InventoryConstants.EquippableSlotsCount
                && item.ItemSlot < InventoryConstants.FirstStoreItemSlotIndex)
            .Select(item => new BagItemDescriptor(
                item.ItemSlot,
                item.Definition!.Group,
                item.Definition.Number,
                item.Level,
                isLocked is not null && isLocked(item),
                PieceCount(item)))
            .ToList();
    }

    /// <summary>
    /// The pieces an item counts as: the durability of an item which is no equipment (a stack), one for equipment
    /// (weapons, armor, wings), whose durability is its wear and not a count.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The pieces.</returns>
    internal static int PieceCount(Item item) => item.IsWearable() ? 1 : Math.Max(1, (int)item.Durability);

    /// <summary>Serializes the public endpoint payload (recipes, exchange offers, effective box cap).</summary>
    /// <param name="engine">Leftovers engine.</param>
    /// <param name="seasonCapOverride">Optional live season box cap.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(CombiningLeftoversEngine engine, int? seasonCapOverride = null) =>
        WritePayload(engine, bag: null, playerZen: null, seasonCapOverride, notice: null);

    /// <summary>Serializes a player-specific payload with bag ingredient counts and action feedback.</summary>
    /// <param name="engine">Leftovers engine.</param>
    /// <param name="player">Player.</param>
    /// <param name="bag">Pre-read bag items.</param>
    /// <param name="seasonCapOverride">Optional live season box cap.</param>
    /// <param name="notice">Optional action result notice.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WriteForPlayer(
        CombiningLeftoversEngine engine,
        Player player,
        IReadOnlyList<BagItemDescriptor> bag,
        int? seasonCapOverride = null,
        (string Status, string MessageEn, string MessageVi)? notice = null) =>
        WritePayload(engine, bag, player.Money, seasonCapOverride, notice);

    private static string WritePayload(
        CombiningLeftoversEngine engine,
        IReadOnlyList<BagItemDescriptor>? bag,
        long? playerZen,
        int? seasonCapOverride,
        (string Status, string MessageEn, string MessageVi)? notice)
    {
        var effectiveMaxBox = engine.EffectiveMaxBoxLevel(seasonCapOverride);
        var zen = playerZen ?? 0L;

        var recipes = engine.Configuration.Recipes.Select(r =>
        {
            var blockedBySeason = r.RequiredStageBoxLevel > 0 && r.RequiredStageBoxLevel > effectiveMaxBox;
            var check = bag is not null
                ? engine.PlanCombine(r.Id, bag, zen, seasonCapOverride, out _)
                : LeftoversCheckResult.Disabled;

            var inputs = r.Inputs.Select(ing => new
            {
                group = ing.Group,
                number = ing.Number,
                exactLevel = ing.ExactLevel,
                count = ing.Count,
                nameEn = ing.NameEn,
                nameVi = ing.NameVi,
                have = bag is null
                    ? 0
                    : bag.Where(b => !b.Locked && b.Group == ing.Group && b.Number == ing.Number && (ing.ExactLevel < 0 || b.Level == ing.ExactLevel)).Sum(b => b.Quantity),
            }).ToList();

            return new
            {
                id = r.Id,
                nameEn = r.NameEn,
                nameVi = r.NameVi,
                enabled = r.Enabled,
                successRatePercent = r.SuccessRatePercent,
                zenCost = r.ZenCost,
                outputGroup = r.OutputGroup,
                outputNumber = r.OutputNumber,
                outputLevel = r.OutputLevel,
                requiredStageBoxLevel = r.RequiredStageBoxLevel,
                blockedBySeasonStage = blockedBySeason,
                canCombine = bag is not null && check == LeftoversCheckResult.Ok,
                inputs,
            };
        }).ToList();

        var offers = engine.Configuration.ExchangeOffers.Select(o =>
        {
            var have = bag is null
                ? 0
                : bag.Where(b => !b.Locked && b.Group == o.InputGroup && b.Number == o.InputNumber && (o.InputLevel < 0 || b.Level == o.InputLevel)).Sum(b => b.Quantity);
            var canExchange = bag is not null && engine.PlanExchange(o.Id, bag, out _) == LeftoversCheckResult.Ok;

            return new
            {
                id = o.Id,
                nameEn = o.NameEn,
                nameVi = o.NameVi,
                enabled = o.Enabled,
                inputGroup = o.InputGroup,
                inputNumber = o.InputNumber,
                inputLevel = o.InputLevel,
                inputCount = o.InputCount,
                have,
                rewardCoins = o.RewardCoins,
                rewardCoinType = o.RewardCoinType.ToString(),
                rewardZen = o.RewardZen,
                rewardItemCount = o.RewardItemCount,
                rewardGroup = o.RewardGroup,
                rewardNumber = o.RewardNumber,
                rewardLevel = o.RewardLevel,
                canExchange,
            };
        }).ToList();

        var recentLogs = engine.Store.RecentLogs(12).Select(l => new
        {
            at = new DateTimeOffset(DateTime.SpecifyKind(l.AtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            action = l.Action,
            targetId = l.TargetId,
            detail = l.Detail,
        }).ToList();

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            exchangeNpcNumber = engine.Configuration.ExchangeNpcNumber,
            effectiveMaxBoxLevel = effectiveMaxBox,
            zen = playerZen,
            notice = notice is { } n ? new { status = n.Status, messageEn = n.MessageEn, messageVi = n.MessageVi } : null,
            recipes,
            offers,
            recentLogs,
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

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
