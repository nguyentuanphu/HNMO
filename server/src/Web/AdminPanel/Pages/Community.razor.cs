// <copyright file="Community.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.Community;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// The page "Community": the one-time coins for linking Discord and joining the Zalo group (with the waiting Zalo claims
/// to approve), the community events (codes, winners, rewards), the automatic posts (kinds, Discord / website, texts,
/// the rare drop filter) and the recent highlights of the <see cref="CommunityPlugIn"/>. It saves the plug-in's
/// configuration like the plug-in editor does, so the running game servers use it at once; the Zalo decisions and
/// removed highlights go straight into the shared store.
/// </summary>
public partial class Community : ComponentBase
{
    private readonly List<EventRow> _events = new();
    private readonly List<string> _errors = new();
    private CommunityConfiguration? _configuration;
    private CommunityEngine? _engine;
    private string _dropItemsText = string.Empty;
    private string _dropMapsText = string.Empty;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private int _nextKey;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<Community> Logger { get; set; } = null!;

    private bool IsVietnamese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    /// <summary>
    /// Checks the edited configuration and puts the edited texts into it.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="events">The events with their winners and items texts.</param>
    /// <param name="dropItems">The rare drop items, "group number; ...".</param>
    /// <param name="dropMaps">The maps of the rare drops, "0, 7".</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Apply(CommunityConfiguration configuration, IReadOnlyList<(CommunityEventDefinition Event, string WinnersText, string ItemsText)> events, string? dropItems, string? dropMaps)
    {
        var errors = new List<string>();
        var engine = new CommunityEngine(configuration, new CommunityStore());
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (definition, winnersText, itemsText) in events)
        {
            definition.Id = definition.Id.Trim();
            var label = string.IsNullOrEmpty(definition.Id) ? "?" : definition.Id;
            if (!System.Text.RegularExpressions.Regex.IsMatch(definition.Id, "^[A-Za-z0-9_-]{1,40}$"))
            {
                errors.Add(string.Format(Resources.CommunityErrorId, label));
            }
            else if (!ids.Add(definition.Id))
            {
                errors.Add(string.Format(Resources.CommunityErrorDuplicateId, label));
            }

            definition.Start = definition.Start.Trim();
            definition.End = definition.End.Trim();
            var start = engine.ToUtc(definition.Start);
            var end = engine.ToUtc(definition.End);
            if ((definition.Start.Length > 0 && start is null) || (definition.End.Length > 0 && end is null) || (start is { } s && end is { } e && e <= s))
            {
                errors.Add(string.Format(Resources.CommunityErrorDates, label));
            }

            definition.Code = definition.Code.Trim();
            var code = CommunityEngine.NormalizeCode(definition.Code);
            if (definition.Code.Length > 0 && code.Length == 0)
            {
                errors.Add(string.Format(Resources.CommunityErrorCode, label));
            }
            else if (code.Length > 0 && definition.Enabled && !codes.Add(code))
            {
                errors.Add(string.Format(Resources.CommunityErrorDuplicateCode, label));
            }

            definition.Winners = (winnersText ?? string.Empty).Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (MissionConfigurationText.TryParseItems(itemsText, out var items))
            {
                definition.Reward.Items = items;
            }
            else
            {
                errors.Add(string.Format(Resources.MissionsErrorItems, label));
            }

            definition.Reward.Coins = Math.Max(0, definition.Reward.Coins);
            definition.Reward.Zen = Math.Max(0, definition.Reward.Zen);
        }

        configuration.Events = events.Select(e => e.Event).ToList();
        var dropItemList = new List<string>();
        foreach (var part in (dropItems ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (CommunityEngine.ParseItem(part) is { } item)
            {
                dropItemList.Add(string.Create(CultureInfo.InvariantCulture, $"{item.Group} {item.Number}"));
            }
            else
            {
                errors.Add(string.Format(Resources.CommunityErrorDropItem, part));
            }
        }

        configuration.Drops.Items = dropItemList;
        var maps = new List<short>();
        foreach (var part in (dropMaps ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (short.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var map))
            {
                maps.Add(map);
            }
            else
            {
                errors.Add(string.Format(Resources.CommunityErrorMap, part));
            }
        }

        configuration.Drops.Maps = maps.Distinct().ToList();
        configuration.Drops.MinimumLevel = Math.Clamp(configuration.Drops.MinimumLevel, 0, 15);
        configuration.Discord.Coins = Math.Max(0, configuration.Discord.Coins);
        configuration.Zalo.Coins = Math.Max(0, configuration.Zalo.Coins);
        configuration.LadderRanksPosted = Math.Clamp(configuration.LadderRanksPosted, 1, 10);
        configuration.HighlightsShown = Math.Clamp(configuration.HighlightsShown, 1, 100);
        configuration.DropCooldownSeconds = Math.Clamp(configuration.DropCooldownSeconds, 0, 3600);
        return errors;
    }

