// <copyright file="S6Events.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.S6Events;
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
/// Admin configuration and control page for <see cref="S6EventsPlugIn"/> (<c>/s6-events</c>).
/// </summary>
public partial class S6Events : ComponentBase
{
    private S6EventsConfiguration? _configuration;
    private S6EventsState _state = new();
    private string _lmsTimesText = "20:30";
    private string _lorenTimesText = "20:00";
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;
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
    private ILogger<S6Events> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        await this.LoadAsync().ConfigureAwait(true);
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void ReloadState()
    {
        var path = string.IsNullOrWhiteSpace(this._configuration?.FileName)
            ? new S6EventsConfiguration().FileName
            : this._configuration.FileName;
        this._state = S6EventsStore.Open(path).Snapshot();
    }

    private async Task TriggerLmsNowAsync()
    {
        try
        {
            var contexts = (this.ServiceProvider.GetService<IList<IManageableServer>>() ?? [])
                .OfType<IGameServerContextProvider>()
                .Select(s => (IGameContext)s.Context)
                .ToList();
            var result = await S6EventsPlugIn.TriggerLmsNowAsync(contexts).ConfigureAwait(true);
            switch (result)
            {
                case "Ok":
                    this._statusIsError = false;
                    this._statusMessage = this.L("LMS registration opened! Players can now join via /lms.", "Đã mở đăng ký LMS! Người chơi có thể gõ /lms để tham gia.");
                    this.ToastService.ShowSuccess(this._statusMessage);
                    break;
                case "AlreadyRunning":
                    this._statusIsError = true;
                    this._statusMessage = this.L("An LMS round is already active.", "Vòng đấu LMS hiện đang diễn ra.");
                    break;
                default:
                    this._statusIsError = true;
                    this._statusMessage = this.L("Enable Plug-in Active + Feature Enabled + LMS Enabled and click Save first.", "Hãy bật Kích Hoạt Plug-in + Bật Tính Năng + Bật LMS rồi nhấn Lưu trước.");
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

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<S6EventsConfiguration>(
                this.PersistenceContextProvider,
                typeof(S6EventsPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new S6EventsConfiguration();
            this._configuration = JsonSerializer.Deserialize<S6EventsConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new S6EventsConfiguration();
            this._lmsTimesText = string.Join(", ", this._configuration.LmsDailyTimes);
            this._lorenTimesText = string.Join(", ", this._configuration.LorenDeepDailyTimes);
            this.ReloadState();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading S6Events configuration.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
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
            cfg.LmsDailyTimes = this._lmsTimesText
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            cfg.LorenDeepDailyTimes = this._lorenTimesText
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            cfg.LmsRegistrationMinutes = Math.Clamp(cfg.LmsRegistrationMinutes, 1, 15);
            cfg.LmsDurationMinutes = Math.Clamp(cfg.LmsDurationMinutes, 1, 60);
            cfg.LorenDeepDurationMinutes = Math.Clamp(cfg.LorenDeepDurationMinutes, 5, 120);
            cfg.LmsMinPlayers = Math.Clamp(cfg.LmsMinPlayers, 2, 50);
            cfg.LmsMinLevel = Math.Clamp(cfg.LmsMinLevel, 1, 1000);

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(S6EventsPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<S6EventsConfiguration>(
                this.PersistenceContextProvider,
                typeof(S6EventsPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this._statusMessage = this.L("Season 6 event settings saved.", "Đã lưu cài đặt sự kiện Season 6.");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving S6Events configuration.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task TriggerLorenDeepNowAsync()
    {
        try
        {
            var contexts = (this.ServiceProvider.GetService<IList<IManageableServer>>() ?? [])
                .OfType<IGameServerContextProvider>()
                .Select(s => (IGameContext)s.Context)
                .ToList();
            var result = await S6EventsPlugIn.TriggerLorenDeepNowAsync(contexts).ConfigureAwait(true);
            switch (result)
            {
                case "Ok":
                    this._statusIsError = false;
                    this._statusMessage = this.L("Loren Deep assault wave 1 launched on Valley of Loren!", "Đã phát động đợt 1 Loren Deep tại Thung Lũng Loren!");
                    this.ToastService.ShowSuccess(this._statusMessage);
                    break;
                case "AlreadyRunning":
                    this._statusIsError = true;
                    this._statusMessage = this.L("A Loren Deep assault is already active.", "Sự kiện Loren Deep đang diễn ra.");
                    break;
                default:
                    this._statusIsError = true;
                    this._statusMessage = this.L("Enable Plug-in Active + Feature Enabled + Loren Deep and click Save first.", "Hãy bật Plug-in + Tính Năng + Loren Deep rồi nhấn Lưu trước.");
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
}
