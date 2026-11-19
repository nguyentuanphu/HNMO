// <copyright file="Marriage.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Marriage;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin page for <see cref="MarriagePlugIn"/> (<c>/marriage</c>).
/// </summary>
public partial class Marriage
{
    private MarriageConfiguration _configuration = new();
    private IReadOnlyList<MarriageCoupleEntry> _couples = [];
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
    private ILogger<Marriage> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
    }

    private static string L(string english, string vietnamese) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? vietnamese : english;

    private static string FormatSydney(DateTime utc)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.Utc;
            }
        }

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentUICulture);
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<MarriageConfiguration>(
                this.PersistenceContextProvider,
                typeof(MarriagePlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new MarriageConfiguration();
            this._configuration = JsonSerializer.Deserialize<MarriageConfiguration>(JsonSerializer.Serialize(cfg)) ?? new MarriageConfiguration();
            this._couples = new MarriageEngine(this._configuration, MarriageStore.Open(this._configuration.FileName), isActive).AllCouples();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Marriage configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        this._configuration.MinimumLevel = Math.Clamp(this._configuration.MinimumLevel, 1, 400);
        this._configuration.CeremonyRadius = Math.Clamp(this._configuration.CeremonyRadius, 2, 60);
        this._isSaving = true;
        try
        {
            var holder = new PlugInConfiguration();
            holder.SetConfiguration(this._configuration, null);
            var json = holder.CustomConfiguration ?? "{}";
            var id = typeof(MarriagePlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, true, token, default).ConfigureAwait(true);
            }

            var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
            if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
            {
                await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L("Saved Marriage settings.", "Đã lưu cài đặt Kết hôn."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Marriage settings.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private void DissolveCouple(string partnerAName)
    {
        var engine = new MarriageEngine(this._configuration, MarriageStore.Open(this._configuration.FileName), true);
        if (engine.Divorce(partnerAName, DateTime.UtcNow, out _) == MarriageActionResult.Success)
        {
            this._couples = engine.AllCouples();
            this.ToastService.ShowSuccess(L("Dissolved marriage.", "Đã hủy kết hôn."));
        }
    }
}
