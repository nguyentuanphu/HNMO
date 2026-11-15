// <copyright file="GuildsLevelsLadder.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// The page "Guilds: levels and ladder": the guild experience sources, the daily cap, the levels with their buffs and
/// the weekly ladder rewards of the <see cref="GuildsPlugIn"/>, in one form, with this week's ladder, last week's winners
/// and the castle siege results. It saves the plug-in's configuration like the plug-in editor does, so the running game
/// servers use it at once.
/// </summary>
public partial class GuildsLevelsLadder : ComponentBase
{
    private readonly List<GuildLevelDefinition> _levels = new();
    private readonly List<RewardRow> _rewards = new();
    private readonly List<string> _errors = new();
    private GuildsConfiguration? _configuration;
    private GuildEngine? _engine;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<GuildsLevelsLadder> Logger { get; set; } = null!;

    /// <summary>
    /// Checks the edited configuration and puts the edited lists into it.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="levels">The levels.</param>
    /// <param name="rewards">The ladder rewards with their item texts.</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Apply(GuildsConfiguration configuration, IReadOnlyList<GuildLevelDefinition> levels, IReadOnlyList<(GuildLadderReward Reward, string ItemsText)> rewards)
    {
        var errors = new List<string>();
        var ordered = levels.OrderBy(l => l.Level).ToList();
        var seen = new HashSet<int>();
        foreach (var level in ordered)
        {
            if (!seen.Add(level.Level))
            {
                errors.Add(string.Format(Resources.GuildsErrorDuplicateLevel, level.Level));
            }

            level.Xp = Math.Max(0, level.Xp);
            level.ExperiencePercent = Math.Clamp(level.ExperiencePercent, 0, 1000);
            level.ZenPercent = Math.Clamp(level.ZenPercent, 0, 1000);
            level.Defense = Math.Clamp(level.Defense, 0, 100_000);
        }

        var valid = ordered.Count > 0 && ordered[0].Level == 1 && ordered[0].Xp == 0
            && ordered.Zip(ordered.Skip(1)).All(p => p.Second.Level > p.First.Level && p.Second.Xp > p.First.Xp);
        if (!valid)
        {
            errors.Add(Resources.GuildsErrorLevels);
        }

        configuration.Levels = ordered;
        var taken = new HashSet<int>();
        foreach (var (reward, itemsText) in rewards)
        {
            reward.RankTo = Math.Max(reward.RankTo, reward.RankFrom);
            if (reward.RankFrom < 1 || reward.RankTo > 100 || Enumerable.Range(reward.RankFrom, Math.Max(0, Math.Min(reward.RankTo, 100) - reward.RankFrom + 1)).Any(r => !taken.Add(r)))
            {
                errors.Add(string.Format(Resources.GuildsErrorRanks, reward.RankFrom, reward.RankTo));
            }

            if (MissionConfigurationText.TryParseItems(itemsText, out var items))
            {
                reward.Items = items;
            }
            else
            {
                errors.Add(string.Format(Resources.MissionsErrorItems, $"{reward.RankFrom}-{reward.RankTo}"));
            }

            reward.CoinsPerMember = Math.Max(0, reward.CoinsPerMember);
            reward.MasterBonusCoins = Math.Max(0, reward.MasterBonusCoins);
            reward.ZenPerMember = Math.Max(0, reward.ZenPerMember);
        }

        configuration.LadderRewards = rewards.Select(r => r.Reward).OrderBy(r => r.RankFrom).ToList();
        configuration.DailyCapPerMember = Math.Max(0, configuration.DailyCapPerMember);
        configuration.OnlineMinutes = Math.Clamp(configuration.OnlineMinutes, 1, 1440);
        configuration.CastleSiegeLadderBonus = Math.Max(0, configuration.CastleSiegeLadderBonus);
        configuration.LadderMinimumXp = Math.Max(0, configuration.LadderMinimumXp);
        foreach (var source in Enum.GetValues<GuildXpSource>())
        {
            var setting = configuration.Source(source);
            setting.Xp = Math.Max(0, setting.Xp);
        }

        return errors;
    }

