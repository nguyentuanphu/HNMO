// <copyright file="FixedOptions.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.FixedItemOptions;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="FixedItemOptionsPlugIn"/>.
/// </summary>
public partial class FixedOptions : ComponentBase
{
    private FixedItemOptionsConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<FixedOptions> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void AddRule()
    {
        if (this._configuration is null)
        {
            return;
        }

        var next = this._configuration.Rules.Count + 1;
        this._configuration.Rules.Add(new FixedItemOptionRule
        {
            Id = $"rule_{next}",
            Enabled = true,
            Label = $"Rule {next}",
            ItemGroup = 0,
            ItemNumber = -1,
            SourceFilter = "box_event",
            PinLuck = true,
        });
    }

    private void RemoveRule(int index)
    {
        if (this._configuration is null || index < 0 || index >= this._configuration.Rules.Count)
        {
            return;
        }

        this._configuration.Rules.RemoveAt(index);
    }

    private void RestoreDefaults()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._configuration.Rules = FixedItemOptionsConfiguration.CreateDefaultRules();
        this.ToastService.ShowSuccess(this.L("Starter fixed option rules restored (click Save to apply).", "Đã khôi phục quy tắc mẫu (bấm Lưu để áp dụng)."));
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<FixedItemOptionsConfiguration>(
                this.PersistenceContextProvider,
                typeof(FixedItemOptionsPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new FixedItemOptionsConfiguration();
            this._configuration = JsonSerializer.Deserialize<FixedItemOptionsConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new FixedItemOptionsConfiguration();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Fixed Item Options configuration.");
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
            cfg.Enabled = this._isActive;
            foreach (var r in cfg.Rules)
            {
                r.Id = string.IsNullOrWhiteSpace(r.Id) ? Guid.NewGuid().ToString("N")[..8] : r.Id.Trim();
                r.Label = (r.Label ?? string.Empty).Trim();
                r.SourceFilter = FixedItemOptionsEngine.NormalizeSourceFilter(r.SourceFilter);
                r.MinLevel = Math.Min((byte)15, r.MinLevel);
                r.MinOptionLevel = Math.Min((byte)7, r.MinOptionLevel);
                r.MinExcellentCount = Math.Clamp(r.MinExcellentCount, 0, 6);
                r.ForcedExcellentBits = Math.Clamp(r.ForcedExcellentBits, 0, 63);
            }

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(FixedItemOptionsPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<FixedItemOptionsConfiguration>(
                this.PersistenceContextProvider,
                typeof(FixedItemOptionsPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Fixed item options saved.", "Đã lưu cài đặt cố định dòng trang bị."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Fixed Item Options settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
