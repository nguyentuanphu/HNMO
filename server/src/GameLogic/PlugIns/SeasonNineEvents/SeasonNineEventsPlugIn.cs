// <copyright file="SeasonNineEventsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.S6Events;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.Properties;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.Pathfinding;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging;

/// <summary>
/// Season 9 events and Uruk Mountain encounter plug-in (disabled by default):
/// Acheron Guardian, Arca Battle (+ Trophy of Battle booty exchange), Chaos Castle Survival,
/// Tormented Square, Invasion Manager (Moon Rabbit / Pouch of Blessing / Fire Flame Ghost / New Year Horses &amp; Goats),
/// and Uruk Mountain Lord Silvester elemental guards (678-680).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineEventsPlugIn_Name), Description = nameof(PlugInResources.SeasonNineEventsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E4A92C61-8B3F-4D1A-A52C-9F04E7B62109")]
public sealed class SeasonNineEventsPlugIn :
    IPeriodicTaskPlugIn,
    IAttackableGotHitPlugIn,
    IAttackableGotKilledPlugIn,
    IPlayerStateChangedPlugIn,
    IPlayerTalkToNpcPlugIn,
    ISupportCustomConfiguration<SeasonNineEventsConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>GrantedBy identifier recorded on coin grant rows.</summary>
    public const string GrantedBy = "HNMO S9 Events";

    private const short LordSilvesterNumber = 673;
    private const short TrophyOfBattleGroup = 13;
    private const short TrophyOfBattleNumber = 147;
    private const short ElementMasterAdnielNpc = 582;

    private readonly ConcurrentDictionary<string, DateTime> _activeDeadlinesUtc = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<Monster>> _spawnedMobsByEvent = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(GameMap Map, byte X, byte Y)> _pendingUrukSpawns = new();
    private DateTime _lastTickUtc = DateTime.MinValue;
    private SeasonNineEventsStore? _store;
    private int _tickRunning;

    /// <inheritdoc />
    public SeasonNineEventsConfiguration? Configuration { get; set; }

    /// <summary>Gets whether a given Season 9 event key (<c>acheron</c>, <c>arca</c>, <c>ccs</c>, <c>tsquare</c>) is currently running.</summary>
    /// <param name="eventKey">Event key.</param>
    /// <returns><see langword="true"/> when live.</returns>
    public bool IsEventLive(string eventKey) =>
        this._activeDeadlinesUtc.TryGetValue(eventKey, out var deadline) && DateTime.UtcNow < deadline;

    /// <summary>Gets active <see cref="SeasonNineEventsPlugIn"/> from <paramref name="gameContext"/>.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static SeasonNineEventsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<SeasonNineEventsPlugIn>().FirstOrDefault();

    /// <summary>Triggers a Season 9 event or Uruk guard wave immediately from the Admin Panel.</summary>
    /// <param name="gameContexts">Running game server contexts.</param>
    /// <param name="eventKey">Key: <c>acheron</c>, <c>arca</c>, <c>ccs</c>, <c>tsquare</c>, <c>uruk</c>, or <c>invasion:&lt;id&gt;</c>.</param>
    /// <returns>Status code (<c>Ok</c>, <c>AlreadyRunning</c>, <c>SeasonMismatch</c>, or <c>Disabled</c>).</returns>
    public static async ValueTask<string> TriggerEventNowAsync(IEnumerable<IGameContext> gameContexts, string eventKey)
    {
        foreach (var ctx in gameContexts)
        {
            if (Get(ctx) is not { } plugin || plugin.Configuration is not { Enabled: true } cfg)
            {
                continue;
            }

            if (cfg.SeasonNineOnly && !HnmoSeasonInstance.IsSeasonNine)
            {
                return "SeasonMismatch";
            }

            if (plugin.IsEventLive(eventKey))
            {
                return "AlreadyRunning";
            }

            return await plugin.StartEventByKeyAsync(ctx, cfg, eventKey, manual: true).ConfigureAwait(false);
        }

        return "Disabled";
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new SeasonNineEventsConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this.Configuration is not { Enabled: true } cfg || (cfg.SeasonNineOnly && !HnmoSeasonInstance.IsSeasonNine))
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - this._lastTickUtc < TimeSpan.FromSeconds(5))
        {
            return;
        }

        this._lastTickUtc = nowUtc;
        if (Interlocked.Exchange(ref this._tickRunning, 1) == 1)
        {
            return;
        }

        try
        {
            await this.CleanupExpiredEventsAsync(gameContext, nowUtc).ConfigureAwait(false);
            while (this._pendingUrukSpawns.TryDequeue(out var pending))
            {
                await this.SpawnUrukGuardsAsync(gameContext, pending.Map, cfg, pending.X, pending.Y).ConfigureAwait(false);
            }

            var store = this.GetStore(cfg);
            if (cfg.AcheronGuardianEnabled
                && SeasonNineEventsEngine.ResolveDueScheduleSlot(cfg.AcheronGuardianDailyTimes, cfg.TimeZone, nowUtc, cfg.AcheronGuardianActiveDays) is { } acheronSlot
                && store.TryClaimScheduleSlot("acheron", acheronSlot))
            {
                await this.StartEventByKeyAsync(gameContext, cfg, "acheron", manual: false).ConfigureAwait(false);
            }

            if (cfg.ArcaBattleEnabled
                && SeasonNineEventsEngine.ResolveDueScheduleSlot(cfg.ArcaBattleDailyTimes, cfg.TimeZone, nowUtc, cfg.ArcaBattleActiveDays) is { } arcaSlot
                && store.TryClaimScheduleSlot("arca", arcaSlot))
            {
                await this.StartEventByKeyAsync(gameContext, cfg, "arca", manual: false).ConfigureAwait(false);
            }

            if (cfg.ChaosCastleSurvivalEnabled
                && SeasonNineEventsEngine.ResolveDueScheduleSlot(cfg.ChaosCastleSurvivalDailyTimes, cfg.TimeZone, nowUtc) is { } ccsSlot
                && store.TryClaimScheduleSlot("ccs", ccsSlot))
            {
                await this.StartEventByKeyAsync(gameContext, cfg, "ccs", manual: false).ConfigureAwait(false);
            }

            if (cfg.TormentedSquareEnabled
                && SeasonNineEventsEngine.ResolveDueScheduleSlot(cfg.TormentedSquareDailyTimes, cfg.TimeZone, nowUtc) is { } tsSlot
                && store.TryClaimScheduleSlot("tsquare", tsSlot))
            {
                await this.StartEventByKeyAsync(gameContext, cfg, "tsquare", manual: false).ConfigureAwait(false);
            }

            if (cfg.InvasionManagerEnabled)
            {
                foreach (var inv in cfg.Invasions.Where(i => i.Enabled && !string.IsNullOrWhiteSpace(i.Id)))
                {
                    if (SeasonNineEventsEngine.ResolveDueScheduleSlot(inv.DailyTimes, cfg.TimeZone, nowUtc, null, inv.DayOfWeek) is { } invSlot
                        && store.TryClaimScheduleSlot($"invasion:{inv.Id}", invSlot))
                    {
                        await this.StartInvasionAsync(gameContext, cfg, inv).ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref this._tickRunning, 0);
        }
    }

    /// <inheritdoc />
    public void AttackableGotHit(IAttackable attackable, IAttacker attacker, HitInfo hitInfo)
    {
        if (this.Configuration is not { Enabled: true, UrukMountainBossGuardsEnabled: true } cfg
            || (cfg.SeasonNineOnly && !HnmoSeasonInstance.IsSeasonNine))
        {
            return;
        }

        if (attackable is Monster monster
            && monster.Definition.Number == LordSilvesterNumber
            && monster.CurrentMap is { Definition.Number: 100 or 101 } map
            && this.GetStore(cfg).TryClaimUrukGuardCooldown(DateTime.UtcNow, cfg.UrukMountainGuardCooldownSeconds))
        {
            this._pendingUrukSpawns.Enqueue((map, monster.Position.X, monster.Position.Y));
        }
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (this.Configuration is not { Enabled: true } cfg || (cfg.SeasonNineOnly && !HnmoSeasonInstance.IsSeasonNine))
        {
            return;
        }

        if (killed is Monster monster
            && monster.Definition.Number == LordSilvesterNumber
            && killer is Player slayer)
        {
            var charName = slayer.SelectedCharacter?.Name ?? "Hero";
            var loginName = slayer.Account?.LoginName ?? string.Empty;
            await this.DeliverRewardAsync(
                slayer.GameContext,
                slayer,
                loginName,
                charName,
                cfg.UrukMountainBossRewardZen,
                cfg.UrukMountainBossRewardCoins,
                cfg.UrukMountainBossRewardCoinType,
                -1,
                0,
                0,
                0,
                "Lord Silvester (Uruk Mountain) Slain",
                "Tiêu Diệt Chúa Tể Silvester (Núi Uruk)",
                cfg,
                $"s9-uruk-{Guid.NewGuid():N}").ConfigureAwait(false);

            this.GetStore(cfg).RecordHistory(new S9EventHistoryEntry
            {
                EventKey = "uruk",
                EventName = "Lord Silvester (Uruk Mountain)",
                CompletedAtUtc = DateTime.UtcNow,
                Summary = $"{charName} defeated Lord Silvester",
                Count = 1,
            });
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (eventArgs.HasBeenHandled
            || this.Configuration is not { Enabled: true } cfg
            || (cfg.SeasonNineOnly && !HnmoSeasonInstance.IsSeasonNine)
            || npc.Definition is null)
        {
            return;
        }

        if (npc.Definition.Number == ElementMasterAdnielNpc)
        {
            var available = S6EventsEngine.CountStackedItems(player.Inventory, TrophyOfBattleGroup, TrophyOfBattleNumber);
            if (available > 0 && await S6EventsEngine.TryConsumeStackedItemsAsync(player, TrophyOfBattleGroup, TrophyOfBattleNumber, 1).ConfigureAwait(false))
            {
                var rate = SeasonNineEventsEngine.CalculateTrophyOfBattleMixRate(
                    available,
                    cfg.ArcaBattleTrophyMixBaseRateLow,
                    cfg.ArcaBattleTrophyMixBaseRateMid,
                    cfg.ArcaBattleTrophyMixBaseRateHigh,
                    cfg.ArcaBattleTrophyMixAddRate);
                var success = Rand.NextInt(0, 100) < rate;
                if (success)
                {
                    await this.DeliverRewardAsync(
                        player.GameContext,
                        player,
                        player.Account?.LoginName ?? string.Empty,
                        player.SelectedCharacter?.Name ?? string.Empty,
                        1_000_000,
                        20,
                        cfg.ArcaBattleWinnerCoinType,
                        14,
                        14,
                        0,
                        1,
                        "Trophy of Battle Booty Mix",
                        "Hợp Thành Chiến Lợi Phẩm Arca",
                        cfg,
                        $"s9-bootymix-{Guid.NewGuid():N}").ConfigureAwait(false);
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Trophy of Battle Booty Mix succeeded ({rate}%)!",
                        $"Hợp thành Chiến Lợi Phẩm Arca thành công ({rate}%)!").ConfigureAwait(false);
                }
                else
                {
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Trophy of Battle Booty Mix failed ({rate}%).",
                        $"Hợp thành Chiến Lợi Phẩm Arca thất bại ({rate}%).").ConfigureAwait(false);
                }

                eventArgs.HasBeenHandled = true;
                eventArgs.LeavesDialogOpen = false;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration is not { Enabled: true } cfg || currentState != PlayerState.EnteredWorld)
        {
            return;
        }

        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var store = this.GetStore(cfg);
        foreach (var owed in store.GetOwedRewardsFor(login, player.SelectedCharacter?.Name))
        {
            if (await SeasonNineEventsEngine.TryDeliverGoodsAsync(player, owed).ConfigureAwait(false))
            {
                store.RemoveOwedReward(owed.RewardId);
                await this.SendLocalizedBlueAsync(
                    player,
                    $"Delivered queued Season 9 reward: {owed.ReasonEn}.",
                    $"Đã phát phần thưởng Season 9 chờ nhận: {owed.ReasonVi}.").ConfigureAwait(false);
            }
        }
    }

    /// <summary>Handles player command queries/entry for Season 9 events (<c>/acheron</c>, <c>/arcabattle</c>, <c>/ccs</c>, <c>/tsquare</c>).</summary>
    /// <param name="player">Issuing player.</param>
    /// <param name="eventKey">Target event key.</param>
    /// <param name="disabledEn">English message when plugin is inactive.</param>
    /// <param name="disabledVi">Vietnamese message when plugin is inactive.</param>
    /// <returns>A task representing execution.</returns>
    public static async ValueTask ExecuteChatCommandAsync(Player player, string eventKey, string disabledEn, string disabledVi)
    {
        if (Get(player.GameContext) is not { } plugin)
        {
            var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await player.ShowBlueMessageAsync(isVi ? disabledVi : disabledEn).ConfigureAwait(false);
            return;
        }

        await plugin.HandleEventCommandAsync(player, eventKey).ConfigureAwait(false);
    }

    /// <summary>Handles player command queries/entry for Season 9 events (<c>/acheron</c>, <c>/arcabattle</c>, <c>/ccs</c>, <c>/tsquare</c>).</summary>
    /// <param name="player">Issuing player.</param>
    /// <param name="eventKey">Target event key.</param>
    /// <returns>A task representing execution.</returns>
    public async ValueTask HandleEventCommandAsync(Player player, string eventKey)
    {
        if (this.Configuration is not { Enabled: true } cfg || (cfg.SeasonNineOnly && !HnmoSeasonInstance.IsSeasonNine))
        {
            await this.SendLocalizedBlueAsync(player, "Season 9 events are currently disabled.", "Sự kiện Season 9 hiện đang tắt.").ConfigureAwait(false);
            return;
        }

        var tz = TimetableEngine.ResolveTimeZone(cfg.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        switch (eventKey)
        {
            case "acheron":
            {
                if (this.IsEventLive("acheron"))
                {
                    await this.WarpPlayerToMapAsync(player, 92, 85, 45).ConfigureAwait(false);
                    await this.SendLocalizedBlueAsync(player, "Warped to Acheron Guardian battlefield!", "Đã dịch chuyển tới chiến trường Hộ Vệ Acheron!").ConfigureAwait(false);
                }
                else
                {
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Acheron Guardian schedule ({cfg.AcheronGuardianActiveDays}): {string.Join(", ", cfg.AcheronGuardianDailyTimes)}.",
                        $"Lịch Hộ Vệ Acheron ({cfg.AcheronGuardianActiveDays}): {string.Join(", ", cfg.AcheronGuardianDailyTimes)}.").ConfigureAwait(false);
                }

                break;
            }
            case "arca":
            {
                if (this.IsEventLive("arca"))
                {
                    await this.WarpPlayerToMapAsync(player, 92, 115, 155).ConfigureAwait(false);
                    await this.SendLocalizedBlueAsync(player, "Warped to Arca Battle!", "Đã dịch chuyển tới Chiến Trường Arca!").ConfigureAwait(false);
                }
                else
                {
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Arca Battle ({cfg.ArcaBattleActiveDays}): {string.Join(", ", cfg.ArcaBattleDailyTimes)}.",
                        $"Chiến Trường Arca ({cfg.ArcaBattleActiveDays}): {string.Join(", ", cfg.ArcaBattleDailyTimes)}.").ConfigureAwait(false);
                }

                break;
            }
            case "ccs":
            {
                var stage = SeasonNineEventsEngine.ResolveTournamentStage(
                    localNow.Day,
                    cfg.ChaosCastleSurvivalEliminationDays,
                    cfg.ChaosCastleSurvivalSemiFinalDays,
                    cfg.ChaosCastleSurvivalFinalDay);
                var level = player.Attributes?[Attributes.Stats.Level] ?? 1;
                if (this.IsEventLive("ccs"))
                {
                    if (level < cfg.ChaosCastleSurvivalMinLevel)
                    {
                        await this.SendLocalizedBlueAsync(
                            player,
                            $"Requires Level {cfg.ChaosCastleSurvivalMinLevel} to enter Chaos Castle Survival.",
                            $"Cần cấp {cfg.ChaosCastleSurvivalMinLevel} để vào Hỗn Nguyên Lâu Sinh Tồn.").ConfigureAwait(false);
                        return;
                    }

                    await this.WarpPlayerToMapAsync(player, 97, 33, 91).ConfigureAwait(false);
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Entered Chaos Castle Survival ({stage})!",
                        $"Đã vào Hỗn Nguyên Lâu Sinh Tồn ({stage})!").ConfigureAwait(false);
                }
                else
                {
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Chaos Castle Survival ({stage}, Lv.{cfg.ChaosCastleSurvivalMinLevel}+) is currently closed.",
                        $"Hỗn Nguyên Lâu Sinh Tồn ({stage}, Cấp {cfg.ChaosCastleSurvivalMinLevel}+) hiện đang đóng.").ConfigureAwait(false);
                }

                break;
            }
            case "tsquare":
            {
                var stage = SeasonNineEventsEngine.ResolveTournamentStage(
                    localNow.Day,
                    cfg.TormentedSquareEliminationDays,
                    cfg.TormentedSquareSemiFinalDays,
                    cfg.TormentedSquareFinalDay);
                var level = player.Attributes?[Attributes.Stats.Level] ?? 1;
                if (this.IsEventLive("tsquare"))
                {
                    if (level < cfg.TormentedSquareMinLevel)
                    {
                        await this.SendLocalizedBlueAsync(
                            player,
                            $"Requires Level {cfg.TormentedSquareMinLevel} to enter Tormented Square.",
                            $"Cần cấp {cfg.TormentedSquareMinLevel} để vào Quảng Trường Đau Khổ.").ConfigureAwait(false);
                        return;
                    }

                    await this.WarpPlayerToMapAsync(player, 102, 127, 128).ConfigureAwait(false);
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Entered Tormented Square ({stage})!",
                        $"Đã vào Quảng Trường Đau Khổ ({stage})!").ConfigureAwait(false);
                }
                else
                {
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Tormented Square ({stage}): {string.Join(", ", cfg.TormentedSquareDailyTimes)}.",
                        $"Quảng Trường Đau Khổ ({stage}): {string.Join(", ", cfg.TormentedSquareDailyTimes)}.").ConfigureAwait(false);
                }

                break;
            }
        }
    }

    private async ValueTask<string> StartEventByKeyAsync(IGameContext gameContext, SeasonNineEventsConfiguration cfg, string eventKey, bool manual)
    {
        if (eventKey.StartsWith("invasion:", StringComparison.Ordinal))
        {
            var invId = eventKey["invasion:".Length..];
            var entry = cfg.Invasions.FirstOrDefault(i => string.Equals(i.Id, invId, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return "Disabled";
            }

            await this.StartInvasionAsync(gameContext, cfg, entry).ConfigureAwait(false);
            return "Ok";
        }

        switch (eventKey)
        {
            case "acheron":
                if (!cfg.AcheronGuardianEnabled && !manual)
                {
                    return "Disabled";
                }

                this._activeDeadlinesUtc["acheron"] = DateTime.UtcNow.AddMinutes(Math.Clamp(cfg.AcheronGuardianDurationMinutes, 1, 120));
                foreach (var obelisk in SeasonNineEventsEngine.AcheronObelisks)
                {
                    if (await gameContext.GetMapAsync((ushort)obelisk.MapNumber).ConfigureAwait(false) is { } map)
                    {
                        await this.SpawnEventMobsAsync(gameContext, map, "acheron", obelisk.MonsterNumber, 1, obelisk.X, obelisk.Y).ConfigureAwait(false);
                        foreach (var guardId in SeasonNineEventsEngine.AcheronGuardianMonsterIds.Take(3))
                        {
                            await this.SpawnEventMobsAsync(gameContext, map, "acheron", guardId, 1, obelisk.X, obelisk.Y).ConfigureAwait(false);
                        }
                    }
                }

                await this.BroadcastGoldenAsync(
                    gameContext,
                    "[Acheron Guardian] Obelisks are under attack! Defend Alkmaar & Debenter (/acheron)!",
                    "[Hộ Vệ Acheron] Tháp Ánh Sáng đang bị tấn công! Hãy bảo vệ Alkmaar & Debenter (/acheron)!").ConfigureAwait(false);
                this.GetStore(cfg).RecordHistory(new S9EventHistoryEntry { EventKey = "acheron", EventName = "Acheron Guardian", CompletedAtUtc = DateTime.UtcNow, Summary = "Started", Count = 3 });
                return "Ok";

            case "arca":
                if (!cfg.ArcaBattleEnabled && !manual)
                {
                    return "Disabled";
                }

                this._activeDeadlinesUtc["arca"] = DateTime.UtcNow.AddMinutes(Math.Clamp(cfg.ArcaBattleDurationMinutes, 1, 120));
                foreach (var obelisk in SeasonNineEventsEngine.AcheronObelisks)
                {
                    if (await gameContext.GetMapAsync((ushort)obelisk.MapNumber).ConfigureAwait(false) is { } map)
                    {
                        await this.SpawnEventMobsAsync(gameContext, map, "arca", obelisk.MonsterNumber, 1, obelisk.X, obelisk.Y).ConfigureAwait(false);
                        foreach (var elemId in SeasonNineEventsEngine.ArcaBattleMonsterIds.Take(3))
                        {
                            await this.SpawnEventMobsAsync(gameContext, map, "arca", elemId, 2, obelisk.X, obelisk.Y).ConfigureAwait(false);
                        }
                    }
                }

                await this.BroadcastGoldenAsync(
                    gameContext,
                    "[Arca Battle] Guild elemental war has begun! Enter via /arcabattle!",
                    "[Chiến Trường Arca] Đại chiến Guild nguyên tố đã bắt đầu! Vào bằng /arcabattle!").ConfigureAwait(false);
                this.GetStore(cfg).RecordHistory(new S9EventHistoryEntry { EventKey = "arca", EventName = "Arca Battle", CompletedAtUtc = DateTime.UtcNow, Summary = "Started", Count = 3 });
                return "Ok";

            case "ccs":
                if (!cfg.ChaosCastleSurvivalEnabled && !manual)
                {
                    return "Disabled";
                }

                this._activeDeadlinesUtc["ccs"] = DateTime.UtcNow.AddMinutes(Math.Clamp(cfg.ChaosCastleSurvivalDurationMinutes, 1, 60));
                if (await gameContext.GetMapAsync(97).ConfigureAwait(false) is { } ccsMap)
                {
                    foreach (var mobId in SeasonNineEventsEngine.ChaosCastleSurvivalMonsterIds)
                    {
                        await this.SpawnEventMobsAsync(gameContext, ccsMap, "ccs", mobId, 15, null, null).ConfigureAwait(false);
                    }
                }

                await this.BroadcastGoldenAsync(
                    gameContext,
                    $"[Chaos Castle Survival] Portal is OPEN (Lv.{cfg.ChaosCastleSurvivalMinLevel}+)! Type /ccs to enter!",
                    $"[Hỗn Nguyên Lâu Sinh Tồn] Cổng đã MỞ (Cấp {cfg.ChaosCastleSurvivalMinLevel}+)! Gõ /ccs để tham gia!").ConfigureAwait(false);
                this.GetStore(cfg).RecordHistory(new S9EventHistoryEntry { EventKey = "ccs", EventName = "Chaos Castle Survival", CompletedAtUtc = DateTime.UtcNow, Summary = "Started", Count = 30 });
                return "Ok";

            case "tsquare":
                if (!cfg.TormentedSquareEnabled && !manual)
                {
                    return "Disabled";
                }

                var tsMinutes = Math.Max(5, cfg.TormentedSquareRoundMinutes * Math.Clamp(cfg.TormentedSquareRoundsCount, 1, 15));
                this._activeDeadlinesUtc["tsquare"] = DateTime.UtcNow.AddMinutes(tsMinutes);
                if (await gameContext.GetMapAsync(102).ConfigureAwait(false) is { } tsMap)
                {
                    var roundOne = SeasonNineEventsEngine.TormentedSquareRounds[0];
                    await this.SpawnEventMobsAsync(gameContext, tsMap, "tsquare", roundOne.MonsterNumber, 20, null, null).ConfigureAwait(false);
                }

                await this.BroadcastGoldenAsync(
                    gameContext,
                    "[Tormented Square] Survival waves OPEN! Form a party and type /tsquare!",
                    "[Quảng Trường Đau Khổ] Chiến trường sinh tồn ĐÃ MỞ! Lập tổ đội và gõ /tsquare!").ConfigureAwait(false);
                this.GetStore(cfg).RecordHistory(new S9EventHistoryEntry { EventKey = "tsquare", EventName = "Tormented Square", CompletedAtUtc = DateTime.UtcNow, Summary = "Started", Count = 20 });
                return "Ok";

            case "uruk":
                if (await gameContext.GetMapAsync(100).ConfigureAwait(false) is { } urukMap)
                {
                    await this.SpawnUrukGuardsAsync(gameContext, urukMap, cfg, 130, 114).ConfigureAwait(false);
                    return "Ok";
                }

                return "Disabled";

            default:
                return "Disabled";
        }
    }

    private async ValueTask StartInvasionAsync(IGameContext gameContext, SeasonNineEventsConfiguration cfg, S9InvasionScheduleEntry entry)
    {
        var eventKey = $"invasion:{entry.Id}";
        this._activeDeadlinesUtc[eventKey] = DateTime.UtcNow.AddMinutes(Math.Clamp(entry.DurationMinutes, 1, 120));
        var spawnedTotal = 0;
        foreach (var mapNum in entry.MapNumbers)
        {
            if (await gameContext.GetMapAsync((ushort)mapNum).ConfigureAwait(false) is { } map)
            {
                await this.SpawnEventMobsAsync(gameContext, map, eventKey, entry.MonsterNumber, entry.MobCount, null, null).ConfigureAwait(false);
                spawnedTotal += entry.MobCount;
            }
        }

        await this.BroadcastGoldenAsync(
            gameContext,
            $"[Invasion] {entry.NameEn} has started across {entry.MapNumbers.Count} map(s)!",
            $"[Xâm Lăng] {entry.NameVi} đã xuất hiện tại {entry.MapNumbers.Count} bản đồ!").ConfigureAwait(false);
        this.GetStore(cfg).RecordHistory(new S9EventHistoryEntry
        {
            EventKey = eventKey,
            EventName = entry.NameEn,
            CompletedAtUtc = DateTime.UtcNow,
            Summary = "Invasion Started",
            Count = spawnedTotal,
        });
    }

    private async ValueTask SpawnUrukGuardsAsync(IGameContext gameContext, GameMap map, SeasonNineEventsConfiguration cfg, byte centerX, byte centerY)
    {
        var countPerType = Math.Clamp(cfg.UrukMountainGuardSpawnCount / 3 + 1, 1, 5);
        foreach (var guardId in SeasonNineEventsEngine.UrukMountainGuardMonsterIds)
        {
            await this.SpawnEventMobsAsync(gameContext, map, "uruk-guards", guardId, (ushort)countPerType, centerX, centerY).ConfigureAwait(false);
        }

        this._activeDeadlinesUtc["uruk-guards"] = DateTime.UtcNow.AddMinutes(15);
        await this.BroadcastGoldenAsync(
            gameContext,
            "[Uruk Mountain] Lord Silvester has summoned Elemental Guards (678-680)!",
            "[Núi Uruk] Chúa Tể Silvester đã triệu hồi Hộ Vệ Nguyên Tố (678-680)!").ConfigureAwait(false);
    }

    private async ValueTask SpawnEventMobsAsync(
        IGameContext gameContext,
        GameMap gameMap,
        string eventKey,
        short monsterNumber,
        ushort count,
        byte? x,
        byte? y)
    {
        var definition = gameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == monsterNumber);
        if (definition is null)
        {
            return;
        }

        var bucket = this._spawnedMobsByEvent.GetOrAdd(eventKey, _ => []);
        for (var i = 0; i < count; i++)
        {
            Point? point = x.HasValue && y.HasValue
                ? new Point(x.Value, y.Value)
                : gameMap.Terrain.RandomWalkableCoordinate;
            if (point is null)
            {
                continue;
            }

            var area = new MonsterSpawnArea
            {
                GameMap = gameMap.Definition,
                MonsterDefinition = definition,
                SpawnTrigger = SpawnTrigger.OnceAtEventStart,
                Quantity = 1,
                X1 = point.Value.X,
                X2 = point.Value.X,
                Y1 = point.Value.Y,
                Y2 = point.Value.Y,
            };
            var mob = new Monster(
                area,
                definition,
                gameMap,
                gameContext.DropGenerator,
                new BasicMonsterIntelligence(),
                gameContext.PlugInManager,
                gameContext.PathFinderPool);
            mob.Initialize();
            await gameMap.AddAsync(mob).ConfigureAwait(false);
            mob.OnSpawn();
            lock (bucket)
            {
                bucket.Add(mob);
            }
        }
    }

    private async ValueTask CleanupExpiredEventsAsync(IGameContext gameContext, DateTime utcNow)
    {
        foreach (var pair in this._activeDeadlinesUtc.ToArray())
        {
            if (utcNow < pair.Value)
            {
                continue;
            }

            if (this._activeDeadlinesUtc.TryRemove(pair.Key, out _)
                && this._spawnedMobsByEvent.TryRemove(pair.Key, out var mobs))
            {
                foreach (var mob in mobs)
                {
                    try
                    {
                        if (mob.CurrentMap is { } map)
                        {
                            await map.RemoveAsync(mob).ConfigureAwait(false);
                        }

                        mob.Dispose();
                    }
                    catch (Exception ex)
                    {
                        gameContext.LoggerFactory.CreateLogger<SeasonNineEventsPlugIn>()
                            .LogDebug(ex, "Cleanup error removing mob for {EventKey}.", pair.Key);
                    }
                }
            }
        }
    }

    private async ValueTask DeliverRewardAsync(
        IGameContext gameContext,
        Player? player,
        string loginName,
        string characterName,
        uint zen,
        int coins,
        CashShopCoinType coinType,
        int itemGroup,
        short itemNumber,
        byte itemLevel,
        int itemCount,
        string reasonEn,
        string reasonVi,
        SeasonNineEventsConfiguration cfg,
        string reference)
    {
        if (coins > 0 && !string.IsNullOrWhiteSpace(loginName))
        {
            await MissionCoinGranter.GrantAsync(
                gameContext,
                new CoinGrant(loginName, coinType, coins, reasonEn, reference),
                GrantedBy).ConfigureAwait(false);
        }

        if (zen > 0 || (itemGroup >= 0 && itemCount > 0))
        {
            var owed = new S9EventOwedReward
            {
                LoginName = loginName,
                CharacterName = characterName,
                ReasonEn = reasonEn,
                ReasonVi = reasonVi,
                Zen = zen,
                ItemGroup = itemGroup,
                ItemNumber = itemNumber,
                ItemLevel = itemLevel,
                ItemCount = itemCount,
            };

            if (player is null || !await SeasonNineEventsEngine.TryDeliverGoodsAsync(player, owed).ConfigureAwait(false))
            {
                this.GetStore(cfg).QueueOwedReward(owed);
            }
        }
    }

    private async ValueTask WarpPlayerToMapAsync(Player player, short mapNumber, byte x, byte y)
    {
        var mapDef = player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == mapNumber)
            ?? player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == 0);
        if (mapDef is null)
        {
            return;
        }

        await player.WarpToAsync(new ExitGate { Map = mapDef, X1 = x, X2 = x, Y1 = y, Y2 = y }).ConfigureAwait(false);
    }

    private async ValueTask BroadcastGoldenAsync(IGameContext gameContext, string en, string vi)
    {
        await gameContext.ForEachPlayerAsync(async recipient =>
        {
            var isVi = string.Equals(recipient.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await recipient.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(isVi ? vi : en, MessageType.GoldenCenter)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private ValueTask SendLocalizedBlueAsync(Player player, string en, string vi)
    {
        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        return player.ShowBlueMessageAsync(isVi ? vi : en);
    }

    private SeasonNineEventsStore GetStore(SeasonNineEventsConfiguration cfg) =>
        this._store ??= SeasonNineEventsStore.Open(cfg.FileName);
}
