// <copyright file="Leftovers.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin configuration and recent activity log page for <see cref="CombiningLeftoversPlugIn"/>.
/// </summary>
public partial class Leftovers : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private CombiningLeftoversConfiguration? _configuration;
    private IReadOnlyList<LeftoversLogEntry> _logs = [];
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
    private ILogger<Leftovers> Logger { get; set; } = null!;

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

    private void AddRecipe()
    {
        this._configuration?.Recipes.Add(new LeftoverRecipe
        {
            Id = $"recipe-{this._configuration.Recipes.Count + 1}",
            NameEn = "Custom leftover combine",
            NameVi = "Ghép đồ dư mới",
        });
    }

    private void AddOffer()
    {
        this._configuration?.ExchangeOffers.Add(new LeftoverExchangeOffer
        {
            Id = $"offer-{this._configuration.ExchangeOffers.Count + 1}",
            NameEn = "Custom surplus exchange",
            NameVi = "Gói đổi nguyên liệu mới",
        });
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<CombiningLeftoversConfiguration>(
                this.PersistenceContextProvider,
                typeof(CombiningLeftoversPlugIn),
                default).ConfigureAwait(true);

            this._isActive = isActive;
            var cfg = saved ?? new CombiningLeftoversConfiguration();
            cfg = JsonSerializer.Deserialize<CombiningLeftoversConfiguration>(JsonSerializer.Serialize(cfg)) ?? new CombiningLeftoversConfiguration();
            this._configuration = cfg;

            var store = CombiningLeftoversStore.Open(cfg.FileName);
            this._logs = store.RecentLogs(25);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Combining Leftovers.");
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

        cfg.Recipes = cfg.Recipes.Where(r => !string.IsNullOrWhiteSpace(r.Id)).ToList();
        cfg.ExchangeOffers = cfg.ExchangeOffers.Where(o => !string.IsNullOrWhiteSpace(o.Id)).ToList();

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(CombiningLeftoversPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<CombiningLeftoversConfiguration>(this.PersistenceContextProvider, typeof(CombiningLeftoversPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Combining Leftovers.", "Đã lưu Đổi đồ dư."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Combining Leftovers.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
