// <copyright file="ImportServer.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.IO;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.ServerImporter;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin page for previewing, applying (with backup), and undoing local imports from legacy Season 6 or Season 9 server files and MSSQL databases.
/// </summary>
public partial class ImportServer : ComponentBase
{
    private readonly LegacyImportOptions _options = new();
    private LegacyImportPreview? _preview;
    private List<LegacyImportBackupSnapshot> _backups = [];
    private string _backupDirectory = string.Empty;
    private bool _showHelp;
    private bool _isVi;
    private bool _isBusy;
    private string? _statusMessage;
    private bool _statusIsError;

    [Inject]
    private IDataSource<GameConfiguration> DataSource { get; set; } = null!;

    [Inject]
    private IHostSettingsService HostSettingsService { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<ImportServer> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        var savePath = this.HostSettingsService.Load().SavePath;
        this._backupDirectory = Path.GetFullPath(Path.Combine(string.IsNullOrWhiteSpace(savePath) ? "../save" : savePath, "import-backups"));
        this.RefreshBackups();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void RefreshBackups()
    {
        this._backups = LegacyImportBackupSnapshot.ListAll(this._backupDirectory);
    }

    private async Task RunDryRunAsync()
    {
        this._statusMessage = null;
        this._statusIsError = false;

        var hasFolder = !string.IsNullOrWhiteSpace(this._options.FolderPath);
        var hasDb = this._options.ImportDatabaseEntities && !string.IsNullOrWhiteSpace(this._options.MssqlConnectionString);
        if (!hasFolder && !hasDb)
        {
            this._statusIsError = true;
            this._statusMessage = this.L(
                "Enter a local server folder path or an MSSQL connection string first.",
                "Vui lòng nhập đường dẫn thư mục máy chủ hoặc chuỗi kết nối MSSQL.");
            return;
        }

        if (hasFolder && !Directory.Exists(this._options.FolderPath.Trim()))
        {
            this._statusIsError = true;
            this._statusMessage = this.L(
                $"Folder not found: {this._options.FolderPath}",
                $"Không tìm thấy thư mục: {this._options.FolderPath}");
            return;
        }

        this._isBusy = true;
        try
        {
            var context = await this.DataSource.GetContextAsync(default).ConfigureAwait(true);
            var config = await this.DataSource.GetOwnerAsync(cancellationToken: default).ConfigureAwait(true);
            var accounts = await context.GetAsync<Account>(default).ConfigureAwait(true);
            var chars = await context.GetAsync<Character>(default).ConfigureAwait(true);
            var guilds = await context.GetAsync<HNMO.DataModel.Entities.Guild>(default).ConfigureAwait(true);

            var dbReader = hasDb ? new MssqlLegacyDatabaseReader() : null;
            this._preview = await LegacyServerImportEngine.BuildDryRunPreviewAsync(
                this._options,
                config,
                dbReader,
                accounts.Select(a => a.LoginName),
                chars.Select(c => c.Name),
                guilds.Where(g => g.Name != null).Select(g => g.Name!)).ConfigureAwait(true);

            this._statusMessage = this.L(
                $"Dry-run complete ({this._preview.DetectedVersion}): {this._preview.TotalMapped} mappable, {this._preview.TotalSkipped} skipped.",
                $"Xem trước hoàn tất ({this._preview.DetectedVersion}): {this._preview.TotalMapped} mục hợp lệ, {this._preview.TotalSkipped} bỏ qua.");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Dry-run server import failed.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isBusy = false;
        }
    }

    private async Task ApplyImportAsync()
    {
        if (this._preview is null)
        {
            return;
        }

        this._isBusy = true;
        this._statusMessage = null;
        this._statusIsError = false;
        try
        {
            var context = await this.DataSource.GetContextAsync(default).ConfigureAwait(true);
            var config = await this.DataSource.GetOwnerAsync(cancellationToken: default).ConfigureAwait(true);
            var dbReader = this._options.ImportDatabaseEntities && !string.IsNullOrWhiteSpace(this._options.MssqlConnectionString)
                ? new MssqlLegacyDatabaseReader()
                : null;

            var result = await LegacyServerImportEngine.ApplyImportAsync(
                this._options,
                context,
                config,
                this._backupDirectory,
                dbReader).ConfigureAwait(true);

            this.RefreshBackups();
            this._statusMessage = this.L(
                $"Import applied cleanly! Backup '{result.BackupId}' saved. Applied: {result.AppliedCount}, Skipped: {result.SkippedCount}.",
                $"Đã áp dụng nhập thành công! Bản sao lưu '{result.BackupId}' đã lưu. Đã nhập: {result.AppliedCount}, Bỏ qua: {result.SkippedCount}.");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Server import apply failed.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isBusy = false;
        }
    }

    private async Task UndoBackupAsync(string backupId)
    {
        this._isBusy = true;
        this._statusMessage = null;
        this._statusIsError = false;
        try
        {
            var context = await this.DataSource.GetContextAsync(default).ConfigureAwait(true);
            var config = await this.DataSource.GetOwnerAsync(cancellationToken: default).ConfigureAwait(true);
            var ok = await LegacyServerImportEngine.UndoBackupAsync(backupId, this._backupDirectory, context, config).ConfigureAwait(true);
            this.RefreshBackups();
            if (ok)
            {
                this._statusMessage = this.L(
                    $"Successfully rolled back backup '{backupId}'.",
                    $"Đã hoàn tác thành công bản sao lưu '{backupId}'.");
                this.ToastService.ShowSuccess(this._statusMessage);
            }
            else
            {
                this._statusIsError = true;
                this._statusMessage = this.L($"Backup '{backupId}' was not found.", $"Không tìm thấy bản sao lưu '{backupId}'.");
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Undo backup {BackupId} failed.", backupId);
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isBusy = false;
        }
    }
}