    /// <summary>Gets the caption of a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The caption.</returns>
    internal static string SourceCaption(GuildXpSource source) => source switch
    {
        GuildXpSource.MonsterKill => Resources.GuildsSourceMonsterKill,
        GuildXpSource.EventFinish => Resources.GuildsSourceEventFinish,
        GuildXpSource.EventWin => Resources.GuildsSourceEventWin,
        GuildXpSource.WorldBossKill => Resources.GuildsSourceWorldBossKill,
        GuildXpSource.WorldBossDamage => Resources.GuildsSourceWorldBossDamage,
        GuildXpSource.Mission => Resources.GuildsSourceMission,
        GuildXpSource.OnlineTime => Resources.GuildsSourceOnlineTime,
        _ => Resources.GuildsSourceCastleSiege,
    };

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private static string Sydney(DateTime utc, GuildEngine engine) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), engine.Clock.TimeZone).ToString("ddd d MMM yyyy HH:mm", System.Globalization.CultureInfo.CurrentUICulture);

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            // Straight from the database: the panel's cached game configuration can miss the row the game
            // server added when it first started.
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<GuildsConfiguration>(this.PersistenceContextProvider, typeof(GuildsPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = saved ?? new GuildsConfiguration();

            // A copy, so that an unsaved edit doesn't change a cached configuration.
            configuration = JsonSerializer.Deserialize<GuildsConfiguration>(JsonSerializer.Serialize(configuration)) ?? new GuildsConfiguration();
            this._configuration = configuration;
            this._levels.Clear();
            this._levels.AddRange(configuration.Levels.OrderBy(l => l.Level));
            this._rewards.Clear();
            this._rewards.AddRange(configuration.LadderRewards.OrderBy(r => r.RankFrom).Select(r => new RewardRow(r)));
            this._engine = new GuildEngine(configuration, GuildStore.Open(configuration.FileName));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the guilds configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void AddLevel()
    {
        var last = this._levels.OrderBy(l => l.Level).LastOrDefault();
        this._levels.Add(new GuildLevelDefinition
        {
            Level = (last?.Level ?? 0) + 1,
            Xp = last is null ? 0 : Math.Max(1, last.Xp * 3 / 2),
            ExperiencePercent = last?.ExperiencePercent ?? 0,
            ZenPercent = last?.ZenPercent ?? 0,
            Defense = last?.Defense ?? 0,
        });
    }

    private void RemoveLevel(GuildLevelDefinition level) => this._levels.Remove(level);

    private void AddReward()
    {
        var next = this._rewards.Count == 0 ? 1 : this._rewards.Max(r => Math.Max(r.Reward.RankFrom, r.Reward.RankTo)) + 1;
        this._rewards.Add(new RewardRow(new GuildLadderReward { RankFrom = next, RankTo = next, CoinsPerMember = 10 }));
    }

    private void RemoveReward(RewardRow row) => this._rewards.Remove(row);

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        this._errors.AddRange(Apply(configuration, this._levels, this._rewards.Select(r => (r.Reward, r.Items.ItemsText)).ToList()));
        if (this._errors.Count > 0)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(GuildsPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<GuildsConfiguration>(this.PersistenceContextProvider, typeof(GuildsPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the guilds configuration: {Levels} levels, {Rewards} ladder rewards, active: {Active}.", configuration.Levels.Count, configuration.LadderRewards.Count, this._isActive);
            this.ToastService.ShowSuccess(Resources.GuildsSaved);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the guilds configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    /// <summary>
    /// A row of the ladder rewards table.
    /// </summary>
    private sealed class RewardRow
    {
        public RewardRow(GuildLadderReward reward)
        {
            this.Reward = reward;
            this.Items = new MissionRewardText { ItemsText = MissionConfigurationText.FormatItems(reward.Items) };
        }

        public GuildLadderReward Reward { get; }

        public MissionRewardText Items { get; }
    }
}
