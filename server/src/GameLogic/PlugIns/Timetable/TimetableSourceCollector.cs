// <copyright file="TimetableSourceCollector.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Globalization;
using System.Text;
using HNMO.DataModel.Configuration;
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
        var (id, nameEn, nameVi, defaultDropsEn, defaultDropsVi) = DescribeInvasion(plugInType, configuration);
        var (mapsEn, mapsVi) = DescribeInvasionMaps(configuration, gameConfiguration);
        return new TimetableSourceItem
        {
            Id = id,
            Category = "invasion",
            NameEn = nameEn,
            NameVi = nameVi,
            MapsEn = mapsEn,
            MapsVi = mapsVi,
            DropsEn = defaultDropsEn,
            DropsVi = defaultDropsVi,
            DailyTimes = configuration.Timetable.ToList(),
            Duration = configuration.TaskDuration > TimeSpan.Zero ? configuration.TaskDuration : TimeSpan.FromMinutes(20),
            IsLiveNow = isActiveNow,
        };
    }

    /// <summary>Collects all active mini-game, invasion, and world boss entries from a live <see cref="IGameContext"/>.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="timetableConfiguration">The timetable configuration.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>The source items.</returns>
    public static List<TimetableSourceItem> CollectFromContext(
        IGameContext gameContext,
        TimetableConfiguration timetableConfiguration,
        DateTime utcNow)
    {
        var items = new List<TimetableSourceItem>();
        var gameConfig = gameContext.Configuration;

        if (timetableConfiguration.ShowMiniGames)
        {
            foreach (var plugIn in gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<IPeriodicMiniGameStartPlugIn>())
            {
                if (plugIn is IScheduledPlugIn scheduled && TryReadTaskConfiguration(plugIn) is { } taskConfig)
                {
                    var isLive = plugIn is IPeriodicMiniGameStartPlugIn mg && mg.IsEventActive(gameContext);
                    items.Add(FromMiniGame(plugIn.Key, taskConfig, gameConfig, scheduled.GetNextStartUtc(utcNow, TimeZoneInfo.Utc), isLive));
                }
            }
        }

        if (timetableConfiguration.ShowInvasions)
        {
            foreach (var plugIn in gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>())
            {
                var config = TryReadInvasionConfiguration(plugIn);
                if (config is not null)
                {
                    items.Add(FromInvasion(plugIn.GetType(), config, gameConfig));
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
            });
        }

        if (timetableConfiguration.ShowWorldBosses
            && gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<WorldBossesPlugIn>().FirstOrDefault() is { } worldBossPlugIn)
        {
            var bossConfig = worldBossPlugIn.Configuration ?? new WorldBossesConfiguration();
            var timeZone = TimetableEngine.ResolveTimeZone(bossConfig.TimeZone);
            var engine = new WorldBossEngine(bossConfig, WorldBossStore.Open(bossConfig.FileName));
            var activeFights = engine.AllAlive().ToDictionary(a => a.BossId, a => a, StringComparer.OrdinalIgnoreCase);
            foreach (var boss in bossConfig.Bosses.Where(b => b.Enabled))
            {
                activeFights.TryGetValue(boss.Id, out var alive);
                items.Add(FromWorldBoss(boss, timeZone, utcNow, gameConfig, alive));
            }
        }

        return items;
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

    private static (string Id, string NameEn, string NameVi, string DropsEn, string DropsVi) DescribeInvasion(
        Type plugInType,
        PeriodicInvasionConfiguration configuration)
    {
        if (plugInType == typeof(GoldenInvasionPlugIn))
        {
            return (
                "invasion-golden",
                "Golden Invasion",
                "Xâm lăng Rồng Vàng / Quái Vàng",
                "Boxes of Kundun +1 to +5, Excellent items, Jewels",
                "Hộp Kundun +1 đến +5, đồ Hoàn Hảo, Ngọc");
        }

        if (plugInType == typeof(RedDragonInvasionPlugIn))
        {
            return (
                "invasion-red-dragon",
                "Red Dragon Invasion",
                "Xâm lăng Rồng Đỏ",
                "Jewels of Bless & Soul, Zen",
                "Ngọc Ước Nguyện, Ngọc Tâm Linh, Zen");
        }

        if (plugInType == typeof(WhiteWizardInvasionPlugIn))
        {
            return (
                "invasion-white-wizard",
                "White Wizard Corps",
                "Phù Thủy Trắng",
                "Ring of Wizardry, Jewels of Bless & Soul",
                "Nhẫn Phù Thủy, Ngọc Ước Nguyện & Tâm Linh");
        }

        if (plugInType == typeof(RabbitInvasionPlugIn))
        {
            return (
                "invasion-rabbits",
                "Lunar Rabbit Invasion",
                "Thỏ Ngọc",
                "Box of Heaven, Jewels, Firecracker / Chaos items",
                "Hộp Thiên Đường, Ngọc, vật phẩm sự kiện");
        }

        if (plugInType == typeof(SnowmanInvasionPlugIn))
        {
            return (
                "invasion-snowmen",
                "Snowman Invasion",
                "Người Tuyết & Ông Già Noel",
                "Jewels of Bless/Soul/Life, Zen, Festive rewards",
                "Ngọc Bless/Soul/Life, Zen, quà Giáng Sinh");
        }

        if (plugInType == typeof(FindTheGmInvasionPlugIn))
        {
            var findGm = configuration as FindTheGmConfiguration ?? new FindTheGmConfiguration();
            var coins = findGm.RewardCoins > 0 ? $"{findGm.RewardCoins} {findGm.RewardCoinType}" : string.Empty;
            var zenEn = findGm.RewardZen > 0 ? $"{findGm.RewardZen:N0} Zen" : string.Empty;
            var reward = string.Join(" + ", new[] { coins, zenEn }.Where(s => !string.IsNullOrEmpty(s)));
            return (
                "invasion-find-gm",
                "Find the GM",
                "Truy tìm GM",
                string.IsNullOrEmpty(reward) ? "Special GM gift" : $"First finder(s) ({findGm.MaxWinners}): {reward}",
                string.IsNullOrEmpty(reward) ? "Quà đặc biệt từ GM" : $"Top {findGm.MaxWinners} tìm thấy trước: {reward}");
        }

        return (
            $"invasion-{plugInType.Name.ToLowerInvariant()}",
            plugInType.Name,
            plugInType.Name,
            "Event monster drops",
            "Vật phẩm rơi từ quái sự kiện");
    }

    private static (string En, string Vi) DescribeInvasionMaps(PeriodicInvasionConfiguration config, GameConfiguration? gameConfiguration)
    {
        var mapIds = config.Mobs.SelectMany(m => m.MapIds).Distinct().ToList();
        if (mapIds.Count == 0)
        {
            return ("Lorencia, Noria, Devias", "Lorencia, Noria, Devias");
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
        var rewardSbEn = new StringBuilder();
        var mapEn = defs.FirstOrDefault()?.Entrance?.Map?.Name.GetTranslation(English);
        var mapVi = defs.FirstOrDefault()?.Entrance?.Map?.Name.GetTranslation(Vietnamese) ?? mapEn;

        foreach (var reward in defs.SelectMany(d => d.Rewards ?? Enumerable.Empty<MiniGameReward>()).Take(4))
        {
            var itemName = reward.ItemReward?.PossibleItems?.FirstOrDefault()?.Name.GetTranslation(English);
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                if (rewardSbEn.Length > 0)
                {
                    rewardSbEn.Append(", ");
                }

                rewardSbEn.Append(itemName);
            }
        }

        var extractedDrops = rewardSbEn.Length > 0 ? rewardSbEn.ToString() : null;

        return type switch
        {
            MiniGameType.BloodCastle => (
                "Blood Castle",
                "Huyết Lâu (Blood Castle)",
                mapEn ?? "Blood Castle (enter via Spirit Archangel in Devias)",
                mapVi ?? "Huyết Lâu (vào qua Thiên Sứ ở Devias)",
                extractedDrops ?? "Jewel of Chaos, Jewels, EXP & Zen",
                extractedDrops ?? "Ngọc Hỗn Nguyên, Ngọc, EXP & Zen"),
            MiniGameType.DevilSquare => (
                "Devil Square",
                "Quảng Trường Quỷ (Devil Square)",
                mapEn ?? "Devil Square (enter via Charon in Noria)",
                mapVi ?? "Quảng Trường Quỷ (vào qua Charon ở Noria)",
                extractedDrops ?? "High EXP waves, Zen, Jewels",
                extractedDrops ?? "Luyện cấp EXP cao, Zen, Ngọc"),
            MiniGameType.ChaosCastle => (
                "Chaos Castle",
                "Hỗn Nguyên Lâu (Chaos Castle)",
                mapEn ?? "Chaos Castle (use Armor of Guardsman)",
                mapVi ?? "Hỗn Nguyên Lâu (dùng Giáp Hộ Vệ)",
                extractedDrops ?? "Ancient items, Jewels of Creation/Soul/Bless/Life",
                extractedDrops ?? "Đồ Thần, Ngọc Sáng Tạo/Tâm Linh/Ước Nguyện/Sinh Mệnh"),
            MiniGameType.IllusionTemple => (
                "Illusion Temple",
                "Bạch Long Điện (Illusion Temple)",
                mapEn ?? "Illusion Temple (Mirage in Devias)",
                mapVi ?? "Bạch Long Điện (Mirage ở Devias)",
                extractedDrops ?? "Jewel of Chaos, PvP team victory rewards",
                extractedDrops ?? "Ngọc Hỗn Nguyên, phần thưởng thắng đội PvP"),
            MiniGameType.Kanturu => (
                "Kanturu / Nightmare",
                "Tháp Tinh Luyện Kanturu",
                mapEn ?? "Kanturu Remains -> Refinery Tower",
                mapVi ?? "Kanturu Di Tích -> Tháp Tinh Luyện",
                extractedDrops ?? "Nightmare drops, 380 items, Gemstone refining",
                extractedDrops ?? "Rơi đồ 380, tinh luyện Đá Tạo Hóa"),
            _ => (
                type.ToString(),
                type.ToString(),
                mapEn ?? "Event Map",
                mapVi ?? "Bản đồ sự kiện",
                extractedDrops ?? "Event rewards",
                extractedDrops ?? "Phần thưởng sự kiện"),
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
