// <copyright file="AntiCheat.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.AntiCheat;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration and audit log page for <see cref="AntiCheatPlugIn"/> (<c>/anti-cheat</c>).
/// </summary>
public partial class AntiCheat : ComponentBase
{
    private AntiCheatConfiguration? _configuration;
    private List<AntiCheatViolationEntry> _entries = [];
    private string _filter = string.Empty;
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
    private ILogger<AntiCheat> Logger { get; set; } = null!;

    private List<AntiCheatViolationEntry> FilteredEntries => string.IsNullOrWhiteSpace(this._filter)
        ? this._entries
        : this._entries.Where(e =>
            e.Account.Contains(this._filter, StringComparison.OrdinalIgnoreCase)
            || e.Character.Contains(this._filter, StringComparison.OrdinalIgnoreCase)
            || e.IpAddress.Contains(this._filter, StringComparison.OrdinalIgnoreCase)
            || e.ViolationType.Contains(this._filter, StringComparison.OrdinalIgnoreCase)
            || e.Details.Contains(this._filter, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void ReloadAudit()
    {
        var path = this._configuration?.FileName ?? ("../save/" + HNMO.GameLogic.PlugIns.HnmoStateStore.StateFileName);
        this._entries = AntiCheatAuditStore.Open(path).ListRecent();
    }

    private void ClearAudit()
    {
        var path = this._configuration?.FileName ?? ("../save/" + HNMO.GameLogic.PlugIns.HnmoStateStore.StateFileName);
        AntiCheatAuditStore.Open(path).Clear();
        this._entries = [];
        this._statusIsError = false;
        this._statusMessage = this.L("Audit log cleared.", "Đã xóa nhật ký vi phạm.");
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<AntiCheatConfiguration>(
                this.PersistenceContextProvider,
                typeof(AntiCheatPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new AntiCheatConfiguration();
            this._configuration = JsonSerializer.Deserialize<AntiCheatConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new AntiCheatConfiguration();
            this.ReloadAudit();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading AntiCheat configuration.");
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
            cfg.MaxConnectionsPerIp = Math.Clamp(cfg.MaxConnectionsPerIp, 0, 100);
            cfg.BaseMinAttackIntervalMs = Math.Clamp(cfg.BaseMinAttackIntervalMs, 50, 2000);
            cfg.MinimumAttackIntervalFloorMs = Math.Clamp(cfg.MinimumAttackIntervalFloorMs, 30, 1000);
            cfg.LatencyToleranceFactor = Math.Clamp(cfg.LatencyToleranceFactor, 0.3, 1.5);
            cfg.HitHackMaxAgility = Math.Clamp(cfg.HitHackMaxAgility, 1000, 65535);
            cfg.StrikesBeforeDisconnect = Math.Clamp(cfg.StrikesBeforeDisconnect, 0, 1000);
            cfg.StrikeWindowMinutes = Math.Clamp(cfg.StrikeWindowMinutes, 1, 1440);
            cfg.MaxAuditEntries = Math.Clamp(cfg.MaxAuditEntries, 50, 5000);

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(AntiCheatPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<AntiCheatConfiguration>(
                this.PersistenceContextProvider,
                typeof(AntiCheatPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this._statusMessage = this.L("Anti-cheat settings saved.", "Đã lưu cài đặt chống gian lận.");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving AntiCheat configuration.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
