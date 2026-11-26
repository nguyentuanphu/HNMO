// <copyright file="TimetableSourceCollector.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.CastleSiege;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.S6Events;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.PlugIns;

/// <summary>
/// Collects scheduled mini-games, invasions, and world bosses from a live <see cref="IGameContext"/>
/// or from stored admin configuration into <see cref="TimetableSourceItem"/> entries with EN/VI maps and drops.
/// </summary>
public static class TimetableSourceCollector
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");

    /// <summary>Builds metadata lookup entry for an invasion plug-in type.</summary>
    /// <param name="plugInType">The invasion plug-in type.</param>
    /// <param name="configuration">The periodic invasion configuration.</param>
    /// <param name="gameConfiguration">Optional game configuration for map name lookup.</param>
    /// <param name="isActiveNow">Whether the invasion is currently active on the server.</param>
    /// <returns>The timetable source item.</returns>
    public static TimetableSourceItem FromInvasion(
        Type plugInType,
        PeriodicInvasionConfiguration configuration,
        GameConfiguration? gameConfiguration,
        bool isActiveNow = false)
    {
        var (id, nameEn, nameVi) = DescribeInvasion(plugInType);
        var find = configuration as FindTheGmConfiguration;
        var monsterIds = configuration.Mobs.Select(mob => (int)mob.MonsterId).ToHashSet();
        var items = gameConfiguration?.Monsters?
            .Where(monster => monster.IsActive && monsterIds.Contains(monster.Number))
            .SelectMany(monster => monster.DropItemGroups)
            .SelectMany(group => group.PossibleItems).Where(item => item.IsActive).Distinct().Take(8).ToArray() ?? [];
        var reward = find is null ? string.Empty : string.Join(" + ", new[]
        {
            find.RewardCoins > 0 ? $"{find.RewardCoins} {find.RewardCoinType}" : string.Empty,
            find.RewardZen > 0 ? $"{find.RewardZen:N0} Zen" : string.Empty,
        }.Where(part => part.Length > 0));
        var (mapsEn, mapsVi) = DescribeInvasionMaps(configuration, gameConfiguration);
        return new TimetableSourceItem
        {
            Id = id,
            Category = "invasion",
            NameEn = nameEn,
            NameVi = nameVi,
            MapsEn = mapsEn,
            MapsVi = mapsVi,
            DropsEn = find is null ? string.Join(", ", items.Select(item => item.Name.GetTranslation(English))) : reward,
            DropsVi = find is null ? string.Join(", ", items.Select(item => item.Name.GetTranslation(Vietnamese))) : reward,
            DailyTimes = configuration.Timetable.ToList(),
            Duration = configuration.TaskDuration > TimeSpan.Zero ? configuration.TaskDuration : TimeSpan.FromMinutes(20),
            IsLiveNow = isActiveNow,
            InvasionConfiguration = configuration,
            WinnerReward = find is not null ? (find.RewardCoins, find.RewardCoinType, find.RewardZen) : null,
        };
    }

    /// <summary>Collects all active mini-game, invasion, and world boss entries from a live <see cref="IGameContext"/>.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="timetableConfiguration">The timetable configuration.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="serverId">An optional game server whose schedules should be included.</param>
    /// <param name="defaultBossServerId">The server where the host starts bosses without an explicit server.</param>
    /// <returns>The source items.</returns>
    public static List<TimetableSourceItem> CollectFromContext(
        IGameContext gameContext,
        TimetableConfiguration timetableConfiguration,
        DateTime utcNow,
        byte? serverId = null,
        byte? defaultBossServerId = null)
    {
        var items = new List<TimetableSourceItem>();
        var gameConfig = gameContext.Configuration;
        var scheduleTimeZone = serverId.HasValue ? gameContext.ServerTimeZone : TimeZoneInfo.Utc;

        if (timetableConfiguration.ShowMiniGames)
        {
            foreach (var plugIn in gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<IPeriodicMiniGameStartPlugIn>())
            {
                if (plugIn is IScheduledPlugIn scheduled && TryReadTaskConfiguration(plugIn) is { } taskConfig
                    && (serverId is not { } miniGameServer || taskConfig.IsEnabledOnServer(miniGameServer)))
                {
                    var isLive = plugIn is IPeriodicMiniGameStartPlugIn mg && mg.IsEventActive(gameContext);
                    items.Add(FromMiniGame(plugIn.Key, taskConfig, gameConfig, scheduled.GetNextStartUtc(utcNow, scheduleTimeZone), isLive));
                }
            }
        }

        if (timetableConfiguration.ShowMiniGames
            && gameContext is IGameServerContext server
            && gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<CastleSiegePlugIn>().FirstOrDefault()
                ?.GetContext(gameContext) is { Configuration: { Enabled: true, CastleSiegeMapDefinition: { IsActive: true } siegeMap } } siege
            && server.ServerConfiguration.Maps.Any(map => map.IsActive && map.Number == siegeMap.Number)
            && FromCastleSiege(siege, utcNow) is { } battle)
        {
            items.Add(battle);
        }

        if (timetableConfiguration.ShowInvasions)
        {
            foreach (var plugIn in gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>())
            {
                var config = TryReadInvasionConfiguration(plugIn);
                if (config is not null && (serverId is not { } invasionServer || config.IsEnabledOnServer(invasionServer)))
                {
                    var invasion = FromInvasion(plugIn.GetType(), config, gameConfig);
                    if (serverId.HasValue && plugIn is IScheduledPlugIn scheduled)
                    {
                        invasion.ExplicitNextStartUtc = scheduled.GetNextStartUtc(utcNow, scheduleTimeZone);
                    }

                    if (serverId.HasValue && plugIn is IInvasionScheduleSpawns spawning)
                    {
                        invasion.ActiveInvasionSpawns = spawning.ActiveSpawns(gameContext);
                    }

                    items.Add(invasion);
                }
            }
        }

        if (timetableConfiguration.ShowMiniGames
            && gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<S6EventsPlugIn>().FirstOrDefault() is { } s6Events
            && s6Events.Configuration is { Enabled: true, LmsEnabled: true } s6Cfg)
        {
            var arenaDef = gameConfig.Maps?.FirstOrDefault(m => m.Number == s6Cfg.LmsArenaMapNumber);
            var mapEn = arenaDef?.Name.GetTranslation(English) ?? "Duel Arena";
            var mapVi = arenaDef?.Name.GetTranslation(Vietnamese) ?? "Đấu Trường";
            var dailyTimes = s6Cfg.LmsDailyTimes
                .Select(raw => TimeOnly.TryParseExact(raw?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? (TimeOnly?)t : null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToList();
            items.Add(new TimetableSourceItem
            {
                Id = "event-lms",
                Category = "event",
                NameEn = "Last Man Standing",
                NameVi = "Chiến Trường Sinh Tử (LMS)",
                MapsEn = mapEn,
                MapsVi = mapVi,
                DropsEn = $"{s6Cfg.LmsRewardCoins} {s6Cfg.LmsRewardCoinType} + {s6Cfg.LmsRewardZen:N0} Zen",
                DropsVi = $"{s6Cfg.LmsRewardCoins} {s6Cfg.LmsRewardCoinType} + {s6Cfg.LmsRewardZen:N0} Zen",
                DailyTimes = dailyTimes,
                Duration = TimeSpan.FromMinutes(Math.Max(1, s6Cfg.LmsRegistrationMinutes + s6Cfg.LmsDurationMinutes)),
                IsLiveNow = s6Events.IsLmsActive,
                WinnerReward = (s6Cfg.LmsRewardCoins, s6Cfg.LmsRewardCoinType, s6Cfg.LmsRewardZen),
            });
        }

        if (timetableConfiguration.ShowWorldBosses
            && gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<WorldBossesPlugIn>().FirstOrDefault() is { } worldBossPlugIn)
        {
            var bossConfig = worldBossPlugIn.Configuration ?? new WorldBossesConfiguration();
            var timeZone = TimetableEngine.ResolveTimeZone(bossConfig.TimeZone);
            var engine = new WorldBossEngine(bossConfig, WorldBossStore.Open(bossConfig.FileName));
            var activeFights = engine.AllAlive().ToDictionary(a => a.BossId, a => a, StringComparer.OrdinalIgnoreCase);
            foreach (var boss in bossConfig.Bosses.Where(b => b.Enabled
                         && (serverId is not { } bossServer || (b.GameServerId >= 0 ? b.GameServerId : defaultBossServerId ?? bossServer) == bossServer)))
            {
                activeFights.TryGetValue(boss.Id, out var alive);
                items.Add(FromWorldBoss(boss, timeZone, utcNow, gameConfig, alive));
            }
        }

        // The Arena's scheduled battles, as the Arena program published them (not in the Arena itself).
        if (!HNMO.Interfaces.HnmoSeasonInstance.IsArena)
        {
            items.AddRange(Arena.ArenaScheduleNoticesPlugIn.Schedules().Select(FromArena));
        }

        return items;
    }

    /// <summary>Projects the configured Castle Siege battle and its actual active window.</summary>
    /// <param name="siege">The running Castle Siege event.</param>
    /// <param name="utcNow">The schedule reference time in UTC.</param>
    /// <returns>The Castle Siege battle, when configured.</returns>
    public static TimetableSourceItem? FromCastleSiege(CastleSiegeContext siege, DateTime utcNow)
    {
        var next = siege.Schedule.GetNextPeriod(CastleSiegeState.Start, utcNow);
        var active = siege.IsEventRunning && siege.StateEndTimeUtc > utcNow;
        if (!active && next is null)
        {
            return null;
        }

        var map = siege.Configuration.CastleSiegeMapDefinition;
        return new TimetableSourceItem
        {
            Id = "event-castle-siege",
            Category = "event",
            NameEn = "Castle Siege",
            NameVi = "Công thành chiến",
            MapsEn = map?.Name.GetTranslation(English) ?? string.Empty,
            MapsVi = map?.Name.GetTranslation(Vietnamese) ?? string.Empty,
            ExplicitNextStartUtc = next?.StartUtc,
            ExplicitEndsAtUtc = active ? siege.StateEndTimeUtc : next?.EndUtc,
            IsLiveNow = active,
        };
    }

    /// <summary>Creates a timetable source item for one of the Arena's scheduled battles.</summary>
    /// <param name="entry">The battle as the Arena published it.</param>
    /// <returns>The timetable source item.</returns>
    public static TimetableSourceItem FromArena(Persistence.ArenaScheduleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var royale = entry.Kind == "royale";
        WorldBossSchedule.TryParseTimes(entry.Times, out var times);
        Arena.ArenaGuildWarEngine.TryParseDays(entry.Days, out var days);
        return new TimetableSourceItem
        {
            Id = royale ? "arena-royale" : "arena-guildwar",
            Category = "event",
            NameEn = royale ? "Arena: battle royale" : "Arena: guild war",
            NameVi = royale ? "Đấu trường: đấu sinh tồn" : "Đấu trường: bang chiến",
            MapsEn = royale ? "Arena (Lorencia)" : "Arena (Valley of Loren)",
            MapsVi = royale ? "Đấu trường (Lorencia)" : "Đấu trường (Thung lũng Loren)",
            DailyTimes = times,
            AllowedDays = days,
            Duration = TimeSpan.FromMinutes(royale ? 12 : 15),
            ExplicitNextStartUtc = entry.NextStartUtc,
        };
    }

    /// <summary>Creates a timetable source item for a mini-game start plug-in.</summary>
    /// <param name="type">The mini-game type.</param>
    /// <param name="configuration">The periodic task configuration.</param>
    /// <param name="gameConfiguration">Optional game configuration.</param>
    /// <param name="explicitNextStartUtc">Optional precomputed start.</param>
    /// <param name="isLiveNow">Whether active now.</param>
    /// <returns>The timetable source item.</returns>
    public static TimetableSourceItem FromMiniGame(
        MiniGameType type,
        PeriodicTaskConfiguration configuration,
        GameConfiguration? gameConfiguration,
        DateTime? explicitNextStartUtc = null,
        bool isLiveNow = false)
    {
        var (nameEn, nameVi, mapEn, mapVi, dropsEn, dropsVi) = DescribeMiniGame(type, gameConfiguration);
        return new TimetableSourceItem
        {
            Id = $"event-{type.ToString().ToLowerInvariant()}",
            Category = "event",
            NameEn = nameEn,
            NameVi = nameVi,
            MapsEn = mapEn,
            MapsVi = mapVi,
            DropsEn = dropsEn,
            DropsVi = dropsVi,
            DailyTimes = configuration.Timetable.ToList(),
            Duration = configuration.TaskDuration > TimeSpan.Zero ? configuration.TaskDuration : TimeSpan.FromMinutes(20),
            ExplicitNextStartUtc = explicitNextStartUtc,
            IsLiveNow = isLiveNow,
        };
    }

    /// <summary>Creates a timetable source item for a world boss definition.</summary>
    /// <param name="boss">The boss definition.</param>
    /// <param name="timeZone">The boss schedule time zone.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="gameConfiguration">Optional game configuration.</param>
    /// <param name="aliveFight">Optional active fight info.</param>
    /// <returns>The timetable source item.</returns>
    public static TimetableSourceItem FromWorldBoss(
        WorldBossDefinition boss,
        TimeZoneInfo timeZone,
        DateTime utcNow,
        GameConfiguration? gameConfiguration,
        WorldBossAliveState? aliveFight = null)
    {
        var mapDef = gameConfiguration?.Maps?.FirstOrDefault(m => m.Number == boss.MapNumber);
        var mapEn = mapDef?.Name.GetTranslation(English) ?? $"Map #{boss.MapNumber}";
        var mapVi = mapDef?.Name.GetTranslation(Vietnamese) ?? mapEn;
        var (dropsEn, dropsVi) = DescribeBossRewards(boss, gameConfiguration);

        return new TimetableSourceItem
        {
            Id = $"boss-{boss.Id}",
            Category = "boss",
            NameEn = string.IsNullOrWhiteSpace(boss.NameEn) ? boss.Id : boss.NameEn,
            NameVi = string.IsNullOrWhiteSpace(boss.NameVi) ? (string.IsNullOrWhiteSpace(boss.NameEn) ? boss.Id : boss.NameEn) : boss.NameVi,
            MapsEn = mapEn,
            MapsVi = mapVi,
            DropsEn = dropsEn,
            DropsVi = dropsVi,
            DailyTimes = WorldBossSchedule.TimesOfDay(boss),
            AllowedDays = boss.Days?.ToList() ?? [],
            Duration = TimeSpan.FromMinutes(Math.Max(1, boss.DespawnMinutes)),
            ExplicitNextStartUtc = WorldBossSchedule.Next(boss, timeZone, utcNow),
            ExplicitEndsAtUtc = aliveFight?.DespawnAtUtc,
            IsLiveNow = aliveFight is not null,
            BossConfiguration = boss,
        };
    }

    private static PeriodicTaskConfiguration? TryReadTaskConfiguration(IPeriodicTaskPlugIn plugIn)
    {
        var prop = plugIn.GetType().GetProperty("Configuration");
        return prop?.GetValue(plugIn) as PeriodicTaskConfiguration
            ?? (plugIn as ISupportDefaultCustomConfiguration)?.CreateDefaultConfig() as PeriodicTaskConfiguration;
    }

    private static PeriodicInvasionConfiguration? TryReadInvasionConfiguration(IPeriodicTaskPlugIn plugIn)
    {
        var prop = plugIn.GetType().GetProperty("Configuration");
        return prop?.GetValue(plugIn) as PeriodicInvasionConfiguration
            ?? (plugIn as ISupportDefaultCustomConfiguration)?.CreateDefaultConfig() as PeriodicInvasionConfiguration;
    }

    private static (string Id, string NameEn, string NameVi) DescribeInvasion(Type plugInType)
        => plugInType.Name switch
        {
            nameof(GoldenInvasionPlugIn) => ("invasion-golden", "Golden Invasion", "Xâm lăng Rồng Vàng / Quái Vàng"),
            nameof(RedDragonInvasionPlugIn) => ("invasion-red-dragon", "Red Dragon Invasion", "Xâm lăng Rồng Đỏ"),
            nameof(WhiteWizardInvasionPlugIn) => ("invasion-white-wizard", "White Wizard Corps", "Phù Thủy Trắng"),
            nameof(RabbitInvasionPlugIn) => ("invasion-rabbits", "Lunar Rabbit Invasion", "Thỏ Ngọc"),
            nameof(SnowmanInvasionPlugIn) => ("invasion-snowmen", "Snowman Invasion", "Người Tuyết & Ông Già Noel"),
            nameof(FindTheGmInvasionPlugIn) => ("invasion-find-gm", "Find the GM", "Truy tìm GM"),
            _ => ($"invasion-{plugInType.Name.ToLowerInvariant()}", plugInType.Name, plugInType.Name),
        };

    private static (string En, string Vi) DescribeInvasionMaps(PeriodicInvasionConfiguration config, GameConfiguration? gameConfiguration)
    {
        var mapIds = config.Mobs.SelectMany(m => m.MapIds).Distinct().ToList();
        if (mapIds.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var en = new List<string>();
        var vi = new List<string>();
        var maps = gameConfiguration?.Maps;
        foreach (var mapId in mapIds)
        {
            var map = maps?.FirstOrDefault(m => m.Number == mapId);
            var fallback = mapId switch
            {
                0 => "Lorencia",
                2 => "Devias",
                3 => "Noria",
                7 => "Atlans",
                8 => "Tarkan",
                _ => $"Map #{mapId}",
            };
            en.Add(map?.Name.GetTranslation(English) ?? fallback);
            vi.Add(map?.Name.GetTranslation(Vietnamese) ?? map?.Name.GetTranslation(English) ?? fallback);
        }

        return (string.Join(", ", en.Distinct()), string.Join(", ", vi.Distinct()));
    }

    private static (string NameEn, string NameVi, string MapEn, string MapVi, string DropsEn, string DropsVi) DescribeMiniGame(
        MiniGameType type,
        GameConfiguration? gameConfiguration)
    {
        var defs = gameConfiguration?.MiniGameDefinitions?.Where(d => d.IsActive && d.Type == type).ToList() ?? [];
        var mapEn = defs.FirstOrDefault()?.Entrance?.Map?.Name.GetTranslation(English);
        var mapVi = defs.FirstOrDefault()?.Entrance?.Map?.Name.GetTranslation(Vietnamese) ?? mapEn;
        var rewardItems = defs.SelectMany(definition => definition.Rewards)
            .SelectMany(reward => reward.ItemReward?.PossibleItems ?? Enumerable.Empty<ItemDefinition>())
            .Where(item => item.IsActive).Distinct().Take(4).ToArray();
        var dropsEn = string.Join(", ", rewardItems.Select(item => item.Name.GetTranslation(English)));
        var dropsVi = string.Join(", ", rewardItems.Select(item => item.Name.GetTranslation(Vietnamese)));

        return type switch
        {
            MiniGameType.BloodCastle => (
                "Blood Castle",
                "Huyết Lâu (Blood Castle)",
                mapEn ?? "Blood Castle (enter via Spirit Archangel in Devias)",
                mapVi ?? "Huyết Lâu (vào qua Thiên Sứ ở Devias)",
                dropsEn,
                dropsVi),
            MiniGameType.DevilSquare => (
                "Devil Square",
                "Quảng Trường Quỷ (Devil Square)",
                mapEn ?? "Devil Square (enter via Charon in Noria)",
                mapVi ?? "Quảng Trường Quỷ (vào qua Charon ở Noria)",
                dropsEn,
                dropsVi),
            MiniGameType.ChaosCastle => (
                "Chaos Castle",
                "Hỗn Nguyên Lâu (Chaos Castle)",
                mapEn ?? "Chaos Castle (use Armor of Guardsman)",
                mapVi ?? "Hỗn Nguyên Lâu (dùng Giáp Hộ Vệ)",
                dropsEn,
                dropsVi),
            MiniGameType.IllusionTemple => (
                "Illusion Temple",
                "Bạch Long Điện (Illusion Temple)",
                mapEn ?? "Illusion Temple (Mirage in Devias)",
                mapVi ?? "Bạch Long Điện (Mirage ở Devias)",
                dropsEn,
                dropsVi),
            MiniGameType.Kanturu => (
                "Kanturu / Nightmare",
                "Tháp Tinh Luyện Kanturu",
                mapEn ?? "Kanturu Remains -> Refinery Tower",
                mapVi ?? "Kanturu Di Tích -> Tháp Tinh Luyện",
                dropsEn,
                dropsVi),
            _ => (
                type.ToString(),
                type.ToString(),
                mapEn ?? "Event Map",
                mapVi ?? "Bản đồ sự kiện",
                dropsEn,
                dropsVi),
        };
    }

    private static (string En, string Vi) DescribeBossRewards(WorldBossDefinition boss, GameConfiguration? gameConfiguration)
    {
        var tiers = new[] { boss.Top1, boss.Top2, boss.Top3, boss.Top10, boss.Participation, boss.LastHit };
        var coinTotal = tiers.Where(t => t is not null && t.Coins > 0).Select(t => $"{t!.Coins} {t.CoinType}").Distinct().Take(3).ToList();
        var itemNames = new List<string>();
        var items = gameConfiguration?.Items;
        foreach (var item in tiers.Where(t => t is not null).SelectMany(t => t!.Items ?? []))
        {
            var name = items?.FirstOrDefault(i => i.Group == item.Group && i.Number == item.Number)?.Name.GetTranslation(English);
            if (!string.IsNullOrWhiteSpace(name))
            {
                itemNames.Add(item.Level > 0 ? $"{name} +{item.Level}" : name);
            }
            else
            {
                itemNames.Add($"Item {item.Group}:{item.Number}");
            }
        }

        var parts = new List<string>();
        if (coinTotal.Count > 0)
        {
            parts.Add(string.Join(" / ", coinTotal));
        }

        if (itemNames.Count > 0)
        {
            parts.Add(string.Join(", ", itemNames.Distinct().Take(4)));
        }

        if (parts.Count == 0)
        {
            return ("Rank coins, Zen & Hunt points", "Xu theo hạng, Zen & Điểm săn boss");
        }

        var text = string.Join(" · ", parts);
        return (text, text);
    }
}