    /// <summary>Gets the caption of a post kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The caption.</returns>
    internal static string KindCaption(CommunityPostKind kind) => kind switch
    {
        CommunityPostKind.RareDrop => Resources.CommunityKindRareDrop,
        CommunityPostKind.WorldBossKill => Resources.CommunityKindWorldBoss,
        CommunityPostKind.GuildLevelUp => Resources.CommunityKindGuildLevel,
        CommunityPostKind.GuildLadderWinner => Resources.CommunityKindGuildLadder,
        CommunityPostKind.TopRaceStarted => Resources.CommunityKindTopRaceStarted,
        CommunityPostKind.TopRaceWinner => Resources.CommunityKindTopRaceWinner,
        _ => Resources.CommunityKindCastleSiege,
    };

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private string Local(DateTime? utc) => utc is { } value && this._engine is { } engine
        ? engine.ToLocal(value).ToString("ddd d MMM yyyy HH:mm", CultureInfo.CurrentUICulture)
        : "-";

    private string PhaseCaption(CommunityEngine engine, CommunityEventDefinition definition)
    {
        var phase = engine.Phase(definition, DateTime.UtcNow);
        return phase switch
        {
            CommunityEventPhase.Upcoming => Resources.CommunityPhaseUpcoming,
            CommunityEventPhase.Ended => Resources.CommunityPhaseEnded,
            _ => Resources.CommunityPhaseOpen,
        };
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            // Straight from the database: the panel's cached game configuration can miss the row the game
            // server added when it first started.
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<CommunityConfiguration>(this.PersistenceContextProvider, typeof(CommunityPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = saved ?? new CommunityConfiguration();

            // A copy, so that an unsaved edit doesn't change a cached configuration.
            configuration = JsonSerializer.Deserialize<CommunityConfiguration>(JsonSerializer.Serialize(configuration)) ?? new CommunityConfiguration();
            this._configuration = configuration;
            this._events.Clear();
            this._events.AddRange(configuration.Events.Select(e => new EventRow(e, this._nextKey++)));
            this._dropItemsText = string.Join("; ", configuration.Drops.Items);
            this._dropMapsText = string.Join(", ", configuration.Drops.Maps);
            this._engine = new CommunityEngine(configuration, CommunityStore.Open(configuration.FileName));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the community configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void AddEvent()
    {
        var number = this._events.Count + 1;
        var id = $"event{number}";
        while (this._events.Any(e => string.Equals(e.Definition.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            id = $"event{++number}";
        }

        var start = this._engine?.ToLocal(DateTime.UtcNow).Date ?? DateTime.Today;
        this._events.Add(new EventRow(
            new CommunityEventDefinition
            {
                Id = id,
                TitleEn = "Community event",
                TitleVi = "Sự kiện cộng đồng",
                Start = start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                End = start.AddDays(7).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            },
            this._nextKey++));
    }

    private void RemoveEvent(EventRow row) => this._events.Remove(row);

    private void DecideZalo(string login, bool approve)
    {
        if (this._engine is not { } engine)
        {
            return;
        }

        if (engine.DecideZalo(login, approve, DateTime.UtcNow))
        {
            this.SaveStore(engine);
            this.Logger.LogInformation("The Zalo claim of {Login} was {Decision}.", login, approve ? "approved" : "rejected");
            this.ToastService.ShowSuccess(approve ? string.Format(Resources.CommunityZaloApproved, login) : string.Format(Resources.CommunityZaloRejected, login));
        }
    }

    private void RemoveHighlight(CommunityHighlight highlight)
    {
        if (this._engine is { } engine && engine.RemoveHighlight(highlight.AtUtc, highlight.TextEn))
        {
            this.SaveStore(engine);
        }
    }

    private void SaveStore(CommunityEngine engine)
    {
        lock (engine.Store.Lock)
        {
            engine.Store.SaveIfChanged();
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        this._errors.AddRange(Apply(configuration, this._events.Select(r => (r.Definition, r.WinnersText, r.ItemsText)).ToList(), this._dropItemsText, this._dropMapsText));
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
            var id = typeof(CommunityPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<CommunityConfiguration>(this.PersistenceContextProvider, typeof(CommunityPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the community configuration: {Events} events, active: {Active}.", configuration.Events.Count, this._isActive);
            this.ToastService.ShowSuccess(Resources.CommunitySaved);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the community configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    /// <summary>
    /// An event being edited, with its winners and items as text.
    /// </summary>
    private sealed class EventRow
    {
        public EventRow(CommunityEventDefinition definition, int key)
        {
            this.Definition = definition;
            this.Key = key;
            this.WinnersText = string.Join(", ", definition.Winners);
            this.ItemsText = MissionConfigurationText.FormatItems(definition.Reward.Items);
        }

        public CommunityEventDefinition Definition { get; }

        public int Key { get; }

        public string WinnersText { get; set; }

        public string ItemsText { get; set; }
    }
}
