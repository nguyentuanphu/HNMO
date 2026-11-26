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

        var usedCounts = new Dictionary<byte, int>();
        foreach (var ing in recipe.Inputs)
        {
            var remainingNeeded = ing.Count;
            foreach (var b in bag)
            {
                if (remainingNeeded <= 0)
                {
                    break;
                }

                if (b.Locked || b.Group != ing.Group || b.Number != ing.Number
                    || (ing.ExactLevel >= 0 && b.Level != ing.ExactLevel))
                {
                    continue;
                }

                usedCounts.TryGetValue(b.Slot, out var alreadyUsed);
                var available = Math.Max(0, b.Quantity) - alreadyUsed;
                if (available <= 0)
                {
                    continue;
                }

                var take = Math.Min(remainingNeeded, available);
                usedCounts[b.Slot] = alreadyUsed + take;
                for (var i = 0; i < take; i++)
                {
                    pickedSlots.Add(b.Slot);
                }

                remainingNeeded -= take;
            }

            if (remainingNeeded > 0)
            {
                pickedSlots.Clear();
                return LeftoversCheckResult.MissingIngredients;
            }
        }

        return LeftoversCheckResult.Ok;
    }

    /// <summary>Validates and selects bag slots for a surplus material exchange offer.</summary>
    /// <param name="offerId">Offer ID.</param>
    /// <param name="bag">Bag items.</param>
    /// <param name="pickedSlots">Selected slots (repeated per stack piece consumed).</param>
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

        var remainingNeeded = offer.InputCount;
        foreach (var b in bag)
        {
            if (remainingNeeded <= 0)
            {
                break;
            }

            if (b.Locked || b.Group != offer.InputGroup || b.Number != offer.InputNumber
                || (offer.InputLevel >= 0 && b.Level != offer.InputLevel))
            {
                continue;
            }

            var take = Math.Min(remainingNeeded, Math.Max(0, b.Quantity));
            for (var i = 0; i < take; i++)
            {
                pickedSlots.Add(b.Slot);
            }

            remainingNeeded -= take;
        }

        if (remainingNeeded > 0)
        {
            pickedSlots.Clear();
            return LeftoversCheckResult.MissingIngredients;
        }

        return LeftoversCheckResult.Ok;
    }
}
