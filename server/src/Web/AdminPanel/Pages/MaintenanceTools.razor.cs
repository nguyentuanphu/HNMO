// <copyright file="MaintenanceTools.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin panel page for audited maintenance operations with dry-run previews (ROADMAP 14.8).
/// </summary>
public partial class MaintenanceTools : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private IReadOnlyList<MaintenanceAccountCandidate> _inactiveCandidates = [];
    private IReadOnlyList<MaintenanceDuplicateItemOccurrence> _duplicateItems = [];
    private IReadOnlyList<MaintenanceExperienceIssue> _expIssues = [];
    private int _minInactiveDays = 90;
    private bool _emptyOnly = true;
    private bool _inactivePreviewed;
    private bool _dupePreviewed;
    private bool _expPreviewed;
    private bool _isBusy;
    private bool _showHelp;

    [Inject]
    private MaintenanceToolsOperations Operations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<MaintenanceTools> Logger { get; set; } = null!;

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

    private async Task PreviewInactiveAsync()
    {
        await this.RunAsync(async () =>
        {
            this._inactiveCandidates = await this.Operations.PreviewInactiveAccountsAsync(this._minInactiveDays, this._emptyOnly, default).ConfigureAwait(true);
            this._inactivePreviewed = true;
        }).ConfigureAwait(true);
    }

    private async Task PurgeInactiveAsync()
    {
        await this.RunAsync(async () =>
        {
            var removed = await this.Operations.PurgeInactiveAccountsAsync(this._minInactiveDays, this._emptyOnly, default).ConfigureAwait(true);
            this._inactiveCandidates = await this.Operations.PreviewInactiveAccountsAsync(this._minInactiveDays, this._emptyOnly, default).ConfigureAwait(true);
            this.ToastService.ShowSuccess(L($"Deleted {removed} inactive account(s).", $"Đã xóa {removed} tài khoản không hoạt động."));
        }).ConfigureAwait(true);
    }

    private async Task PreviewDuplicatesAsync()
    {
        await this.RunAsync(async () =>
        {
            this._duplicateItems = await this.Operations.PreviewDuplicateItemIdsAsync(default).ConfigureAwait(true);
            this._dupePreviewed = true;
        }).ConfigureAwait(true);
    }

    private async Task RemoveDuplicatesAsync()
    {
        await this.RunAsync(async () =>
        {
            var removed = await this.Operations.RemoveDuplicateItemsAsync(default).ConfigureAwait(true);
            this._duplicateItems = await this.Operations.PreviewDuplicateItemIdsAsync(default).ConfigureAwait(true);
            this.ToastService.ShowSuccess(L($"Removed {removed} duplicate item copy(ies).", $"Đã xóa {removed} bản sao vật phẩm trùng."));
        }).ConfigureAwait(true);
    }

    private async Task PreviewExpAsync()
    {
        await this.RunAsync(async () =>
        {
            this._expIssues = await this.Operations.PreviewExperienceIssuesAsync(default).ConfigureAwait(true);
            this._expPreviewed = true;
        }).ConfigureAwait(true);
    }

    private async Task FixExpAsync()
    {
        await this.RunAsync(async () =>
        {
            var fixedCount = await this.Operations.FixExperienceIssuesAsync(default).ConfigureAwait(true);
            this._expIssues = await this.Operations.PreviewExperienceIssuesAsync(default).ConfigureAwait(true);
            this.ToastService.ShowSuccess(L($"Fixed experience on {fixedCount} character(s).", $"Đã sửa kinh nghiệm cho {fixedCount} nhân vật."));
        }).ConfigureAwait(true);
    }

    private async Task RunAsync(Func<Task> action)
    {
        this._isBusy = true;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Maintenance operation failed.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isBusy = false;
        }
    }
}
