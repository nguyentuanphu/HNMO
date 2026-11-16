// <copyright file="CatchupCap.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin configuration page for <see cref="CatchupCapPlugIn"/>.
/// </summary>
public partial class CatchupCap : ComponentBase
{
    private CatchupCapConfiguration? _configuration;
    private CatchupCapSnapshot? _snapshot;
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
    private ILogger<CatchupCap> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<CatchupCapConfiguration>(
                this.PersistenceContextProvider,
                typeof(CatchupCapPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new CatchupCapConfiguration();
            cfg = JsonSerializer.Deserialize<CatchupCapConfiguration>(JsonSerializer.Serialize(cfg)) ?? new CatchupCapConfiguration();
            this._configuration = cfg;
            this._snapshot = new CatchupCapEngine(cfg, CatchupCapStore.Open(cfg.FileName), isActive).GetSnapshot(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Catch-up Cap configuration.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task ResetTriggersAsync()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        CatchupCapStore.Open(cfg.FileName).Mutate(d =>
        {
            d.LevelTriggeredAtUtc = null;
            d.LevelFirstPlayer = string.Empty;
            d.LevelTriggerDayKey = string.Empty;
            d.ResetTriggeredAtUtc = null;
            d.ResetFirstPlayer = string.Empty;
            d.ResetTriggerDayKey = string.Empty;
            return (true, true);
        });
        this.ToastService.ShowSuccess(L("Cleared trigger history.", "Đã xóa dấu mốc mở khóa."));
        await this.LoadAsync().ConfigureAwait(true);
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
            var id = typeof(CatchupCapPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<CatchupCapConfiguration>(this.PersistenceContextProvider, typeof(CatchupCapPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Catch-up Cap settings.", "Đã lưu cài đặt Giới hạn đuổi kịp."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Catch-up Cap settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
