// <copyright file="Timetable.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin page for <see cref="TimetablePlugIn"/> and scheduled invasions (Golden, Red Dragon, White Wizard, Rabbits, Snowmen, Find the GM).
/// </summary>
public partial class Timetable
{
    private TimetableConfiguration _configuration = new();
    private FindTheGmConfiguration? _findGmConfig;
    private readonly List<InvasionRow> _invasions = [];
    private IReadOnlyList<TimetableComputedEntry> _preview = [];
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isActive;
    private bool _showHelp;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<Timetable> Logger { get; set; } = null!;

    private static string L(string english, string vietnamese) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? vietnamese : english;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<TimetableConfiguration>(
                this.PersistenceContextProvider,
                typeof(TimetablePlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new TimetableConfiguration();
            this._configuration = JsonSerializer.Deserialize<TimetableConfiguration>(JsonSerializer.Serialize(cfg)) ?? new TimetableConfiguration();

            this._invasions.Clear();
            await this.LoadInvasionRowAsync(typeof(GoldenInvasionPlugIn), "Golden Invasion", "Quái Vàng", InvasionConfigurationDefaults.Golden, isDisabledByDefault: false).ConfigureAwait(true);
            await this.LoadInvasionRowAsync(typeof(RedDragonInvasionPlugIn), "Red Dragon Invasion", "Rồng Đỏ", InvasionConfigurationDefaults.RedDragon, isDisabledByDefault: false).ConfigureAwait(true);
            await this.LoadInvasionRowAsync(typeof(WhiteWizardInvasionPlugIn), "White Wizard Corps", "Phù Thủy Trắng", InvasionConfigurationDefaults.WhiteWizard, isDisabledByDefault: false).ConfigureAwait(true);
            await this.LoadInvasionRowAsync(typeof(RabbitInvasionPlugIn), "Lunar Rabbit Invasion", "Thỏ Ngọc", InvasionConfigurationDefaults.Rabbits, isDisabledByDefault: true).ConfigureAwait(true);
            await this.LoadInvasionRowAsync(typeof(SnowmanInvasionPlugIn), "Snowman Invasion", "Người Tuyết", InvasionConfigurationDefaults.Snowmen, isDisabledByDefault: true).ConfigureAwait(true);
            await this.LoadFindGmRowAsync().ConfigureAwait(true);

            this.RebuildPreview();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the timetable configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task LoadInvasionRowAsync(
        Type plugInType,
        string nameEn,
        string nameVi,
        PeriodicInvasionConfiguration defaults,
        bool isDisabledByDefault)
    {
        var (exists, active, saved) = await PlugInSettings.ReadStoredAsync<PeriodicInvasionConfiguration>(
            this.PersistenceContextProvider,
            plugInType,
            default).ConfigureAwait(true);
        var cfg = saved ?? defaults;
        this._invasions.Add(new InvasionRow
        {
            PlugInType = plugInType,
            NameEn = nameEn,
            NameVi = nameVi,
            IsActive = exists ? active : !isDisabledByDefault,
            Configuration = cfg,
            TimesText = string.Join(", ", cfg.Timetable.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture))),
            DurationMinutes = Math.Max(1, (int)Math.Round(cfg.TaskDuration.TotalMinutes)),
        });
    }

