// <copyright file="DbLogRetention.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HNMO.Interfaces;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Admin page for configuring database chat/audit retention (<c>DbLogRetentionDays</c>) and exporting old entries before deletion.
/// </summary>
public partial class DbLogRetention : ComponentBase
{
    private MaintenanceSettings _settings = new();
    private IReadOnlyList<ArchiveFileSummary> _archives = [];
    private string? _lastMessage;
    private bool _isBusy;
    private bool _showHelp;

    /// <summary>Gets or sets optional save-directory override (used in tests).</summary>
    [Parameter]
    public string? SaveDirectory { get; set; }

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        var saveDir = this.ResolveSaveDir();
        this._settings = MaintenanceSettings.Load(saveDir);
        this.RefreshArchives(saveDir);
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private string ResolveSaveDir() =>
        string.IsNullOrWhiteSpace(this.SaveDirectory)
            ? HNMO.GameLogic.PlugIns.HnmoSaveFolder.Default.Directory
            : Path.GetFullPath(this.SaveDirectory);

    private void RefreshArchives(string saveDir)
    {
        var archiveDir = Path.Combine(saveDir, "archives");
        if (!Directory.Exists(archiveDir))
        {
            this._archives = [];
            return;
        }

        this._archives = Directory.GetFiles(archiveDir, "db-logs-*.json.gz", SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new ArchiveFileSummary(f.Name, f.Length, ControlOperations.FormatSydneyTime(new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero))))
            .ToArray();
    }

    private void SaveSettings()
    {
        this._isBusy = true;
        try
        {
            var saveDir = this.ResolveSaveDir();
            MaintenanceSettings.Save(saveDir, this._settings);
            this._settings = MaintenanceSettings.Load(saveDir);
            this._lastMessage = L("Settings saved.", "Đã lưu cài đặt.");
            this.ToastService.ShowSuccess(this._lastMessage);
        }
        finally
        {
            this._isBusy = false;
        }
    }

    private void ExportAndPurgeNow()
    {
        this._isBusy = true;
        try
        {
            var saveDir = this.ResolveSaveDir();
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, this._settings.DbLogRetentionDays));
            var exporter = DbLogRetentionRegistry.Current;
            if (exporter is null)
            {
                this._lastMessage = L("No PostgreSQL connection is active; nothing to purge.", "Chưa kết nối PostgreSQL; không có dữ liệu để xuất.");
                return;
            }

            var res = exporter.ExportAndPurgeOlderThan(cutoff, Path.Combine(saveDir, "archives"));
            this.RefreshArchives(saveDir);
            this._lastMessage = res.ExportArchivePath is null
                ? L("No logs older than the retention window were found.", "Không có nhật ký nào vượt quá số ngày lưu trữ.")
                : L($"Exported & purged {res.DeletedChatRows} chat + {res.DeletedAuditRows} audit rows.", $"Đã xuất & dọn {res.DeletedChatRows} dòng chat + {res.DeletedAuditRows} dòng quản trị.");
        }
        catch (Exception ex)
        {
            this._lastMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isBusy = false;
        }
    }

    private sealed record ArchiveFileSummary(string FileName, long SizeBytes, string CreatedSydney);
}
