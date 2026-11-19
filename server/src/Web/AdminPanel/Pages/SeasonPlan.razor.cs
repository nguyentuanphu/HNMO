// <copyright file="SeasonPlan.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.SeasonPlan;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration and live stage toggle page for <see cref="SeasonPlanPlugIn"/>.
/// </summary>
public partial class SeasonPlan : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private SeasonPlanConfiguration? _configuration;
    private readonly List<StageRow> _rows = [];
    private Dictionary<string, DateTime> _unlocked = new(StringComparer.OrdinalIgnoreCase);
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<SeasonPlan> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string SydneyTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SydneyZone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    private void AddStage()
    {
        this._rows.Add(new StageRow(new SeasonStageDefinition
        {
            Id = $"stage-{this._rows.Count + 1}",
            NameEn = $"Stage {this._rows.Count + 1}",
            NameVi = $"Giai đoạn {this._rows.Count + 1}",
        }));
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<SeasonPlanConfiguration>(
                this.PersistenceContextProvider,
                typeof(SeasonPlanPlugIn),
                default).ConfigureAwait(true);

            this._isActive = isActive;
            var cfg = saved ?? new SeasonPlanConfiguration();
            cfg = JsonSerializer.Deserialize<SeasonPlanConfiguration>(JsonSerializer.Serialize(cfg)) ?? new SeasonPlanConfiguration();
            this._configuration = cfg;
            this._rows.Clear();
            this._rows.AddRange(cfg.Stages.Select(s => new StageRow(s)));

            var store = SeasonPlanStore.Open(cfg.FileName);
            this._unlocked = store.Read(d => new Dictionary<string, DateTime>(d.UnlockedStages, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Season Plan.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task ToggleUnlockAsync(string stageId, bool unlock)
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        var engine = new SeasonPlanEngine(cfg, SeasonPlanStore.Open(cfg.FileName), enabled: true);
        engine.SetStageUnlocked(stageId, unlock, DateTime.UtcNow);
        this.ToastService.ShowSuccess(unlock ? L("Stage unlocked.", "Đã mở khóa giai đoạn.") : L("Stage locked.", "Đã khóa giai đoạn."));
        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        cfg.Stages = this._rows.Select(r =>
        {
            r.Apply();
            return r.Stage;
        }).Where(s => !string.IsNullOrWhiteSpace(s.Id)).ToList();

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(SeasonPlanPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<SeasonPlanConfiguration>(this.PersistenceContextProvider, typeof(SeasonPlanPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Season Plan.", "Đã lưu Lộ trình mùa giải."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Season Plan.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private sealed class StageRow
    {
        public StageRow(SeasonStageDefinition stage)
        {
            this.Stage = stage;
            this.DateText = stage.UnlockAtUtc is { } u
                ? DateTime.SpecifyKind(u, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : string.Empty;
            this.GatesText = string.Join(", ", stage.Unlocks.Select(g => g.ToString()));
        }

        public SeasonStageDefinition Stage { get; }

        public string DateText { get; set; }

        public string GatesText { get; set; }

        public void Apply()
        {
            this.Stage.UnlockAtUtc = DateTime.TryParseExact(
                this.DateText?.Trim(),
                ["yyyy-MM-dd HH:mm", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt)
                ? dt
                : null;

            this.Stage.Unlocks = (this.GatesText ?? string.Empty)
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<SeasonFeatureGate>(s, ignoreCase: true, out var g) ? (SeasonFeatureGate?)g : null)
                .OfType<SeasonFeatureGate>()
                .Distinct()
                .ToList();
        }
    }
}
