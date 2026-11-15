// <copyright file="TopRaces.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// The page "Top races": the races of the <see cref="TopRacesPlugIn"/> with every field (kind, classes, period, dates,
/// minimum, prizes per rank), the notices, the live standings and the past winners. It saves the plug-in's configuration
/// like the plug-in editor does, so the running game servers use it at once.
/// </summary>
public partial class TopRaces : ComponentBase
{
    private static readonly Regex IdPattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);

    private readonly List<RaceRow> _races = new();
    private readonly List<string> _errors = new();
    private TopRacesConfiguration? _configuration;
    private TopRaceEngine? _engine;
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
    private ILogger<TopRaces> Logger { get; set; } = null!;

    private bool IsVietnamese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    /// <summary>
    /// Checks the edited races and puts them into the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="races">The races with their prizes and the prizes' item texts.</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Apply(TopRacesConfiguration configuration, IReadOnlyList<(TopRaceDefinition Race, IReadOnlyList<(TopRacePrize Prize, string ItemsText)> Prizes)> races)
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var zone = new MissionClock(configuration.TimeZone, 0).TimeZone;
        foreach (var (race, prizes) in races)
        {
            race.Id = race.Id?.Trim() ?? string.Empty;
            race.TitleEn = race.TitleEn?.Trim() ?? string.Empty;
            race.TitleVi = race.TitleVi?.Trim() ?? string.Empty;
            var name = race.Id.Length > 0 ? race.Id : "?";
            if (!IdPattern.IsMatch(race.Id))
            {
                errors.Add(string.Format(Resources.TopRacesErrorId, name));
            }
            else if (!ids.Add(race.Id))
            {
                errors.Add(string.Format(Resources.TopRacesErrorDuplicateId, race.Id));
            }

            if (race.TitleEn.Length == 0)
            {
                errors.Add(string.Format(Resources.TopRacesErrorTitle, name));
            }

            if (race.Period == TopRacePeriod.Custom)
            {
                var start = TopRaceEngine.ParseLocal(race.Start);
                var end = TopRaceEngine.ParseLocal(race.End);
                if (start is null || end is null || TopRaceEngine.LocalToUtc(end.Value, zone) <= TopRaceEngine.LocalToUtc(start.Value, zone))
                {
                    errors.Add(string.Format(Resources.TopRacesErrorDates, name));
                }
                else
                {
                    race.Start = start.Value.ToString(TopRaceEngine.DateFormat, CultureInfo.InvariantCulture);
                    race.End = end.Value.ToString(TopRaceEngine.DateFormat, CultureInfo.InvariantCulture);
                }
            }

            race.Minimum = Math.Max(1, race.Minimum);
            var taken = new HashSet<int>();
            foreach (var (prize, itemsText) in prizes)
            {
                prize.RankTo = Math.Max(prize.RankTo, prize.RankFrom);
                if (prize.RankFrom < 1 || prize.RankTo > 100 || Enumerable.Range(prize.RankFrom, Math.Max(0, Math.Min(prize.RankTo, 100) - prize.RankFrom + 1)).Any(r => !taken.Add(r)))
                {
                    errors.Add(string.Format(Resources.TopRacesErrorRanks, name, prize.RankFrom, prize.RankTo));
                }

                if (MissionConfigurationText.TryParseItems(itemsText, out var items))
                {
                    prize.Items = items;
                }
                else
                {
                    errors.Add(string.Format(Resources.MissionsErrorItems, $"{name} {prize.RankFrom}-{prize.RankTo}"));
                }

                prize.Coins = Math.Max(0, prize.Coins);
                prize.Zen = Math.Max(0, prize.Zen);
            }

            race.Prizes = prizes.Select(p => p.Prize).OrderBy(p => p.RankFrom).ToList();
        }

        configuration.Races = races.Select(r => r.Race).ToList();
        configuration.HistoryKept = Math.Clamp(configuration.HistoryKept, 1, 500);
        return errors;
    }

    /// <summary>Gets the caption of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The caption.</returns>
    internal static string KindCaption(TopRaceKind kind) => kind switch
    {
        TopRaceKind.ResetsGained => Resources.TopRacesKindResetsGained,
        TopRaceKind.LevelsGained => Resources.TopRacesKindLevelsGained,
        TopRaceKind.LevelReached => Resources.TopRacesKindLevelReached,
        TopRaceKind.BossHuntPoints => Resources.TopRacesKindBossHuntPoints,
        TopRaceKind.PkKills => Resources.TopRacesKindPkKills,
        TopRaceKind.EventWins => Resources.TopRacesKindEventWins,
        TopRaceKind.GuildPoints => Resources.TopRacesKindGuildPoints,
        TopRaceKind.MonsterKills => Resources.TopRacesKindMonsterKills,
        _ => Resources.TopRacesKindOnlineMinutes,
    };

    /// <summary>Gets the caption of an event.</summary>
    /// <param name="value">The event.</param>
    /// <returns>The caption.</returns>
    internal static string EventCaption(TopRaceEvent value) => value switch
    {
        TopRaceEvent.BloodCastle => Resources.TopRacesEventBloodCastle,
        TopRaceEvent.DevilSquare => Resources.TopRacesEventDevilSquare,
        TopRaceEvent.ChaosCastle => Resources.TopRacesEventChaosCastle,
        _ => Resources.TopRacesEventAny,
    };

    /// <summary>Gets the caption of a scope.</summary>
    /// <param name="value">The scope.</param>
    /// <returns>The caption.</returns>
    internal static string ScopeCaption(TopRaceScope value) => value switch
    {
        TopRaceScope.OneClass => Resources.TopRacesScopeOneClass,
        TopRaceScope.PerClass => Resources.TopRacesScopePerClass,
        _ => Resources.TopRacesScopeAll,
    };

    /// <summary>Gets the caption of a period.</summary>
    /// <param name="value">The period.</param>
    /// <returns>The caption.</returns>
    internal static string PeriodCaption(TopRacePeriod value) => value switch
    {
        TopRacePeriod.Daily => Resources.TopRacesPeriodDaily,
        TopRacePeriod.Weekly => Resources.TopRacesPeriodWeekly,
        TopRacePeriod.Monthly => Resources.TopRacesPeriodMonthly,
        _ => Resources.TopRacesPeriodCustom,
    };

    /// <summary>Gets the caption of a class.</summary>
    /// <param name="value">The class.</param>
    /// <returns>The caption.</returns>
    internal static string ClassCaption(TopRaceClass value) => value switch
    {
        TopRaceClass.DarkWizard => Resources.TopRacesClassDarkWizard,
        TopRaceClass.DarkKnight => Resources.TopRacesClassDarkKnight,
        TopRaceClass.FairyElf => Resources.TopRacesClassFairyElf,
        TopRaceClass.MagicGladiator => Resources.TopRacesClassMagicGladiator,
        TopRaceClass.DarkLord => Resources.TopRacesClassDarkLord,
        TopRaceClass.Summoner => Resources.TopRacesClassSummoner,
        _ => Resources.TopRacesClassRageFighter,
    };

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private static string Sydney(DateTime utc, TopRaceEngine engine) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), engine.Clock.TimeZone).ToString("ddd d MMM yyyy HH:mm", CultureInfo.CurrentUICulture);

    private static string PrizeText(TopRaceLine line)
    {
        if (!line.Prized)
        {
            return "-";
        }

        var parts = new List<string>();
        if (line.Coins > 0)
        {
            parts.Add($"{line.Coins:N0} {CashShopCoinCaption.Get(line.CoinType)}");
        }

        if (line.Zen > 0)
        {
            parts.Add($"{line.Zen:N0} zen");
        }

        if (line.Items.Count > 0)
        {
            parts.Add(MissionConfigurationText.FormatItems(line.Items));
        }

        return string.Join(", ", parts);
    }

    private static void AddPrize(RaceRow row)
    {
        var next = row.Prizes.Count == 0 ? 1 : row.Prizes.Max(p => Math.Max(p.Prize.RankFrom, p.Prize.RankTo)) + 1;
        row.Prizes.Add(new PrizeRow(new TopRacePrize { RankFrom = next, RankTo = next, Coins = 10 }));
    }

    private string Title(string en, string vi) => this.IsVietnamese && !string.IsNullOrWhiteSpace(vi) ? vi : en;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            // Straight from the database: the panel's cached game configuration can miss the row the game
            // server added when it first started.
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<TopRacesConfiguration>(this.PersistenceContextProvider, typeof(TopRacesPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = saved ?? new TopRacesConfiguration();

            // A copy, so that an unsaved edit doesn't change a cached configuration.
            configuration = JsonSerializer.Deserialize<TopRacesConfiguration>(JsonSerializer.Serialize(configuration)) ?? new TopRacesConfiguration();
            this._configuration = configuration;
            this._races.Clear();
            this._races.AddRange(configuration.Races.Select(r => new RaceRow(r)));

            // The live standings use their own copy: an unsaved (or invalid) edit must not end a running race.
            var live = JsonSerializer.Deserialize<TopRacesConfiguration>(JsonSerializer.Serialize(configuration)) ?? new TopRacesConfiguration();
            this._engine = new TopRaceEngine(live, TopRaceStore.Open(live.FileName));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the top races configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void AddRace()
    {
        var number = this._races.Count + 1;
        while (this._races.Any(r => r.Race.Id == $"race-{number}"))
        {
            number++;
        }

        var race = new TopRaceDefinition
        {
            Id = $"race-{number}",
            TitleEn = $"Top race {number}",
            TitleVi = $"Đua top {number}",
            Kind = TopRaceKind.ResetsGained,
            Period = TopRacePeriod.Weekly,
            Prizes = [new TopRacePrize { RankFrom = 1, RankTo = 1, Coins = 100 }],
        };
        this._races.Add(new RaceRow(race));
    }

    private void RemoveRace(RaceRow row) => this._races.Remove(row);

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        var races = this._races.Select(r => (r.Race, (IReadOnlyList<(TopRacePrize, string)>)r.Prizes.Select(p => (p.Prize, p.Items.ItemsText)).ToList())).ToList();
        this._errors.AddRange(Apply(configuration, races));
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
            var id = typeof(TopRacesPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<TopRacesConfiguration>(this.PersistenceContextProvider, typeof(TopRacesPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the top races configuration: {Races} races, active: {Active}.", configuration.Races.Count, this._isActive);
            this.ToastService.ShowSuccess(Resources.TopRacesSaved);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the top races configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    /// <summary>
    /// A race with its prize rows.
    /// </summary>
    private sealed class RaceRow
    {
        public RaceRow(TopRaceDefinition race)
        {
            this.Race = race;
            this.Prizes = race.Prizes.OrderBy(p => p.RankFrom).Select(p => new PrizeRow(p)).ToList();
        }

        public TopRaceDefinition Race { get; }

        public List<PrizeRow> Prizes { get; }
    }

    /// <summary>
    /// A row of a race's prizes table.
    /// </summary>
    private sealed class PrizeRow
    {
        public PrizeRow(TopRacePrize prize)
        {
            this.Prize = prize;
            this.Items = new MissionRewardText { ItemsText = MissionConfigurationText.FormatItems(prize.Items) };
        }

        public TopRacePrize Prize { get; }

        public MissionRewardText Items { get; }
    }
}
