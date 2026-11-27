// <copyright file="MountChainEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

/// <summary>Result of validating a mount chain craft, salvage, or summon action.</summary>
public enum MountActionResult
{
    /// <summary>Permitted.</summary>
    Ok,

    /// <summary>Plug-in disabled.</summary>
    Disabled,

    /// <summary>Tier not found.</summary>
    NotFound,

    /// <summary>Insufficient Mount Shards.</summary>
    NotEnoughShards,

    /// <summary>Insufficient Zen.</summary>
    NotEnoughZen,

    /// <summary>Missing required unlocked/bag duplicate mounts of the previous tier.</summary>
    MissingPreviousMount,

    /// <summary>No unlocked duplicate mount of this tier found to salvage.</summary>
    MissingDuplicateToSalvage,

    /// <summary>Tier is not unlocked yet on this account.</summary>
    NotUnlocked,
}

/// <summary>
/// Pure rule engine for Golden Boss shard awards, mount upgrades using duplicate bag mounts, salvaging duplicates, and active PvE tier lookup.
/// </summary>
public sealed class MountChainEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MountChainEngine"/> class.
    /// </summary>
    /// <param name="configuration">Mount Chain configuration.</param>
    /// <param name="store">Persistent store.</param>
    /// <param name="enabled">Whether plug-in is active.</param>
    public MountChainEngine(MountChainConfiguration configuration, MountChainStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets configuration.</summary>
    public MountChainConfiguration Configuration { get; }

    /// <summary>Gets store.</summary>
    public MountChainStore Store { get; }

    /// <summary>Gets a value indicating whether enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Determines whether a monster matches configured Golden Boss rules.</summary>
    /// <param name="monsterNumber">Monster definition number.</param>
    /// <param name="designation">Monster designation name.</param>
    /// <returns><c>true</c> if Golden Boss.</returns>
    public bool IsGoldenBoss(short monsterNumber, string? designation) =>
        this.Configuration.GoldenMonsterNumbers.Contains(monsterNumber)
        || (!string.IsNullOrWhiteSpace(designation) && designation.Contains("Golden", StringComparison.OrdinalIgnoreCase));

    /// <summary>Awards shards to <paramref name="login"/> for a Golden Boss kill.</summary>
    /// <param name="login">Account login name.</param>
    /// <param name="amount">Shard count.</param>
    /// <returns>Updated shard balance.</returns>
    public int AwardShards(string login, int amount)
    {
        if (string.IsNullOrWhiteSpace(login) || amount <= 0)
        {
            return 0;
        }

        var total = 0;
        this.Store.Update(data =>
        {
            var acc = GetOrCreateAccount(data, login);
            acc.Shards += amount;
            total = acc.Shards;
        });

        return total;
    }

    /// <summary>Validates crafting/upgrading into <paramref name="tierId"/>.</summary>
    /// <param name="login">Account login name.</param>
    /// <param name="tierId">Target tier ID.</param>
    /// <param name="bag">Unlocked bag items.</param>
    /// <param name="zen">Player Zen.</param>
    /// <param name="consumedSlots">Selected duplicate mount slots of previous tier to consume.</param>
    /// <returns>Check result.</returns>
    public MountActionResult PlanCraft(string login, string tierId, IReadOnlyList<MountBagItem> bag, long zen, out List<byte> consumedSlots)
    {
        consumedSlots = [];
        if (!this.Enabled)
        {
            return MountActionResult.Disabled;
        }

        var tier = this.Configuration.Tiers.FirstOrDefault(t => string.Equals(t.Id, tierId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tier is null)
        {
            return MountActionResult.NotFound;
        }

        var shards = this.Store.Read(d => d.Accounts.TryGetValue(login, out var a) ? a.Shards : 0);
        if (shards < tier.ShardsRequired)
        {
            return MountActionResult.NotEnoughShards;
        }

        if (zen < tier.ZenCost)
        {
            return MountActionResult.NotEnoughZen;
        }

        if (!string.IsNullOrWhiteSpace(tier.PreviousTierId) && tier.PreviousMountsRequired > 0)
        {
            var prev = this.Configuration.Tiers.FirstOrDefault(t => string.Equals(t.Id, tier.PreviousTierId, StringComparison.OrdinalIgnoreCase));
            if (prev is null)
            {
                return MountActionResult.NotFound;
            }

            var matched = bag
                .Where(b => !b.Locked && b.Group == prev.ItemGroup && b.Number == prev.ItemNumber && b.Level == prev.ItemLevel)
                .Take(tier.PreviousMountsRequired)
                .ToList();
            if (matched.Count < tier.PreviousMountsRequired)
            {
                return MountActionResult.MissingPreviousMount;
            }

            consumedSlots.AddRange(matched.Select(m => m.Slot));
        }

        return MountActionResult.Ok;
    }

    /// <summary>Consumes shards and unlocks <paramref name="tier"/> in the account's collection, setting it active.</summary>
    /// <param name="login">Account login.</param>
    /// <param name="tier">Tier definition.</param>
    /// <param name="utcNow">Timestamp.</param>
    public void CompleteCraft(string login, MountTierDefinition tier, DateTime utcNow)
    {
        this.Store.Update(data =>
        {
            var acc = GetOrCreateAccount(data, login);
            acc.Shards = Math.Max(0, acc.Shards - tier.ShardsRequired);
            acc.Unlocked.TryAdd(tier.Id, utcNow);
            acc.ActiveTierId = tier.Id;
        });
    }

    /// <summary>Validates salvaging one duplicate bag mount of <paramref name="tierId"/> into shards.</summary>
    /// <param name="tierId">Tier ID.</param>
    /// <param name="bag">Bag items.</param>
    /// <param name="slot">Matched bag slot.</param>
    /// <returns>Check result.</returns>
    public MountActionResult PlanSalvage(string tierId, IReadOnlyList<MountBagItem> bag, out byte slot)
    {
        slot = 0;
        if (!this.Enabled)
        {
            return MountActionResult.Disabled;
        }

        var tier = this.Configuration.Tiers.FirstOrDefault(t => string.Equals(t.Id, tierId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tier is null)
        {
            return MountActionResult.NotFound;
        }

        var hit = bag.FirstOrDefault(b => !b.Locked && b.Group == tier.ItemGroup && b.Number == tier.ItemNumber && b.Level == tier.ItemLevel);
        if (hit is null)
        {
            return MountActionResult.MissingDuplicateToSalvage;
        }

        slot = hit.Slot;
        return MountActionResult.Ok;
    }

    /// <summary>Selects an unlocked tier as active collection mount for modest PvE bonuses.</summary>
    /// <param name="login">Account login.</param>
    /// <param name="tierId">Tier ID.</param>
    /// <returns>Result.</returns>
    public MountActionResult ActivateTier(string login, string tierId)
    {
        if (!this.Enabled)
        {
            return MountActionResult.Disabled;
        }

        var tier = this.Configuration.Tiers.FirstOrDefault(t => string.Equals(t.Id, tierId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tier is null)
        {
            return MountActionResult.NotFound;
        }

        var result = MountActionResult.NotUnlocked;
        this.Store.Update(data =>
        {
            if (data.Accounts.TryGetValue(login, out var acc) && acc.Unlocked.ContainsKey(tier.Id))
            {
                acc.ActiveTierId = tier.Id;
                result = MountActionResult.Ok;
            }
        });

        return result;
    }

    /// <summary>Gets the active unlocked mount tier for <paramref name="login"/>, or <c>null</c>.</summary>
    /// <param name="login">Account login.</param>
    /// <returns>Active tier or <c>null</c>.</returns>
    public MountTierDefinition? GetActiveTier(string login)
    {
        if (!this.Enabled || string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var activeId = this.Store.Read(d => d.Accounts.TryGetValue(login, out var a) ? a.ActiveTierId : string.Empty);
        return string.IsNullOrWhiteSpace(activeId)
            ? null
            : this.Configuration.Tiers.FirstOrDefault(t => string.Equals(t.Id, activeId, StringComparison.OrdinalIgnoreCase));
    }

    private static MountAccountState GetOrCreateAccount(MountChainStoreData data, string login)
    {
        if (!data.Accounts.TryGetValue(login, out var acc))
        {
            acc = new MountAccountState();
            data.Accounts[login] = acc;
        }

        return acc;
    }
}
