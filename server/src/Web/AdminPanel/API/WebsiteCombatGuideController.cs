// <copyright file="WebsiteCombatGuideController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Public combat and loot rules from the running game servers.</summary>
/// <param name="servers">Known game servers.</param>
/// <param name="persistence">Host settings.</param>
/// <param name="cache">Short-lived public projection cache.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website/combat-guide")]
public sealed class WebsiteCombatGuideController(IDictionary<int, IGameServer> servers, IPersistenceContextProvider persistence, IMemoryCache cache) : ControllerBase
{
    private const int MaximumServers = 32;
    private const int MaximumBossesPerServer = 16;

    /// <summary>Reads the public guide without loading player or fight histories.</summary>
    /// <returns>A bounded rules snapshot or an unavailable response.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var settings = await PlugInSettings.ReadActiveAsync(persistence, typeof(WebsitePlugIn), () => new WebsiteConfiguration(), this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (settings is not { CombatGuideEnabled: true })
        {
            return this.Ok(new { enabled = false });
        }

        var snapshot = cache.GetOrCreate(nameof(WebsiteCombatGuideController), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return servers.Values.Where(server => server is IGameServerContextProvider)
                .OrderBy(server => server.Id).Take(MaximumServers).Select(BuildServer).ToArray();
        });

        if (snapshot is not { Length: > 0 })
        {
            return this.StatusCode(503);
        }

        return this.Ok(new { enabled = true, hit = BuildHitRules(), servers = snapshot });
    }

    internal static object BuildHitRules()
    {
        const int exampleAttackRate = 900;
        const int exampleDefenseRate = 600;
        const int exampleAttackerLevel = 200;
        const int exampleDefenderLevel = 300;
        return new
        {
            multiplier = AttackableExtensions.PvpHitMultiplier,
            emptyShare = AttackableExtensions.PvpHitEmptyShare,
            duelFactor = AttackableExtensions.DuelDamageFactor,
            penalties = AttackableExtensions.GetPvpHitPenalties().ToArray().Select(p => new { p.LevelGap, p.Penalty }).ToArray(),
            example = new
            {
                attackRate = exampleAttackRate,
                defenseRate = exampleDefenseRate,
                attackerLevel = exampleAttackerLevel,
                defenderLevel = exampleDefenderLevel,
                chance = AttackableExtensions.GetPvpHitChance(exampleAttackRate, exampleDefenseRate, exampleAttackerLevel, exampleDefenderLevel),
            },
        };
    }

    internal static object BuildLootRules(MonsterAiConfiguration settings)
    {
        const int firstDamage = 750;
        const int finishingDamage = 250;
        return new
        {
            settings.SplitExpByDamage,
            settings.DropToTopDamageDealer,
            example = new
            {
                firstDamage,
                finishingDamage,
                firstShare = settings.SplitExpByDamage ? AttackableNpcBase.GetDamageExperienceShare(firstDamage, firstDamage + finishingDamage) : 0,
                finishingShare = settings.SplitExpByDamage ? AttackableNpcBase.GetDamageExperienceShare(finishingDamage, firstDamage + finishingDamage) : 1,
            },
        };
    }

    internal static object? BuildLimits(CombatLimitsConfiguration? limits)
        => limits is { Enabled: true }
            ? new
            {
                damageCap = Math.Max(0, limits.MaxDamagePerHit),
                pvpReduction = Math.Clamp(limits.PvpDamageReductionPercent, 0, 95),
                siegeMapReduction = Math.Clamp(limits.CastleSiegeDamageReductionPercent, 0, 95),
            }
            : null;

    internal static object BuildBossRule(WorldBossDefinition boss)
        => new
        {
            name = new { en = ShortText(boss.NameEn), vi = ShortText(boss.NameVi) },
            partyRank = boss.RankBy == WorldBossRankBy.Party,
            boss.TopAlsoGetParticipation,
            boss.ParticipationMinimumPercent,
            boss.ParticipationMinimumDamage,
            hasParticipation = !WorldBossEngine.RewardOf(boss, "participation").IsEmpty,
            hasLastHit = !WorldBossEngine.RewardOf(boss, "lasthit").IsEmpty,
            topRanks = new[] { 1, 2, 3, WorldBossEngine.PaidRanks }
                .Where(rank => WorldBossEngine.RankTier(rank) is { } tier && !WorldBossEngine.RewardOf(boss, tier).IsEmpty).ToArray(),
        };

    private static object BuildServer(IGameServer server)
    {
        var context = ((IGameServerContextProvider)server).Context;
        var configuredBosses = WorldBossesPlugIn.Get(context)?.GetHostedBosses(context.Id).Take(MaximumBossesPerServer + 1).ToArray() ?? [];
        return new
        {
            server.Id,
            name = ShortText(server.Description),
            loot = BuildLootRules(MonsterAiPlugIn.For(context.PlugInManager)),
            limits = BuildLimits(context.FeaturePlugIns.GetPlugIn<CombatLimitsPlugIn>()?.Configuration),
            bosses = configuredBosses.Take(MaximumBossesPerServer).Select(BuildBossRule).ToArray(),
            moreBosses = configuredBosses.Length > MaximumBossesPerServer,
        };
    }

    private static string ShortText(string? value) => value is null ? string.Empty : value[..Math.Min(120, value.Length)];
}
