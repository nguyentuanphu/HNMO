// <copyright file="WebsiteEventGuideProjection.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Quests;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Interfaces;

/// <summary>Publishes actual event rules and configured NPC quests without collecting any player records.</summary>
public static class WebsiteEventGuideProjection
{
    private const int MaximumEvents = 32;
    private const int MaximumQuests = 48;
    private const int MaximumTiers = 12;
    private const int MaximumDetails = 12;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");

    /// <summary>Projects one selected server's rules and event times.</summary>
    /// <param name="configuration">The loaded game definitions.</param>
    /// <param name="serverMaps">Maps actually hosted on the server.</param>
    /// <param name="schedule">The computed timetable for this server.</param>
    /// <param name="sources">The typed settings used to compute the schedule.</param>
    /// <returns>Bounded event and quest data for the public guide.</returns>
    public static object Build(GameConfiguration configuration, IEnumerable<GameMapDefinition> serverMaps, TimetableSnapshot schedule, IReadOnlyList<TimetableSourceItem> sources)
    {
        var maps = serverMaps.Where(map => map.IsActive).ToArray();
        var eventSources = sources.ToLookup(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var miniGames = configuration.MiniGameDefinitions.Where(game => game.IsActive)
            .ToLookup(game => $"event-{game.Type.ToString().ToLowerInvariant()}", StringComparer.Ordinal);
        var quests = maps.SelectMany(map => map.MonsterSpawns
                .Where(spawn => spawn.Quantity > 0 && spawn.MonsterDefinition is { IsActive: true })
                .SelectMany(spawn => spawn.MonsterDefinition!.Quests.Select(quest => new { Map = map, Npc = spawn.MonsterDefinition!, Quest = quest })))
            .GroupBy(row => (Npc: row.Npc.Number, row.Quest.Group, row.Quest.Number))
            .OrderBy(group => group.First().Quest.MinimumCharacterLevel).ThenBy(group => group.Key.Group).ThenBy(group => group.Key.Number)
            .Take(MaximumQuests + 1).Select(group => Quest(group.First().Quest, group.First().Npc, group.Select(row => row.Map).Distinct())).ToArray();
        return new
        {
            events = schedule.Entries.Take(MaximumEvents).Select(row =>
            {
                var source = eventSources[row.Id].FirstOrDefault();
                var actualBoss = source?.BossConfiguration;
                return new
                {
                    id = Text(row.Id),
                    category = row.Category is "event" or "invasion" or "boss" ? row.Category : "custom",
                    name = new PublicName(Text(row.NameEn), Text(row.NameVi)),
                    nextStartUtc = row.NextStartUtc is { } next && next >= schedule.NowUtc ? UtcInstant(next) : null,
                    endsAtUtc = source?.IsLiveNow == true ? UtcInstant(row.EndsAtUtc) : null,
                    isActive = source?.IsLiveNow == true,
                    tiers = miniGames[row.Id].OrderBy(game => game.GameLevel).Take(MaximumTiers).Select(MiniGame).ToArray(),
                    invasion = source?.InvasionConfiguration is { } invasion ? Invasion(invasion, source.ActiveInvasionSpawns, maps, configuration) : null,
                    boss = actualBoss is null ? null : Boss(actualBoss, configuration),
                    winner = source?.WinnerReward is { } reward ? new { reward.Coins, coinType = reward.CoinType.ToString(), reward.Zen } : null,
                };
            }).ToArray(),
            eventsTruncated = schedule.Entries.Count > MaximumEvents,
            quests = quests.Take(MaximumQuests).ToArray(),
            questsTruncated = quests.Length > MaximumQuests,
        };
    }

    /// <summary>Limits host-configured public labels to a single bounded string.</summary>
    /// <param name="value">The configured text.</param>
    /// <returns>The visible label, or null when it is empty.</returns>
    internal static string? Text(string? value)
    {
        var text = value?.Trim();
        return text is { Length: > 0 } ? text[..Math.Min(text.Length, 140)] : null;
    }

    private static object MiniGame(MiniGameDefinition game) => new
    {
        tier = game.GameLevel,
        name = Name(game.Name),
        map = game.Entrance?.Map is { IsActive: true } map ? Name(map.Name) : null,
        minimumLevel = game.MinimumCharacterLevel,
        maximumLevel = game.MaximumCharacterLevel,
        specialMinimumLevel = game.MinimumSpecialCharacterLevel,
        specialMaximumLevel = game.MaximumSpecialCharacterLevel,
        game.RequiresMasterClass,
        game.EntranceFee,
        durationSeconds = game.GameDuration.TotalSeconds,
        ticket = game.TicketItem is { IsActive: true } item ? Name(item.Name) : null,
        ticketLevel = game.TicketItem is { IsActive: true } ? (int?)game.TicketItemLevel : null,
        rewards = game.Rewards.Where(reward => reward.RewardType != MiniGameRewardType.Undefined).Take(MaximumDetails).Select(reward => new
        {
            kind = reward.RewardType.ToString(),
            amount = reward.RewardAmount,
            reward.Rank,
            success = Enum.GetValues<MiniGameSuccessFlags>().Where(flag => flag != MiniGameSuccessFlags.None && reward.RequiredSuccess.HasFlag(flag)).Select(flag => flag.ToString()).ToArray(),
            requiredKill = reward.RequiredKill is { } kill ? Name(kill.Designation) : null,
            pool = LimitItemPool(reward.ItemReward?.PossibleItems),
        }).ToArray(),
    };

