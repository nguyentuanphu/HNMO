// <copyright file="EventAlerts.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.EventAlerts;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="EventAlertsPlugIn"/> (Web Push VAPID &amp; Discord channel posts).
/// </summary>
public partial class EventAlerts : ComponentBase
{
    private EventAlertsConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isSendingTest;
    private bool _showHelp;
    private bool _isVi;
    private int _subscriberCount;
    private IReadOnlyList<EventAlertSentLog> _logs = [];

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private AdminPolicyGuard PolicyGuard { get; set; } = null!;

    [Inject]
    private ILogger<EventAlerts> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private static string FormatSydney(DateTime utc)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.Utc;
            }
        }

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz)
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private async Task<bool> EnsureAdministratorAsync()
    {
        if (await this.PolicyGuard.IsAuthorizedAsync(AdminPolicies.Administrator).ConfigureAwait(true))
        {
            return true;
        }

        this.ToastService.ShowError(this.L("The operation is not allowed for this caller.", "Bạn không có quyền thực hiện thao tác này."));
        return false;
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private async Task RegenerateVapidKeysAsync()
    {
        if (!await this.EnsureAdministratorAsync().ConfigureAwait(true))
        {
            return;
        }

        if (this._configuration is null)
        {
            return;
        }

        var store = EventAlertStore.Open(this._configuration.FileName);
        var (pub, priv) = store.RegenerateVapidKeys();
        this._configuration.VapidPublicKey = pub;
        this._configuration.VapidPrivateKey = priv;
        this.ToastService.ShowSuccess(this.L("Generated new VAPID P-256 keypair (click Save Settings to persist).", "Đã tạo cặp khóa VAPID P-256 mới (bấm Lưu Cài Đặt để áp dụng)."));
    }

    private async Task SendTestAlertAsync()
    {
        if (!await this.EnsureAdministratorAsync().ConfigureAwait(true))
        {
            return;
        }

        if (this._configuration is null)
        {
            return;
        }

        this._isSendingTest = true;
        try
        {
            var store = EventAlertStore.Open(this._configuration.FileName);
            var candidate = new EventAlertCandidate(
                $"test:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                "world_boss",
                "HNMO Test Event Alert",
                "Thử Nghiệm Thông Báo Sự Kiện",
                "Web Push + Discord integration test notification.",
                "Thông báo kiểm tra kết nối Web Push + Discord.");
            var log = await EventAlertsPlugIn.DispatchCandidateAsync(this._configuration, store, candidate, this.Logger).ConfigureAwait(true);
            this._logs = store.ListLogs();
            this._subscriberCount = store.ListSubscriptions().Count;
            this.ToastService.ShowSuccess(this.L(
                $"Test sent: {log.PushSucceeded} browser(s), Discord={(log.DiscordPosted ? "OK" : "Skipped/Failed")}.",
                $"Đã gửi thử: {log.PushSucceeded} trình duyệt, Discord={(log.DiscordPosted ? "OK" : "Bỏ qua/Lỗi")}."));
        }
        finally
        {
            this._isSendingTest = false;
        }
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<EventAlertsConfiguration>(
                this.PersistenceContextProvider,
                typeof(EventAlertsPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new EventAlertsConfiguration();
            this._configuration = JsonSerializer.Deserialize<EventAlertsConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new EventAlertsConfiguration();

            var store = EventAlertStore.Open(this._configuration.FileName);
            var (pub, priv) = store.EnsureVapidKeys(this._configuration.VapidPublicKey, this._configuration.VapidPrivateKey);
            this._configuration.VapidPublicKey = pub;
            this._configuration.VapidPrivateKey = priv;
            this._subscriberCount = store.ListSubscriptions().Count;
            this._logs = store.ListLogs();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Event Alerts configuration.");
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
        try
        {
            cfg.Enabled = this._isActive;
            cfg.LeadTimeMinutes = Math.Clamp(cfg.LeadTimeMinutes, 0, 30);
            cfg.DiscordWebhookUrl = (cfg.DiscordWebhookUrl ?? string.Empty).Trim();
            cfg.VapidSubject = string.IsNullOrWhiteSpace(cfg.VapidSubject) ? "mailto:admin@hnmo.local" : cfg.VapidSubject.Trim();
            var store = EventAlertStore.Open(cfg.FileName);
            var (pub, priv) = store.EnsureVapidKeys(cfg.VapidPublicKey, cfg.VapidPrivateKey);
            cfg.VapidPublicKey = pub;
            cfg.VapidPrivateKey = priv;

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(EventAlertsPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<EventAlertsConfiguration>(
                this.PersistenceContextProvider,
                typeof(EventAlertsPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Event alerts settings saved.", "Đã lưu cài đặt thông báo sự kiện."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Event Alerts settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