    private async Task LoadFindGmRowAsync()
    {
        var (exists, active, saved) = await PlugInSettings.ReadStoredAsync<FindTheGmConfiguration>(
            this.PersistenceContextProvider,
            typeof(FindTheGmInvasionPlugIn),
            default).ConfigureAwait(true);
        var cfg = saved ?? new FindTheGmConfiguration();
        this._findGmConfig = cfg;
        this._invasions.Add(new InvasionRow
        {
            PlugInType = typeof(FindTheGmInvasionPlugIn),
            NameEn = "Find the GM",
            NameVi = "Truy tìm GM",
            IsActive = exists && active,
            Configuration = cfg,
            TimesText = string.Join(", ", cfg.Timetable.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture))),
            DurationMinutes = Math.Max(1, (int)Math.Round(cfg.TaskDuration.TotalMinutes)),
            IsFindGm = true,
        });
    }

    private void RebuildPreview()
    {
        var now = DateTime.UtcNow;
        var sources = new List<TimetableSourceItem>();
        foreach (var inv in this._invasions.Where(r => r.IsActive))
        {
            sources.Add(TimetableSourceCollector.FromInvasion(inv.PlugInType, inv.Configuration, null));
        }

        this._preview = TimetableEngine.BuildSnapshot(
            this._configuration,
            this._isActive,
            now,
            sources,
            []).Entries;
    }

    private void AddCustomEntry()
    {
        this._configuration.CustomEntries.Add(new TimetableCustomEntry
        {
            Id = $"custom-{this._configuration.CustomEntries.Count + 1}",
            NameEn = "Special Event",
            NameVi = "Sự kiện đặc biệt",
            MapsEn = "Lorencia",
            MapsVi = "Lorencia",
            DropsEn = "Special rewards",
            DropsVi = "Quà đặc biệt",
            ScheduleTimes = "20:00",
            DurationMinutes = 30,
        });
    }

    private async Task SaveTimetableAsync()
    {
        this._isSaving = true;
        try
        {
            var id = typeof(TimetablePlugIn).GUID;
            await this.SavePlugInConfigAsync(id, this._configuration, this._isActive).ConfigureAwait(true);
            this.RebuildPreview();
            this.ToastService.ShowSuccess(L("Saved timetable settings.", "Đã lưu cài đặt bảng lịch."));
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving timetable configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task SaveInvasionAsync(InvasionRow row)
    {
        var parsedTimes = TimetableEngine.ParseTimes(row.TimesText);
        if (parsedTimes.Count == 0)
        {
            this.ToastService.ShowError(L("Enter at least one valid HH:mm time.", "Nhập ít nhất một giờ HH:mm hợp lệ."));
            return;
        }

        row.Configuration.Timetable = parsedTimes;
        row.Configuration.TaskDuration = TimeSpan.FromMinutes(Math.Clamp(row.DurationMinutes, 1, 180));

        this._isSaving = true;
        try
        {
            object configToSave = row.IsFindGm && this._findGmConfig is not null
                ? this._findGmConfig
                : row.Configuration;
            await this.SavePlugInConfigAsync(row.PlugInType.GUID, configToSave, row.IsActive).ConfigureAwait(true);
            this.RebuildPreview();
            this.ToastService.ShowSuccess(L("Saved invasion schedule.", "Đã lưu lịch xâm lăng."));
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving invasion configuration for {PlugIn}.", row.PlugInType.Name);
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task SavePlugInConfigAsync(Guid plugInId, object configuration, bool isActive)
    {
        var holder = new PlugInConfiguration();
        holder.SetConfiguration(configuration, null);
        var json = holder.CustomConfiguration ?? "{}";
        var preview = await this.PluginOperations.ReplaceFromPanelAsync(plugInId, configuration, json, false, null, default).ConfigureAwait(true);
        if (preview["confirmationToken"]?.GetValue<string>() is { } token)
        {
            await this.PluginOperations.ReplaceFromPanelAsync(plugInId, configuration, json, true, token, default).ConfigureAwait(true);
        }

        var activePreview = await this.PluginOperations.SetActiveAsync(plugInId, isActive, false, null, default).ConfigureAwait(true);
        if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
        {
            await this.PluginOperations.SetActiveAsync(plugInId, isActive, true, activeToken, default).ConfigureAwait(true);
        }
    }

    private string FormatSydney(DateTime? utc)
    {
        if (utc is not { } dt)
        {
            return "—";
        }

        var tz = TimetableEngine.ResolveTimeZone(this._configuration.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Utc), tz);
        return local.ToString("ddd HH:mm", CultureInfo.CurrentUICulture);
    }

    private sealed class InvasionRow
    {
        public required Type PlugInType { get; init; }

        public required string NameEn { get; init; }

        public required string NameVi { get; init; }

        public bool IsActive { get; set; }

        public required PeriodicInvasionConfiguration Configuration { get; set; }

        public string TimesText { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public bool IsFindGm { get; set; }
    }
}