    private static object Invasion(PeriodicInvasionConfiguration invasion, IReadOnlyList<InvasionSpawnConfiguration>? activeSpawns, IReadOnlyList<GameMapDefinition> maps, GameConfiguration configuration) => new
    {
        singleMap = invasion.ForceSingleMap,
        spawns = (activeSpawns ?? (invasion.ForceSingleMap ? [] : invasion.Mobs.ToArray())).Where(spawn => spawn.Count > 0).Take(MaximumDetails).Select(spawn =>
        {
            var monster = configuration.Monsters.FirstOrDefault(item => item.IsActive && item.Number == spawn.MonsterId);
            return new
            {
                monster = monster is null ? null : Name(monster.Designation),
                spawn.Count,
                everyMap = spawn.IsSpawnOnAllMaps,
                maps = maps.Where(map => spawn.MapIds.Contains((ushort)map.Number)).Take(MaximumDetails).Select(map => Name(map.Name)).ToArray(),
                loot = LimitItemPool(monster?.DropItemGroups.Where(group => group.Chance > 0).SelectMany(group => group.PossibleItems)),
            };
        }).Where(spawn => spawn.monster is not null && spawn.maps.Length > 0).ToArray(),
    };

    private static object Boss(WorldBossDefinition boss, GameConfiguration configuration)
    {
        var tiers = new (string Kind, MissionReward Reward)[]
        {
            ("top1", boss.Top1), ("top2", boss.Top2), ("top3", boss.Top3),
            ("top10", boss.Top10), ("participation", boss.Participation), ("lastHit", boss.LastHit),
        };
        return new
        {
            map = configuration.Maps.FirstOrDefault(map => map.IsActive && map.Number == boss.MapNumber) is { } location ? Name(location.Name) : null,
            boss.ParticipationMinimumDamage,
            participationMinimumPercent = double.IsFinite(boss.ParticipationMinimumPercent) ? (double?)boss.ParticipationMinimumPercent : null,
            boss.TopAlsoGetParticipation,
            rewards = tiers.Where(tier => tier.Reward is { } value && (value.Zen > 0 || value.Coins > 0 || value.Items.Count > 0))
                .Select(tier => new
                {
                    tier.Kind,
                    tier.Reward.Zen,
                    tier.Reward.Coins,
                    coinType = tier.Reward.CoinType.ToString(),
                    items = tier.Reward.Items.Take(MaximumDetails).Select(reward => new
                    {
                        item = configuration.Items.FirstOrDefault(definition => definition.IsActive && definition.Group == reward.Group && definition.Number == reward.Number) is { } configuredItem
                            ? Name(configuredItem.Name) : null,
                        reward.Level,
                        reward.Count,
                    }).Where(reward => reward.item is not null).ToArray(),
                }).ToArray(),
        };
    }

    private static object Quest(QuestDefinition quest, MonsterDefinition npc, IEnumerable<GameMapDefinition> maps) => new
    {
        name = Name(quest.Name),
        quest.Group,
        quest.Number,
        npc = Name(npc.Designation),
        maps = maps.Take(MaximumDetails).Select(map => Name(map.Name)).ToArray(),
        minimumLevel = quest.MinimumCharacterLevel,
        maximumLevel = quest.MaximumCharacterLevel,
        quest.RequiredStartMoney,
        quest.Repeatable,
        qualifiedClass = quest.QualifiedCharacter is { } qualified ? Name(qualified.Name) : null,
        kills = quest.RequiredMonsterKills.Where(requirement => requirement.Monster is { IsActive: true }).Take(MaximumDetails)
            .Select(requirement => new { name = Name(requirement.Monster!.Designation), count = requirement.MinimumNumber }).ToArray(),
        items = quest.RequiredItems.Where(requirement => requirement.Item is { IsActive: true }).Take(MaximumDetails)
            .Select(requirement => new { name = Name(requirement.Item!.Name), count = requirement.MinimumNumber }).ToArray(),
        rewards = quest.Rewards.Where(reward => reward.RewardType != QuestRewardType.Undefined).Take(MaximumDetails).Select(reward => new
        {
            kind = reward.RewardType.ToString(),
            value = reward.RewardType is QuestRewardType.CharacterEvolutionFirstToSecond or QuestRewardType.CharacterEvolutionSecondToThird or QuestRewardType.Skill ? (int?)null : reward.Value,
            name = reward.ItemReward?.Definition is { IsActive: true } item ? Name(item.Name) : reward.SkillReward is { } skill ? Name(skill.Name) : null,
            level = reward.ItemReward?.Level,
            attribute = Text(reward.AttributeReward?.Designation),
        }).ToArray(),
    };

    private static PublicName[] LimitItemPool(IEnumerable<HNMO.DataModel.Configuration.Items.ItemDefinition>? items)
        => items?.Where(item => item.IsActive).Distinct().Take(MaximumDetails).Select(item => Name(item.Name)).ToArray() ?? [];

    private static PublicName Name(LocalizedString value) => new(Text(value.GetTranslation(English)), Text(value.GetTranslation(Vietnamese)));

    private static DateTime? UtcInstant(DateTime? time) => time is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : null;

    private sealed record PublicName(string? En, string? Vi);
}
