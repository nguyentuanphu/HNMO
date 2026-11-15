// <copyright file="WorldBossesRankings.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// The page "World bosses and rankings": the world bosses, their schedules, announcements and rewards, and the weekly
/// ranking coins of the <see cref="WorldBossesPlugIn"/>, in one form. It saves the plug-in's configuration like the
/// plug-in editor does, so the running game servers use it at once. It also spawns a boss now and lists the recent kills.
/// </summary>
public partial class WorldBossesRankings : ComponentBase
{
    /// <summary>The tiers of a boss's rewards, in the order of the page.</summary>
    internal static readonly string[] Tiers = ["top1", "top2", "top3", "top10", "participation", "lasthit"];

    /// <summary>The weekly rankings with coins by rank.</summary>
    internal static readonly WeeklyCategory[] RankedCategories = [WeeklyCategory.BossHunt, WeeklyCategory.BloodCastle, WeeklyCategory.DevilSquare, WeeklyCategory.ChaosCastle];

    private static readonly DayOfWeek[] Week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    private readonly List<BossRow> _rows = new();
    private readonly Dictionary<WeeklyCategory, string> _ranks = new();
    private readonly List<string> _errors = new();
    private List<WorldBossKillRecord> _kills = new();
    private List<WorldBossAliveState> _alive = new();
    private WorldBossesConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Inject]
    private ILogger<WorldBossesRankings> Logger { get; set; } = null!;

    /// <summary>
    /// Checks the edited configuration and puts the text fields into it.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bosses">The bosses with the item texts of their reward tiers (in the order of <see cref="Tiers"/>).</param>
    /// <param name="ranks">The coins by rank of the rankings as text ("1:200, 2:120").</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Apply(WorldBossesConfiguration configuration, IReadOnlyList<(WorldBossDefinition Boss, IReadOnlyList<string> ItemTexts)> bosses, IReadOnlyDictionary<WeeklyCategory, string> ranks)
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (boss, itemTexts) in bosses)
        {
            boss.Id = boss.Id.Trim();
            if (!Regex.IsMatch(boss.Id, "^[A-Za-z0-9_-]{1,40}$"))
            {
                errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesErrorId, boss.Id));
            }
            else if (!ids.Add(boss.Id))
            {
                errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesErrorDuplicateId, boss.Id));
            }

            var timesText = boss.ScheduleKind == WorldBossScheduleKind.FixedTimes ? boss.FixedTimes : boss.StartTime;
            if (!WorldBossSchedule.TryParseTimes(timesText, out var times) || times.Count == 0 || (boss.ScheduleKind == WorldBossScheduleKind.EveryHours && times.Count != 1))
            {
                errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesErrorTimes, boss.Id));
            }
            else if (boss.ScheduleKind == WorldBossScheduleKind.FixedTimes)
            {
                boss.FixedTimes = WorldBossSchedule.FormatTimes(times);
            }
            else
            {
                boss.StartTime = WorldBossSchedule.FormatTimes(times);
            }

            if (!SpotsValid(boss.SpawnSpots))
            {
                errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesErrorSpots, boss.Id));
            }

            for (var i = 0; i < Tiers.Length; i++)
            {
                var reward = WorldBossEngine.RewardOf(boss, Tiers[i]);
                if (MissionConfigurationText.TryParseItems(i < itemTexts.Count ? itemTexts[i] : string.Empty, out var items))
                {
                    reward.Items = items;
                }
                else
                {
                    errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesErrorItems, boss.Id));
                }

                reward.Zen = Math.Max(0, reward.Zen);
                reward.Coins = Math.Max(0, reward.Coins);
            }

            boss.EveryHours = Math.Clamp(boss.EveryHours, 1, 24);
            boss.DespawnMinutes = Math.Clamp(boss.DespawnMinutes, 1, 1440);
            boss.AnnounceMinutesBefore = Math.Clamp(boss.AnnounceMinutesBefore, 0, 240);
            boss.GameServerId = Math.Clamp(boss.GameServerId, -1, 255);
            boss.HuntPoints = Math.Max(0, boss.HuntPoints);
            boss.ParticipationMinimumPercent = Math.Clamp(boss.ParticipationMinimumPercent, 0, 100);
            boss.ParticipationMinimumDamage = Math.Max(0, boss.ParticipationMinimumDamage);
        }

        configuration.Bosses = bosses.Select(b => b.Boss).ToList();
        foreach (var category in RankedCategories)
        {
            var ranking = configuration.Ranking(category)!;
            if (MissionConfigurationText.TryParsePairs(ranks.TryGetValue(category, out var text) ? text : string.Empty, WorldBossEngine.PaidRanks, out var pairs))
            {
                ranking.Ranks = pairs.Select(p => new RankCoins { Rank = p.Number, Coins = p.Coins }).ToList();
            }
            else
            {
                errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesErrorRanks, CategoryCaption(category)));
            }

            ranking.PointsFinish = Math.Max(0, ranking.PointsFinish);
            ranking.PointsWin = Math.Max(0, ranking.PointsWin);
        }

        configuration.CastleSiege.CoinsPerMember = Math.Max(0, configuration.CastleSiege.CoinsPerMember);
        configuration.CastleSiege.GuildMasterBonus = Math.Max(0, configuration.CastleSiege.GuildMasterBonus);
        if (string.IsNullOrWhiteSpace(configuration.TimeZone))
        {
            configuration.TimeZone = "Australia/Sydney";
        }

        return errors;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private static string CategoryCaption(WeeklyCategory category) => category switch
    {
        WeeklyCategory.BossHunt => Resources.WorldBossesBossHunt,
        WeeklyCategory.BloodCastle => Resources.WorldBossesBloodCastle,
        WeeklyCategory.DevilSquare => Resources.WorldBossesDevilSquare,
        WeeklyCategory.ChaosCastle => Resources.WorldBossesChaosCastle,
        _ => Resources.WorldBossesCastleSiege,
    };

    private static string TierCaption(string tier) => tier switch
    {
        "top1" => Resources.WorldBossesTop1,
        "top2" => Resources.WorldBossesTop2,
        "top3" => Resources.WorldBossesTop3,
        "top10" => Resources.WorldBossesTop10,
        "participation" => Resources.WorldBossesParticipation,
        _ => Resources.WorldBossesLastHit,
    };

    private static string DayCaption(DayOfWeek day) => CultureInfo.CurrentUICulture.DateTimeFormat.GetAbbreviatedDayName(day);

    private static bool SpotsValid(string? text)
    {
        foreach (var part in (text ?? string.Empty).Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var xy = part.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length != 2 || !byte.TryParse(xy[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !byte.TryParse(xy[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        return true;
    }

    private string SydneyTime(DateTime utc)
    {
        var zone = new MissionClock(this._configuration?.TimeZone, 0).TimeZone;
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("ddd yyyy-MM-dd HH:mm", CultureInfo.CurrentUICulture);
    }

    private string NextSpawnText(WorldBossDefinition boss)
    {
        if (this._alive.FirstOrDefault(a => a.BossId == boss.Id) is { } alive)
        {
            return string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesAliveNow, this.SydneyTime(alive.DespawnAtUtc));
        }

        var zone = new MissionClock(this._configuration?.TimeZone, 0).TimeZone;
        return WorldBossSchedule.Next(boss, zone, DateTime.UtcNow) is { } next ? this.SydneyTime(next) : "-";
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            // Straight from the database: the panel's cached game configuration can miss the row the game
            // server added when it first started.
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<WorldBossesConfiguration>(this.PersistenceContextProvider, typeof(WorldBossesPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = saved ?? new WorldBossesConfiguration();

            // A copy, so that an unsaved edit doesn't change the cached configuration.
            configuration = JsonSerializer.Deserialize<WorldBossesConfiguration>(JsonSerializer.Serialize(configuration)) ?? new WorldBossesConfiguration();
            this._configuration = configuration;
            this._rows.Clear();
            this._rows.AddRange(configuration.Bosses.Select(b => new BossRow(b)));
            this._ranks.Clear();
            foreach (var category in RankedCategories)
            {
                this._ranks[category] = MissionConfigurationText.FormatPairs(configuration.Ranking(category)!.Ranks.Select(r => (r.Rank, r.Coins)));
            }

            var store = WorldBossStore.Open(configuration.FileName);
            lock (store.Lock)
            {
                this._kills = store.Data.Kills.Take(10).ToList();
                this._alive = store.Data.Alive.ToList();
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the world bosses configuration.");
            this.ToastService.ShowError(string.Format(CultureInfo.CurrentCulture, Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void AddBoss()
    {
        var number = this._rows.Count + 1;
        while (this._rows.Any(r => r.Boss.Id == $"boss-{number}"))
        {
            number++;
        }

        this._rows.Add(new BossRow(new WorldBossDefinition { Id = $"boss-{number}", NameEn = "New boss", NameVi = "Boss mới", ScheduleKind = WorldBossScheduleKind.FixedTimes, FixedTimes = "20:00" }));
    }

    private void RemoveBoss(BossRow row) => this._rows.Remove(row);

    private void ToggleDay(WorldBossDefinition boss, DayOfWeek day, bool on)
    {
        var days = boss.Days.ToHashSet();
        if (on)
        {
            days.Add(day);
        }
        else
        {
            days.Remove(day);
        }

        boss.Days = Week.Where(days.Contains).ToList();
    }

    private async Task SpawnNowAsync(WorldBossDefinition boss)
    {
        try
        {
            var contexts = (this.ServiceProvider.GetService<IList<IManageableServer>>() ?? [])
                .OfType<IGameServerContextProvider>()
                .Select(s => (IGameContext)s.Context)
                .ToList();
            var result = await WorldBossesPlugIn.SpawnNowAsync(contexts, boss.Id).ConfigureAwait(true);
            var name = string.IsNullOrWhiteSpace(boss.NameEn) ? boss.Id : boss.NameEn;
            switch (result)
            {
                case "Ok":
                    this.ToastService.ShowSuccess(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesSpawned, name));
                    break;
                case "Alive":
                    this.ToastService.ShowError(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesSpawnAlive, name));
                    break;
                case "Failed":
                    this.ToastService.ShowError(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesSpawnFailed, name));
                    break;
                default:
                    this.ToastService.ShowError(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesSpawnNoServer, name));
                    break;
            }

            this.Logger.LogInformation("Spawn now of the world boss {Boss}: {Result}.", boss.Id, result);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error spawning the world boss {Boss}.", boss.Id);
            this.ToastService.ShowError(string.Format(CultureInfo.CurrentCulture, Resources.UnexpectedErrorOccurred, ex.Message));
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        this._errors.AddRange(Apply(configuration, this._rows.Select(r => (r.Boss, (IReadOnlyList<string>)r.Rewards.Select(t => t.ItemsText).ToList())).ToList(), this._ranks));
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
            var id = typeof(WorldBossesPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<WorldBossesConfiguration>(this.PersistenceContextProvider, typeof(WorldBossesPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the world bosses configuration: {Count} bosses, active: {Active}.", configuration.Bosses.Count, this._isActive);
            this.ToastService.ShowSuccess(Resources.WorldBossesSaved);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the world bosses configuration.");
            this.ToastService.ShowError(string.Format(CultureInfo.CurrentCulture, Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    /// <summary>
    /// A boss of the page, with the item texts of its reward tiers.
    /// </summary>
    private sealed class BossRow
    {
        public BossRow(WorldBossDefinition boss)
        {
            this.Boss = boss;
            this.Rewards = Tiers.Select(t => new MissionRewardText { ItemsText = MissionConfigurationText.FormatItems(WorldBossEngine.RewardOf(boss, t).Items) }).ToList();
        }

        public WorldBossDefinition Boss { get; }

        public List<MissionRewardText> Rewards { get; }
    }
}
