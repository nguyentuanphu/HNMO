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
    /// <summary>The time zones offered for the server files and for HNMO (IANA ids, which .NET resolves on every platform).</summary>
    private static readonly (string Id, string Name)[] TimeZoneChoices =
    [
        ("Asia/Ho_Chi_Minh", "Việt Nam (UTC+7)"),
        ("Asia/Bangkok", "Thailand (UTC+7)"),
        ("Asia/Singapore", "Singapore / Malaysia (UTC+8)"),
        ("Asia/Manila", "Philippines (UTC+8)"),
        ("Asia/Shanghai", "China (UTC+8)"),
        ("Asia/Seoul", "Korea (UTC+9)"),
        ("Asia/Tokyo", "Japan (UTC+9)"),
        ("Australia/Perth", "Perth (UTC+8)"),
        ("Australia/Brisbane", "Brisbane (UTC+10)"),
        ("Australia/Sydney", "Sydney / Melbourne (UTC+10/+11)"),
        ("Pacific/Auckland", "New Zealand (UTC+12/+13)"),
        ("Etc/UTC", "UTC"),
        ("Europe/London", "London (UTC+0/+1)"),
        ("Europe/Berlin", "Central Europe (UTC+1/+2)"),
        ("Europe/Moscow", "Moscow (UTC+3)"),
        ("America/Sao_Paulo", "Brazil (UTC-3)"),
        ("America/New_York", "US East (UTC-5/-4)"),
        ("America/Los_Angeles", "US West (UTC-8/-7)"),
    ];

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
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

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
            var config = await this.DataSource.GetOwnerAsync(cancellationToken: default).ConfigureAwait(true);

            // The names of the accounts, characters and guilds there are already, each read in a context of its own
            // with the configuration's items: read in the configuration's context, the characters' items attached a
            // second copy of an item definition it already tracks, and on a database the preview stopped with
            // "The instance of entity type 'ItemDefinition' cannot be tracked".
            List<string> accountNames, characterNames, guildNames;
            using (var accountContext = this.PersistenceContextProvider.CreateNewTypedContext(typeof(Account), true, config))
            {
                var accounts = (await accountContext.GetAsync<Account>(default).ConfigureAwait(true)).ToList();
                accountNames = accounts.Select(a => a.LoginName).ToList();
                characterNames = accounts.SelectMany(a => a.Characters).Select(c => c.Name).ToList();
            }

            using (var guildContext = this.PersistenceContextProvider.CreateNewTypedContext(typeof(HNMO.DataModel.Entities.Guild), true, config))
            {
                var guilds = await guildContext.GetAsync<HNMO.DataModel.Entities.Guild>(default).ConfigureAwait(true);
                guildNames = guilds.Where(g => g.Name != null).Select(g => g.Name!).ToList();
            }

            var dbReader = hasDb ? new MssqlLegacyDatabaseReader() : null;
            this._preview = await LegacyServerImportEngine.BuildDryRunPreviewAsync(
                this._options,
                config,
                dbReader,
                accountNames,
                characterNames,
                guildNames).ConfigureAwait(true);

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
