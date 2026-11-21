// <copyright file="UltimateAutoPlay.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.UltimateAutoPlay;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin panel configuration page for <see cref="UltimateAutoPlayPlugIn"/>.
/// </summary>
public partial class UltimateAutoPlay : ComponentBase
{
    private UltimateAutoPlayConfiguration? _configuration;
    private bool _isActive = true;
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
    private ILogger<UltimateAutoPlay> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private RenderFragment RenderModeSelect(string id, UltimateFeatureMode current, Action<UltimateFeatureMode> setter) => builder =>
    {
        builder.OpenElement(0, "select");
        builder.AddAttribute(1, "id", id);
        builder.AddAttribute(2, "class", "form-select form-select-sm");
        builder.AddAttribute(3, "value", (int)current);
        builder.AddAttribute(4, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            if (int.TryParse(e.Value?.ToString(), out var parsed) && Enum.IsDefined(typeof(UltimateFeatureMode), parsed))
            {
                setter((UltimateFeatureMode)parsed);
            }
        }));
        AddOption(builder, 5, UltimateFeatureMode.Off, L("Off", "Tắt"));
        AddOption(builder, 8, UltimateFeatureMode.Free, L("Free (All)", "Miễn phí"));
        AddOption(builder, 11, UltimateFeatureMode.VipOnly, L("VIP Only", "Chỉ VIP"));
        builder.CloseElement();
    };

    private static void AddOption(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int seq, UltimateFeatureMode mode, string label)
    {
        builder.OpenElement(seq, "option");
        builder.AddAttribute(seq + 1, "value", (int)mode);
        builder.AddContent(seq + 2, label);
        builder.CloseElement();
    }

    private void ApplyRecommendedSplit()
    {
        if (this._configuration is null) return;
        this._configuration.Enabled = true;
        this._configuration.SparkleGuide = UltimateFeatureMode.Free;
        this._configuration.PresetsAndStats = UltimateFeatureMode.Free;
        this._configuration.ManaSdPotions = UltimateFeatureMode.VipOnly;
        this._configuration.JewelDeposit = UltimateFeatureMode.VipOnly;
        this._configuration.TargetPriority = UltimateFeatureMode.VipOnly;
        this._configuration.Patrol = UltimateFeatureMode.VipOnly;
        this._configuration.Survival = UltimateFeatureMode.VipOnly;
        this._configuration.BossFocus = UltimateFeatureMode.VipOnly;
    }

    private void ApplyAllFree() => this.SetAllModes(UltimateFeatureMode.Free);

    private void ApplyAllVip() => this.SetAllModes(UltimateFeatureMode.VipOnly);

    private void SetAllModes(UltimateFeatureMode mode)
    {
        if (this._configuration is null) return;
        this._configuration.Enabled = true;
        this._configuration.SparkleGuide = mode;
        this._configuration.ManaSdPotions = mode;
        this._configuration.JewelDeposit = mode;
        this._configuration.TargetPriority = mode;
        this._configuration.Patrol = mode;
        this._configuration.Survival = mode;
        this._configuration.BossFocus = mode;
        this._configuration.PresetsAndStats = mode;
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, active, cfg) = await PlugInSettings.ReadStoredAsync<UltimateAutoPlayConfiguration>(
                this.PersistenceContextProvider,
                typeof(UltimateAutoPlayPlugIn),
                default).ConfigureAwait(true);
            this._isActive = active;
            this._configuration = cfg ?? new UltimateAutoPlayConfiguration();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Ultimate Autoplay settings.");
            this._configuration = new UltimateAutoPlayConfiguration();
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
            var id = typeof(UltimateAutoPlayPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<UltimateAutoPlayConfiguration>(
                this.PersistenceContextProvider,
                typeof(UltimateAutoPlayPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Ultimate Autoplay settings.", "Đã lưu cài đặt Autoplay Toàn Diện."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Ultimate Autoplay settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
