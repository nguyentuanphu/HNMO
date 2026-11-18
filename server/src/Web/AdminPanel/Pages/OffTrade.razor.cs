// <copyright file="OffTrade.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.BufferNpc;
using HNMO.GameLogic.PlugIns.OfflineTrade;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration and live monitoring page for <see cref="OfflineTradePlugIn"/> (<c>/offtrade</c>).
/// </summary>
public partial class OffTrade : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyTimeZone();
    private readonly List<ActiveOffTradeRow> _activeShops = [];
    private OfflineTradeConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;
    private string _allowedMapsText = string.Empty;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private IServerProvider ServerProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<OffTrade> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private static TimeZoneInfo ResolveSydneyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    private static string FormatSydney(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SydneyZone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<OfflineTradeConfiguration>(
                this.PersistenceContextProvider,
                typeof(OfflineTradePlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new OfflineTradeConfiguration();
            cfg = JsonSerializer.Deserialize<OfflineTradeConfiguration>(JsonSerializer.Serialize(cfg)) ?? new OfflineTradeConfiguration();
            this._configuration = cfg;
            this._allowedMapsText = cfg.AllowedMapNumbers is { Count: > 0 }
                ? string.Join(", ", cfg.AllowedMapNumbers)
                : string.Empty;
            this.RefreshActiveShops();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Off-Trade configuration.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void RefreshActiveShops()
    {
        this._activeShops.Clear();
        foreach (var ctx in this.ServerProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).Distinct())
        {
            foreach (var offline in ctx.OfflinePlayerManager.OfflinePlayers)
            {
                if (!offline.IsOffTrade)
                {
                    continue;
                }

                this._activeShops.Add(new ActiveOffTradeRow(
                    offline.AccountLoginName ?? string.Empty,
                    offline.SelectedCharacter?.Name ?? string.Empty,
                    offline.SelectedCharacter?.StoreName ?? string.Empty,
                    offline.CurrentMap?.Definition.Name ?? "-",
                    $"{offline.Position.X},{offline.Position.Y}",
                    offline.ShopStorage?.Items.Count() ?? 0,
                    FormatSydney(offline.StartTimestamp),
                    offline.OffTradeExpiresAtUtc is { } exp ? FormatSydney(exp) : this.L("Unlimited", "Không giới hạn"),
                    ctx.OfflinePlayerManager));
            }
        }
    }

    private async Task StopActiveShopAsync(ActiveOffTradeRow row)
    {
        if (string.IsNullOrEmpty(row.LoginName))
        {
            return;
        }

        await row.Manager.StopAsync(row.LoginName).ConfigureAwait(true);
        this.ToastService.ShowSuccess(this.L(
            $"Stopped off-trade shop for {row.CharacterName}.",
            $"Đã dừng treo cửa hàng offline của {row.CharacterName}."));
        this.RefreshActiveShops();
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
            cfg.MaxDurationHours = Math.Clamp(cfg.MaxDurationHours, 0, 720);
            cfg.OpenFeeZen = Math.Max(0, cfg.OpenFeeZen);
            cfg.MinimumLevel = Math.Clamp(cfg.MinimumLevel, 1, 1000);
            cfg.AllowedMapNumbers = BufferNpcEngine.ParseShortIdList(this._allowedMapsText);

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(OfflineTradePlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<OfflineTradeConfiguration>(
                this.PersistenceContextProvider,
                typeof(OfflineTradePlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Saved Off-Trade settings.", "Đã lưu cài đặt Treo cửa hàng Offline."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Off-Trade settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private sealed record ActiveOffTradeRow(
        string LoginName,
        string CharacterName,
        string StoreName,
        string MapName,
        string Coordinates,
        int ItemCount,
        string StartedSydney,
        string ExpiresSydney,
        OfflinePlayerManager Manager);
}
