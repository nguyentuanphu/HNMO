// <copyright file="StateRetention.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin page for <see cref="HnmoStateRetentionPlugIn"/> (archival policy and database row stats).
/// </summary>
public partial class StateRetention : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private HnmoStateRetentionConfiguration? _configuration;
    private HnmoStateRetentionMetadata _meta = new();
    private int _activeRows;
    private int _archivedRows;
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
    private ILogger<StateRetention> Logger { get; set; } = null!;

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

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, _, saved) = await PlugInSettings.ReadStoredAsync<HnmoStateRetentionConfiguration>(
                this.PersistenceContextProvider,
                typeof(HnmoStateRetentionPlugIn),
                default).ConfigureAwait(true);

            var cfg = saved ?? new HnmoStateRetentionConfiguration();
            cfg = JsonSerializer.Deserialize<HnmoStateRetentionConfiguration>(JsonSerializer.Serialize(cfg)) ?? new HnmoStateRetentionConfiguration();
            this._configuration = cfg;

            var store = HnmoStateStore.Open(cfg.FileName);
            var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(store?.FilePath);
            this._activeRows = backend.CountRows();
            this._archivedRows = backend.CountArchivedRows();
            this._meta = HnmoStateRetentionPlugIn.ReadMetadata(cfg.FileName);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading State Retention settings.");
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
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(HnmoStateRetentionPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L("Saved retention settings.", "Đã lưu cài đặt lưu trữ."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving State Retention settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task ArchiveNowAsync()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        var plugIn = new HnmoStateRetentionPlugIn { Configuration = cfg };
        var count = plugIn.ArchiveNow(DateTime.UtcNow);
        this.ToastService.ShowSuccess(L($"Archived {count} expired row(s).", $"Đã chuyển lưu trữ {count} dòng cũ."));
        await this.LoadAsync().ConfigureAwait(true);
    }
}
