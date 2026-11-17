// <copyright file="PartyOptions.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.PartyOptions;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin configuration page for <see cref="PartyOptionsPlugIn"/>.
/// </summary>
public partial class PartyOptions : ComponentBase
{
    private PartyOptionsConfiguration? _configuration;
    private IReadOnlyList<PartyAdSnapshot> _ads = [];
    private string _spotsText = string.Empty;
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
    private ILogger<PartyOptions> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<PartyOptionsConfiguration>(
                this.PersistenceContextProvider,
                typeof(PartyOptionsPlugIn),
                default).ConfigureAwait(true);

            this._isActive = isActive;
            var cfg = saved ?? new PartyOptionsConfiguration();
            cfg = JsonSerializer.Deserialize<PartyOptionsConfiguration>(JsonSerializer.Serialize(cfg)) ?? new PartyOptionsConfiguration();
            this._configuration = cfg;
            this._spotsText = string.Join(", ", cfg.HuntingSpots);

            var store = PartyOptionsStore.Open(cfg.FileName);
            this._ads = store.ReadAds();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Party Options.");
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

        cfg.HuntingSpots = (this._spotsText ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(PartyOptionsPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<PartyOptionsConfiguration>(this.PersistenceContextProvider, typeof(PartyOptionsPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Party Options.", "Đã lưu cài đặt Tổ đội."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Party Options.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
