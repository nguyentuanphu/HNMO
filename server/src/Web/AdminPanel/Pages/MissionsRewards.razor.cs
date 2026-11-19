// <copyright file="MissionsRewards.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// The page "Missions and rewards": the daily and weekly missions, the streak and every earned-coin source of the
/// <see cref="MissionsPlugIn"/>, in one form. It saves the plug-in's configuration like the plug-in editor does, so the
/// running game servers use it at once.
/// </summary>
public partial class MissionsRewards : ComponentBase
{
    private readonly List<MissionRow> _rows = new();
    private readonly List<string> _errors = new();
    private MissionsConfiguration? _configuration;
    private MissionRewardText _streakReward = new();
    private string _milestoneLevels = string.Empty;
    private string _calendarDays = string.Empty;
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
    private ILogger<MissionsRewards> Logger { get; set; } = null!;

    /// <summary>
    /// Checks the edited configuration and puts the text fields into it.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="missions">The missions with their item texts.</param>
    /// <param name="streakItems">The items text of the streak bonus.</param>
    /// <param name="milestoneLevels">The milestone levels text.</param>
    /// <param name="calendarDays">The calendar days text.</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Apply(MissionsConfiguration configuration, IReadOnlyList<(MissionDefinition Mission, string ItemsText)> missions, string streakItems, string milestoneLevels, string calendarDays)
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (mission, itemsText) in missions)
        {
            mission.Id = mission.Id.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(mission.Id, "^[A-Za-z0-9_-]{1,40}$"))
            {
                errors.Add(string.Format(Resources.MissionsErrorId, mission.Id));
            }
            else if (!ids.Add(mission.Id))
            {
                errors.Add(string.Format(Resources.MissionsErrorDuplicateId, mission.Id));
            }

            if (mission.Target < 1)
            {
                errors.Add(string.Format(Resources.MissionsErrorTarget, mission.Id));
            }

            if (MissionConfigurationText.TryParseItems(itemsText, out var items))
            {
                mission.Reward.Items = items;
            }
            else
            {
                errors.Add(string.Format(Resources.MissionsErrorItems, mission.Id));
            }

            mission.Reward.Zen = Math.Max(0, mission.Reward.Zen);
            mission.Reward.Coins = Math.Max(0, mission.Reward.Coins);
        }

        configuration.Missions = missions.Select(m => m.Mission).ToList();
        if (MissionConfigurationText.TryParseItems(streakItems, out var streak))
        {
            configuration.StreakBonus.Items = streak;
        }
        else
        {
            errors.Add(string.Format(Resources.MissionsErrorItems, Resources.MissionsStreakBonus));
        }

        if (MissionConfigurationText.TryParsePairs(milestoneLevels, 1000, out var levels))
        {
            configuration.Milestones.Levels = levels.Select(l => new LevelCoins { Level = l.Number, Coins = l.Coins }).ToList();
        }
        else
        {
            errors.Add(Resources.MissionsErrorLevels);
        }

        if (MissionConfigurationText.TryParsePairs(calendarDays, 31, out var days))
        {
            configuration.LoginCalendar.Days = days.Select(d => new DayCoins { Day = d.Number, Coins = d.Coins }).ToList();
        }
        else
        {
            errors.Add(Resources.MissionsErrorDays);
        }

        configuration.ResetHour = Math.Clamp(configuration.ResetHour, 0, 23);
        configuration.StreakBonusDay = Math.Clamp(configuration.StreakBonusDay, 2, 60);
        configuration.StreakMissionsRequired = Math.Max(0, configuration.StreakMissionsRequired);
        configuration.EarnedDailyCap = Math.Max(0, configuration.EarnedDailyCap);
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

    private static string KindCaption(MissionKind kind) => kind switch
    {
        MissionKind.KillMonsters => Resources.MissionsKindKill,
        MissionKind.FinishEvent => Resources.MissionsKindFinishEvent,
        MissionKind.WinEvent => Resources.MissionsKindWinEvent,
        MissionKind.ChaosMachineMix => Resources.MissionsKindMix,
        MissionKind.PickUpZen => Resources.MissionsKindZen,
        MissionKind.LevelUp => Resources.MissionsKindLevel,
        _ => Resources.MissionsKindMinutes,
    };

    private static string EventCaption(MissionEventType type) => type switch
    {
        MissionEventType.BloodCastle => Resources.MissionsEventBloodCastle,
        MissionEventType.DevilSquare => Resources.MissionsEventDevilSquare,
        MissionEventType.ChaosCastle => Resources.MissionsEventChaosCastle,
        _ => Resources.MissionsEventAny,
    };

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            // Straight from the database: the panel's cached game configuration can miss the row the game
            // server added when it first started.
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<MissionsConfiguration>(this.PersistenceContextProvider, typeof(MissionsPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = saved ?? new MissionsConfiguration();

            // A copy, so that an unsaved edit doesn't change the cached configuration.
            configuration = JsonSerializer.Deserialize<MissionsConfiguration>(JsonSerializer.Serialize(configuration)) ?? new MissionsConfiguration();
            this._configuration = configuration;
            this._rows.Clear();
            this._rows.AddRange(configuration.Missions.Select(m => new MissionRow(m)));
            this._streakReward = new MissionRewardText { ItemsText = MissionConfigurationText.FormatItems(configuration.StreakBonus.Items) };
            this._milestoneLevels = MissionConfigurationText.FormatPairs(configuration.Milestones.Levels.Select(l => (l.Level, l.Coins)));
            this._calendarDays = MissionConfigurationText.FormatPairs(configuration.LoginCalendar.Days.Select(d => (d.Day, d.Coins)));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the missions configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void AddMission()
    {
        var number = this._rows.Count + 1;
        while (this._rows.Any(r => r.Mission.Id == $"mission-{number}"))
        {
            number++;
        }

        this._rows.Add(new MissionRow(new MissionDefinition { Id = $"mission-{number}", Target = 10, TitleEn = "New mission", TitleVi = "Nhiệm vụ mới" }));
    }

    private void RemoveMission(MissionRow row) => this._rows.Remove(row);

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        this._errors.AddRange(Apply(configuration, this._rows.Select(r => (r.Mission, r.Reward.ItemsText)).ToList(), this._streakReward.ItemsText, this._milestoneLevels, this._calendarDays));
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
            var id = typeof(MissionsPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<MissionsConfiguration>(this.PersistenceContextProvider, typeof(MissionsPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the missions configuration: {Count} missions, active: {Active}.", configuration.Missions.Count, this._isActive);
            this.ToastService.ShowSuccess(Resources.MissionsSaved);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the missions configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    /// <summary>
    /// A row of the missions table.
    /// </summary>
    private sealed class MissionRow
    {
        public MissionRow(MissionDefinition mission)
        {
            this.Mission = mission;
            this.Reward = new MissionRewardText { ItemsText = MissionConfigurationText.FormatItems(mission.Reward.Items) };
        }

        public MissionDefinition Mission { get; }

        public MissionRewardText Reward { get; }
    }
}

/// <summary>
/// The text form of a reward's items, as the page edits it.
/// </summary>
public sealed class MissionRewardText
{
    /// <summary>Gets or sets the items as "group number level count; ...".</summary>
    public string ItemsText { get; set; } = string.Empty;
}
