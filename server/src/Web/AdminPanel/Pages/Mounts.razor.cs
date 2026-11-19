// <copyright file="Mounts.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.MountChain;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration and collection summary page for <see cref="MountChainPlugIn"/>.
/// </summary>
public partial class Mounts : ComponentBase
{
    private MountChainConfiguration? _configuration;
    private int _collectorsCount;
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
    private ILogger<Mounts> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private void AddTier()
    {
        this._configuration?.Tiers.Add(new MountTierDefinition
        {
            Id = $"tier-{this._configuration.Tiers.Count + 1}",
            NameEn = "Custom Mount",
            NameVi = "Thú cưỡi mới",
        });
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<MountChainConfiguration>(
                this.PersistenceContextProvider,
                typeof(MountChainPlugIn),
                default).ConfigureAwait(true);

            this._isActive = isActive;
            var cfg = saved ?? new MountChainConfiguration();
            cfg = JsonSerializer.Deserialize<MountChainConfiguration>(JsonSerializer.Serialize(cfg)) ?? new MountChainConfiguration();
            this._configuration = cfg;

            var store = MountChainStore.Open(cfg.FileName);
            this._collectorsCount = store.Read(d => d.Accounts.Count(kv => kv.Value.Unlocked.Count > 0));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Mount Chain.");
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

        cfg.Tiers = cfg.Tiers.Where(t => !string.IsNullOrWhiteSpace(t.Id)).ToList();

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(MountChainPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<MountChainConfiguration>(this.PersistenceContextProvider, typeof(MountChainPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Mount Chain.", "Đã lưu Chuỗi thú cưỡi."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Mount Chain.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
