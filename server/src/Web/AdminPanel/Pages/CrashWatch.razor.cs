// <copyright file="CrashWatch.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.IO;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Admin Panel page displaying Crash Watch incidents, admin notices, and <c>/control</c> auto-restart policy settings.
/// </summary>
public partial class CrashWatch : ComponentBase
{
    private MaintenanceSettings _settings = new();
    private IReadOnlyList<CrashIncident> _incidents = [];
    private int _unacknowledgedCount;
    private string _latestSummary = string.Empty;
    private string _newWebhookUrl = string.Empty;
    private bool _hasExistingWebhook;
    private bool _showHelp;
    private bool _isVi;

    [Inject]
    private IHostSettingsService HostSettingsService { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private AdminPolicyGuard PolicyGuard { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        this.Refresh();
    }

    private string SaveDir => Path.GetDirectoryName(this.HostSettingsService.SettingsFilePath) ?? AppContext.BaseDirectory;

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

    private void SetLanguage(bool vi)
    {
        this._isVi = vi;
        this.UpdateBannerSummary();
    }

    private void Refresh()
    {
        var loaded = MaintenanceSettings.Load(this.SaveDir);
        this._hasExistingWebhook = !string.IsNullOrWhiteSpace(loaded.CrashDiscordWebhookUrl);
        this._settings = new MaintenanceSettings
        {
            MaxLogDays = loaded.MaxLogDays,
            MaxLogTotalMb = loaded.MaxLogTotalMb,
            MaxLogFileMb = loaded.MaxLogFileMb,
            DbLogRetentionDays = loaded.DbLogRetentionDays,
            AutoBackupEnabled = loaded.AutoBackupEnabled,
            AutoBackupHourSydney = loaded.AutoBackupHourSydney,
            BackupKeepDaily = loaded.BackupKeepDaily,
            BackupKeepWeekly = loaded.BackupKeepWeekly,
            BackupKeepMonthly = loaded.BackupKeepMonthly,
            VacuumIntervalHours = loaded.VacuumIntervalHours,
            LowDiskWarningMb = loaded.LowDiskWarningMb,
            AutoRestartOnCrash = loaded.AutoRestartOnCrash,
            MaxCrashRestartsPerHour = loaded.MaxCrashRestartsPerHour,
            CrashRestartDelaySeconds = loaded.CrashRestartDelaySeconds,
            CrashDiscordWebhookUrl = loaded.CrashDiscordWebhookUrl,
        };
        this._incidents = CrashWatchStore.ListIncidents(this.SaveDir);
        this._unacknowledgedCount = this._incidents.Count(i => !i.Acknowledged);
        this.UpdateBannerSummary();
    }

    private void UpdateBannerSummary()
    {
        var unread = this._incidents.FirstOrDefault(i => !i.Acknowledged);
        this._latestSummary = unread is null
            ? string.Empty
            : $"{unread.OccurredAtSydney} — {(this._isVi ? unread.SummaryVi : unread.SummaryEn)}";
    }

    private async Task AcknowledgeAllAsync()
    {
        if (!await this.EnsureAdministratorAsync().ConfigureAwait(true))
        {
            return;
        }

        CrashWatchStore.AcknowledgeAll(this.SaveDir);
        this.Refresh();
        this.ToastService.ShowSuccess(this.L("Crash watch notices acknowledged.", "Đã xác nhận cảnh báo sự cố."));
    }

    private async Task SaveSettingsAsync()
    {
        if (!await this.EnsureAdministratorAsync().ConfigureAwait(true))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(this._newWebhookUrl))
        {
            this._settings.CrashDiscordWebhookUrl = this._newWebhookUrl.Trim();
        }

        MaintenanceSettings.Save(this.SaveDir, this._settings);
        this._newWebhookUrl = string.Empty;
        this.Refresh();
        this.ToastService.ShowSuccess(this.L("Crash watch policy saved.", "Đã lưu chính sách Giám sát sự cố."));
    }
}
