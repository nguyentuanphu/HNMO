// <copyright file="GameplayRules.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin panel configuration page for per-map rules (14.1), bonus events (14.2), and combat limits (14.3).
/// </summary>
public partial class GameplayRules : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private MapRulesConfiguration _mapConfig = new();
    private BonusEventsConfiguration _bonusConfig = new();
    private CombatLimitsConfiguration _combatConfig = new();
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
    private ILogger<GameplayRules> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string SydneyTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SydneyZone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static T CloneConfig<T>(T value)
        where T : new() =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value)) ?? new T();

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

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, _, mapSaved) = await PlugInSettings.ReadStoredAsync<MapRulesConfiguration>(this.PersistenceContextProvider, typeof(MapRulesPlugIn), default).ConfigureAwait(true);
            var (_, _, bonusSaved) = await PlugInSettings.ReadStoredAsync<BonusEventsConfiguration>(this.PersistenceContextProvider, typeof(BonusEventsPlugIn), default).ConfigureAwait(true);
            var (_, _, combatSaved) = await PlugInSettings.ReadStoredAsync<CombatLimitsConfiguration>(this.PersistenceContextProvider, typeof(CombatLimitsPlugIn), default).ConfigureAwait(true);

            this._mapConfig = CloneConfig(mapSaved ?? new MapRulesConfiguration());
            this._bonusConfig = CloneConfig(bonusSaved ?? new BonusEventsConfiguration());
            this._combatConfig = CloneConfig(combatSaved ?? new CombatLimitsConfiguration());
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading gameplay rules.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void AddMapRule() => this._mapConfig.Rules.Add(new MapRuleEntry { MapNumber = 0, MapName = "Lorencia" });

    private void AddBonusEvent() => this._bonusConfig.Events.Add(new BonusEventEntry { StartTimeSydney = "20:00", DurationMinutes = 60, ExpBonusPercent = 50 });

    private Task SaveMapRulesAsync() =>
        this.SavePlugInAsync(typeof(MapRulesPlugIn).GUID, this._mapConfig, L("Saved per-map rules.", "Đã lưu luật bản đồ."));

    private Task SaveBonusEventsAsync() =>
        this.SavePlugInAsync(typeof(BonusEventsPlugIn).GUID, this._bonusConfig, L("Saved bonus event schedule.", "Đã lưu lịch Giờ vàng."));

    private Task SaveCombatLimitsAsync() =>
        this.SavePlugInAsync(typeof(CombatLimitsPlugIn).GUID, this._combatConfig, L("Saved combat damage limits.", "Đã lưu giới hạn chiến đấu."));

    private async Task SavePlugInAsync<T>(Guid plugInTypeId, T cfg, string successMessage)
        where T : notnull
    {
        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(plugInTypeId, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(plugInTypeId, cfg, json, true, token, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(successMessage);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed saving gameplay rules configuration.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
