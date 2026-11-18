// <copyright file="RateTuning.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="RateTuningPlugIn"/> (<c>/rate-tuning</c>).
/// </summary>
public partial class RateTuning : ComponentBase
{
    private RateTuningConfiguration? _configuration;
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
    private ILogger<RateTuning> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        await this.LoadAsync().ConfigureAwait(true);
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void ResetClassRates()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._configuration.ClassDamageRates = RateTuningConfiguration.CreateDefaultClassRates();
        this._statusIsError = false;
        this._statusMessage = this.L("Class damage multipliers reset to 100% (click Save to apply).", "Đã đặt lại tỉ lệ sát thương các phái về 100% (nhấn Lưu để áp dụng).");
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<RateTuningConfiguration>(
                this.PersistenceContextProvider,
                typeof(RateTuningPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new RateTuningConfiguration();
            var clone = JsonSerializer.Deserialize<RateTuningConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new RateTuningConfiguration();
            if (clone.ClassDamageRates.Count == 0)
            {
                clone.ClassDamageRates = RateTuningConfiguration.CreateDefaultClassRates();
            }

            this._configuration = clone;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading RateTuning configuration.");
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
            cfg.SoulSuccessRatePercent = Math.Clamp(cfg.SoulSuccessRatePercent, 0, 100);
            cfg.SoulLuckBonusPercent = Math.Clamp(cfg.SoulLuckBonusPercent, 0, 100);
            cfg.LifeSuccessRatePercent = Math.Clamp(cfg.LifeSuccessRatePercent, 0, 100);
            cfg.MaxLifeOptionLevel = Math.Clamp(cfg.MaxLifeOptionLevel, 1, 7);
            cfg.BroadcastMinChaosMixLevel = Math.Clamp(cfg.BroadcastMinChaosMixLevel, 10, 15);
            cfg.ZenDropMultiplierPercent = Math.Clamp(cfg.ZenDropMultiplierPercent, 10, 1000);
            foreach (var entry in cfg.ClassDamageRates)
            {
                entry.PvpDamagePercent = Math.Clamp(entry.PvpDamagePercent, 10, 500);
                entry.PvmDamagePercent = Math.Clamp(entry.PvmDamagePercent, 10, 500);
            }

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(RateTuningPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<RateTuningConfiguration>(
                this.PersistenceContextProvider,
                typeof(RateTuningPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this._statusMessage = this.L("Rate tuning settings saved.", "Đã lưu cài đặt tỉ lệ & cân bằng.");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving RateTuning configuration.");
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
