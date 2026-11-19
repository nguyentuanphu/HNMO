// <copyright file="OnboardingStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Writes the guide as JSON: the state of a character for the game client (HnmoOnboardingState) and the public steps and
/// starter kit for the website. Non-ASCII text (the Vietnamese texts) is escaped, so the JSON is plain ASCII.
/// </summary>
public static class OnboardingStateWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes the state of a character.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="characterId">The character id.</param>
    /// <param name="facts">What's known of the character (for the counts of the steps checked by looking).</param>
    /// <param name="target">Where the current step leads, if anywhere.</param>
    /// <param name="kitNew">Whether the starter kit was just given (the client tells it once).</param>
    /// <param name="classNumber">The class of the character (for the kit's class items).</param>
    /// <param name="boardEvent">What happened, if anything.</param>
    /// <returns>The JSON.</returns>
    public static string WriteState(OnboardingEngine engine, string characterId, OnboardingFacts? facts, OnboardingTarget? target, bool kitNew, int classNumber, OnboardingBoardEvent? boardEvent)
    {
        var configuration = engine.Configuration;
        var status = engine.StatusOf(characterId);
        var (completed, skipped) = engine.StepsOf(characterId);
        var progress = engine.Progress(characterId, facts);
        var steps = configuration.EnabledSteps().ToList();
        var state = new
        {
            V = 1,
            Status = !configuration.GuideEnabled && status == OnboardingStatus.Active ? "off" : StatusName(status),
            Steps = steps.Select(s => new
            {
                s.Id,
                s.Kind,
                s.TitleEn,
                s.TitleVi,
                s.HintEn,
                s.HintVi,
                Target = Math.Max(1, s.Target),
                s.Optional,
                Done = completed.Contains(s.Id),
                Skipped = skipped.Contains(s.Id),
                Reward = Reward(s.Reward),
            }).ToList(),
            Current = progress is { } p ? new { p.Step.Id, p.Value, p.Target, Where = target } : null,
            Done = steps.Count(s => completed.Contains(s.Id)),
            Total = steps.Count,
            Final = Reward(configuration.FinalReward),
            Owed = engine.Owed(characterId).Select(o => new
            {
                o.Kind,
                o.TitleEn,
                o.TitleVi,
                Zen = o.Zen,
                Coins = o.CoinsGiven ? 0 : o.Coins,
                o.CoinType,
                Items = o.Items.Select(Item).ToList(),
                o.BuffMinutes,
            }).ToList(),
            Kit = kitNew ? Kit(configuration.StarterKit, classNumber, null) : null,
            Event = boardEvent,
        };
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Writes the public steps and starter kit (no player data), for the website.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="itemName">Gives the name of an item, if known.</param>
    /// <param name="classes">The classes a new character can have (number and name).</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(OnboardingConfiguration configuration, Func<short, short, string?>? itemName = null, IReadOnlyList<(int Number, string Name)>? classes = null)
    {
        var value = new
        {
            V = 1,
            Enabled = true,
            configuration.GuideEnabled,
            configuration.NewCharacterMaxLevel,
            Steps = configuration.GuideEnabled
                ? configuration.EnabledSteps().Select(s => new { s.Id, s.Kind, s.TitleEn, s.TitleVi, s.HintEn, s.HintVi, Target = Math.Max(1, s.Target), s.Optional, Reward = Reward(s.Reward, itemName) }).ToList()
                : null,
            Final = configuration.GuideEnabled ? Reward(configuration.FinalReward, itemName) : null,
            Kit = configuration.StarterKit.Enabled ? Kit(configuration.StarterKit, null, itemName, classes) : null,
        };
        return JsonSerializer.Serialize(value, Options);
    }

    private static string StatusName(OnboardingStatus status) => status switch
    {
        OnboardingStatus.Active => "active",
        OnboardingStatus.Done => "done",
        OnboardingStatus.Skipped => "skipped",
        _ => "none",
    };

    private static object Kit(StarterKitConfiguration kit, int? classNumber, Func<short, short, string?>? itemName, IReadOnlyList<(int Number, string Name)>? classes = null) => new
    {
        kit.PerAccount,
        Zen = kit.ZenEnabled ? kit.Zen : 0,
        Coins = kit.CoinsEnabled ? kit.Coins : 0,
        kit.CoinType,
        Points = kit.PointsEnabled ? kit.LevelUpPoints : 0,
        BuffMinutes = kit.ExperienceBuff.Enabled ? kit.ExperienceBuff.Minutes : 0,
        BuffName = kit.ExperienceBuff.Enabled ? itemName?.Invoke(kit.ExperienceBuff.ItemGroup, kit.ExperienceBuff.ItemNumber) : null,
        Items = !kit.ItemsEnabled
            ? []
            : (classNumber is { } number ? OnboardingEngine.KitItemsOf(kit, number) : kit.Items.Where(i => i.Count > 0).ToList())
                .Select(i => new
                {
                    i.Group,
                    i.Number,
                    i.Level,
                    i.Count,
                    i.Durability,
                    i.Skill,
                    i.Luck,
                    i.OptionLevel,
                    Excellent = i.ExcellentOptions,
                    i.ClassNumber,
                    ClassName = i.ClassNumber >= 0 ? classes?.FirstOrDefault(c => c.Number == i.ClassNumber).Name : null,
                    Name = itemName?.Invoke(i.Group, i.Number),
                }).ToList<object>(),
    };

    private static object Item(StarterKitItem i) => new { i.Group, i.Number, i.Level, i.Count, i.Durability, i.Skill, i.Luck, i.OptionLevel, Excellent = i.ExcellentOptions };

    private static object Reward(MissionReward reward, Func<short, short, string?>? itemName = null) => new
    {
        reward.Zen,
        reward.Coins,
        reward.CoinType,
        Items = reward.Items.Where(i => i.Count > 0).Select(i => new { i.Group, i.Number, i.Level, i.Count, Name = itemName?.Invoke(i.Group, i.Number) }).ToList(),
    };
}
