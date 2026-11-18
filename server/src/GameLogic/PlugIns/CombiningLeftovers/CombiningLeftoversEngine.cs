// <copyright file="CombiningLeftoversEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

/// <summary>Result of validating a leftover combine or exchange.</summary>
public enum LeftoversCheckResult
{
    /// <summary>Allowed and ready to execute.</summary>
    Ok,

    /// <summary>Feature disabled.</summary>
    Disabled,

    /// <summary>Recipe/offer not found or disabled.</summary>
    NotFound,

    /// <summary>Blocked by current season stage box level limit.</summary>
    BlockedBySeasonStage,

    /// <summary>Insufficient Zen.</summary>
    NotEnoughZen,

    /// <summary>Missing required bag items.</summary>
    MissingIngredients,
}

/// <summary>Bag item slot descriptor used by <see cref="CombiningLeftoversEngine"/>.</summary>
/// <param name="Slot">Inventory slot index.</param>
/// <param name="Group">Item group.</param>
/// <param name="Number">Item number.</param>
/// <param name="Level">Item level.</param>
/// <param name="Locked">Whether account-safety locked.</param>
public sealed record BagItemDescriptor(byte Slot, byte Group, short Number, byte Level, bool Locked = false);

/// <summary>
/// Pure rule validator &amp; ingredient picker for Combining Leftovers &amp; Surplus Material Exchange.
/// </summary>
public sealed class CombiningLeftoversEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CombiningLeftoversEngine"/> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="store">Persistent store.</param>
    /// <param name="enabled">Whether active.</param>
    public CombiningLeftoversEngine(CombiningLeftoversConfiguration configuration, CombiningLeftoversStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets configuration.</summary>
    public CombiningLeftoversConfiguration Configuration { get; }

    /// <summary>Gets store.</summary>
    public CombiningLeftoversStore Store { get; }

    /// <summary>Gets a value indicating whether feature is active.</summary>
    public bool Enabled { get; }

    /// <summary>Effective max box level taking configured <see cref="CombiningLeftoversConfiguration.MaxBoxLevel"/> and Season Plan into account.</summary>
    /// <param name="seasonCapOverride">Optional live cap from SeasonPlanPlugIn.</param>
    /// <returns>Max allowed box stage level.</returns>
    public int EffectiveMaxBoxLevel(int? seasonCapOverride = null)
    {
        var cap = Math.Max(1, this.Configuration.MaxBoxLevel);
        var fromStore = this.Store.TryReadSeasonStageMaxBoxLevel();
        var activeCap = seasonCapOverride is > 0 ? seasonCapOverride : fromStore;
        return activeCap is > 0 ? Math.Min(cap, activeCap.Value) : cap;
    }

    /// <summary>Validates and selects exact bag slots to consume for a combine recipe.</summary>
    /// <param name="recipeId">Recipe ID.</param>
    /// <param name="bag">Bag item descriptors.</param>
    /// <param name="playerZen">Player Zen.</param>
    /// <param name="seasonCapOverride">Optional live season stage box cap.</param>
    /// <param name="pickedSlots">Selected inventory slots to consume if <c>Ok</c>.</param>
    /// <returns>Validation result.</returns>
    public LeftoversCheckResult PlanCombine(
        string recipeId,
        IReadOnlyList<BagItemDescriptor> bag,
        long playerZen,
        int? seasonCapOverride,
        out List<byte> pickedSlots)
    {
        pickedSlots = [];
        if (!this.Enabled)
        {
            return LeftoversCheckResult.Disabled;
        }

        var recipe = this.Configuration.Recipes.FirstOrDefault(r => r.Enabled && string.Equals(r.Id, recipeId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (recipe is null)
        {
            return LeftoversCheckResult.NotFound;
        }

        var effectiveCap = this.EffectiveMaxBoxLevel(seasonCapOverride);
        if (recipe.RequiredStageBoxLevel > 0 && recipe.RequiredStageBoxLevel > effectiveCap)
        {
            return LeftoversCheckResult.BlockedBySeasonStage;
        }

        if (playerZen < recipe.ZenCost)
        {
            return LeftoversCheckResult.NotEnoughZen;
        }

        var used = new HashSet<byte>();
        foreach (var ing in recipe.Inputs)
        {
            var matched = bag
                .Where(b => !b.Locked && !used.Contains(b.Slot)
                    && b.Group == ing.Group && b.Number == ing.Number
                    && (ing.ExactLevel < 0 || b.Level == ing.ExactLevel))
                .Take(ing.Count)
                .ToList();
            if (matched.Count < ing.Count)
            {
                pickedSlots.Clear();
                return LeftoversCheckResult.MissingIngredients;
            }

            foreach (var m in matched)
            {
                used.Add(m.Slot);
                pickedSlots.Add(m.Slot);
            }
        }

        return LeftoversCheckResult.Ok;
    }

    /// <summary>Validates and selects bag slots for a surplus material exchange offer.</summary>
    /// <param name="offerId">Offer ID.</param>
    /// <param name="bag">Bag items.</param>
    /// <param name="pickedSlots">Selected slots.</param>
    /// <returns>Validation result.</returns>
    public LeftoversCheckResult PlanExchange(
        string offerId,
        IReadOnlyList<BagItemDescriptor> bag,
        out List<byte> pickedSlots)
    {
        pickedSlots = [];
        if (!this.Enabled)
        {
            return LeftoversCheckResult.Disabled;
        }

        var offer = this.Configuration.ExchangeOffers.FirstOrDefault(o => o.Enabled && string.Equals(o.Id, offerId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (offer is null)
        {
            return LeftoversCheckResult.NotFound;
        }

        var matched = bag
            .Where(b => !b.Locked && b.Group == offer.InputGroup && b.Number == offer.InputNumber
                && (offer.InputLevel < 0 || b.Level == offer.InputLevel))
            .Take(offer.InputCount)
            .ToList();
        if (matched.Count < offer.InputCount)
        {
            return LeftoversCheckResult.MissingIngredients;
        }

        pickedSlots.AddRange(matched.Select(m => m.Slot));
        return LeftoversCheckResult.Ok;
    }
}
