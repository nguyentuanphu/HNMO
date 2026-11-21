// <copyright file="Market.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.ItemMarket;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration, active listing moderation, and audit log page for <see cref="MarketPlugIn"/>.
/// </summary>
public partial class Market : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private MarketConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private string _bannedText = string.Empty;
    private List<MarketListing> _listings = [];
    private List<MarketAuditEntry> _audit = [];

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<Market> Logger { get; set; } = null!;

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

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<MarketConfiguration>(
                this.PersistenceContextProvider,
                typeof(MarketPlugIn),
                default).ConfigureAwait(true);

            this._isActive = isActive;
            var configuration = saved ?? new MarketConfiguration();
            configuration = JsonSerializer.Deserialize<MarketConfiguration>(JsonSerializer.Serialize(configuration)) ?? new MarketConfiguration();
            this._configuration = configuration;
            this._bannedText = string.Join(Environment.NewLine, configuration.BannedItems);

            var store = MarketStore.Open(configuration.FileName);
            var snapshot = store.Read(d => (d.Listings.ToList(), d.AuditLog.Take(100).ToList()));
            this._listings = snapshot.Item1;
            this._audit = snapshot.Item2;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Item Market settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task CancelListingAsync(string listingId)
    {
        if (this._configuration is not { } c)
        {
            return;
        }

        var engine = new MarketEngine(c, MarketStore.Open(c.FileName), enabled: true);
        if (engine.CancelListing("admin", listingId, isAdmin: true, "admin", DateTime.UtcNow))
        {
            this.ToastService.ShowSuccess(L("Listing canceled and item queued for seller claim.", "Đã hủy tin đăng và chuyển đồ về Kho chợ của người bán."));
            await this.LoadAsync().ConfigureAwait(true);
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        configuration.BannedItems = this._bannedText
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Contains(':'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(MarketPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<MarketConfiguration>(this.PersistenceContextProvider, typeof(MarketPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Item Market settings.", "Đã lưu cài đặt Chợ vật phẩm."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Item Market settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
