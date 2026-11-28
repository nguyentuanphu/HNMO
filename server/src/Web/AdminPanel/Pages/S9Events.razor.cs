// <copyright file="S9Events.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.SeasonNineEvents;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for Season 9 events and Uruk Mountain encounters (<c>/s9-events</c>).
/// </summary>
public partial class S9Events : ComponentBase
{
    private SeasonNineEventsConfiguration? _configuration;
    private List<InvasionRowEditor> _invasionRows = [];
    private string _acheronTimesText = "23:00";
    private string _arcaTimesText = "22:00";
    private string _ccsTimesText = string.Empty;
    private string _tsTimesText = "17:30, 19:00, 20:30, 22:00, 23:30";
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private string _lang = "en";
    private string? _statusMessage;
    private bool _statusIsError;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Inject]
    private ILogger<S9Events> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        this._lang = iso switch
        {
            "vi" => "vi",
            "de" => "de",
            "zh" => "zh-CN",
            _ => "en",
        };
        await this.LoadAsync().ConfigureAwait(true);
    }

    private void SetLanguage(string lang) => this._lang = lang;

    private string T(string en, string vi, string de, string zh) => this._lang switch
    {
        "vi" => vi,
        "de" => de,
        "zh-CN" => zh,
        _ => en,
    };

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<SeasonNineEventsConfiguration>(
                this.PersistenceContextProvider,
                typeof(SeasonNineEventsPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new SeasonNineEventsConfiguration();
            this._configuration = JsonSerializer.Deserialize<SeasonNineEventsConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new SeasonNineEventsConfiguration();
            this._acheronTimesText = string.Join(", ", this._configuration.AcheronGuardianDailyTimes);
            this._arcaTimesText = string.Join(", ", this._configuration.ArcaBattleDailyTimes);
            this._ccsTimesText = string.Join(", ", this._configuration.ChaosCastleSurvivalDailyTimes);
            this._tsTimesText = string.Join(", ", this._configuration.TormentedSquareDailyTimes);
            this._invasionRows = this._configuration.Invasions
                .Select(entry => new InvasionRowEditor(entry))
                .ToList();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Season 9 event settings.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task TriggerEventNowAsync(string eventKey)
    {
        try
        {
            var contexts = (this.ServiceProvider.GetService<IList<IManageableServer>>() ?? [])
                .OfType<IGameServerContextProvider>()
                .Select(s => (IGameContext)s.Context)
                .ToList();
            var result = await SeasonNineEventsPlugIn.TriggerEventNowAsync(contexts, eventKey).ConfigureAwait(true);
            switch (result)
            {
                case "Ok":
                    this._statusIsError = false;
                    this._statusMessage = this.T(
                        $"Triggered Season 9 event ({eventKey}).",
                        $"Đã kích hoạt sự kiện Season 9 ({eventKey}).",
                        $"Season-9-Event ({eventKey}) gestartet.",
                        $"已触发第 9 赛季活动 ({eventKey})。");
                    this.ToastService.ShowSuccess(this._statusMessage);
                    break;
                case "AlreadyRunning":
                    this._statusIsError = true;
                    this._statusMessage = this.T("That event is already running.", "Sự kiện này đang diễn ra.", "Dieses Event läuft bereits.", "该活动正在进行中。");
                    break;
                case "SeasonMismatch":
                    this._statusIsError = true;
                    this._statusMessage = this.T("Server is running Season 6 while Season 9 Only is checked.", "Máy chủ đang chạy Season 6 và đang bật Chỉ Season 9.", "Server läuft auf Season 6 und 'Nur Season 9' ist aktiv.", "服务器处于第 6 赛季且勾选了“仅第 9 赛季”。");
                    break;
                default:
                    this._statusIsError = true;
                    this._statusMessage = this.T("Activate Plug-in Active + Events Enabled and Save first.", "Hãy bật Plug-in Active + Bật sự kiện và Lưu trước.", "Bitte zuerst Plugin aktiv + Events aktivieren und speichern.", "请先勾选启用插件和启用活动并保存。");
                    break;
            }
        }
        catch (Exception ex)
        {
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        this._isSaving = true;
        this._statusMessage = null;
        this._statusIsError = false;
        try
        {
            static List<string> SplitList(string text) => text
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            cfg.AcheronGuardianDailyTimes = SplitList(this._acheronTimesText);
            cfg.ArcaBattleDailyTimes = SplitList(this._arcaTimesText);
            cfg.ChaosCastleSurvivalDailyTimes = SplitList(this._ccsTimesText);
            cfg.TormentedSquareDailyTimes = SplitList(this._tsTimesText);
            foreach (var row in this._invasionRows)
            {
                row.Entry.DailyTimes = SplitList(row.TimesText);
                row.Entry.MapNumbers = row.MapsText
                    .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? (short?)m : null)
                    .Where(m => m.HasValue)
                    .Select(m => m!.Value)
                    .ToList();
            }

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(SeasonNineEventsPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<SeasonNineEventsConfiguration>(
                this.PersistenceContextProvider,
                typeof(SeasonNineEventsPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this._statusMessage = this.T("Season 9 event settings saved.", "Đã lưu cấu hình sự kiện Season 9.", "Season-9-Event-Einstellungen gespeichert.", "第 9 赛季活动配置已保存。");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Season 9 event settings.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private sealed class InvasionRowEditor
    {
        public InvasionRowEditor(S9InvasionScheduleEntry entry)
        {
            this.Entry = entry;
            this.MapsText = string.Join(", ", entry.MapNumbers);
            this.TimesText = string.Join(", ", entry.DailyTimes);
        }

        public S9InvasionScheduleEntry Entry { get; }

        public string MapsText { get; set; }

        public string TimesText { get; set; }
    }
}
