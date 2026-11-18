// <copyright file="QolSettings.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Qol;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="QolPlugIn"/> (<c>/qol-settings</c>).
/// </summary>
public partial class QolSettings : ComponentBase
{
    private QolConfiguration? _configuration;
    private QolState _state = new();
    private string _allowedMapsText = "0, 2, 3";
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
    private ILogger<QolSettings> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        await this.LoadAsync().ConfigureAwait(true);
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void ReloadState()
    {
        var path = string.IsNullOrWhiteSpace(this._configuration?.FileName)
            ? new QolConfiguration().FileName
            : this._configuration.FileName;
        this._state = QolStore.Open(path).Snapshot();
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<QolConfiguration>(
                this.PersistenceContextProvider,
                typeof(QolPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new QolConfiguration();
            this._configuration = JsonSerializer.Deserialize<QolConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new QolConfiguration();
            this._allowedMapsText = string.Join(", ", this._configuration.MiningAllowedMapNumbers);
            this.ReloadState();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading QolSettings configuration.");
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
            cfg.MiningCooldownSeconds = Math.Clamp(cfg.MiningCooldownSeconds, 1, 3600);
            cfg.MiningAllowedMapNumbers = this._allowedMapsText
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) ? (short?)num : null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .Distinct()
                .ToList();

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(QolPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<QolConfiguration>(
                this.PersistenceContextProvider,
                typeof(QolPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this._statusMessage = this.L("QoL settings saved.", "Đã lưu cài đặt QoL.");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving QolSettings configuration.");
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
