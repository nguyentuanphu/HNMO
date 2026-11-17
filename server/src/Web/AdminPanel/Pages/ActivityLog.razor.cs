// <copyright file="ActivityLog.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin panel viewer for <see cref="ActivityLogPlugIn"/> (ROADMAP 14.9).
/// </summary>
public partial class ActivityLog : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private ActivityLogConfiguration? _configuration;
    private IReadOnlyList<ActivityLogEntry> _entries = [];
    private string _accountFilter = string.Empty;
    private string _characterFilter = string.Empty;
    private string _itemFilter = string.Empty;
    private string _kindFilter = string.Empty;
    private string _fromSydneyText = string.Empty;
    private string _toSydneyText = string.Empty;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;

    /// <summary>Gets or sets optional account query parameter from URL (e.g., linked from Economy Dashboard).</summary>
    [SupplyParameterFromQuery(Name = "account")]
    public string? QueryAccount { get; set; }

    /// <summary>Gets or sets optional item query parameter from URL.</summary>
    [SupplyParameterFromQuery(Name = "item")]
    public string? QueryItem { get; set; }

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<ActivityLog> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrWhiteSpace(this.QueryAccount))
        {
            this._accountFilter = this.QueryAccount.Trim();
        }

        if (!string.IsNullOrWhiteSpace(this.QueryItem))
        {
            this._itemFilter = this.QueryItem.Trim();
        }

        await this.LoadAsync().ConfigureAwait(true);
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string SydneyTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SydneyZone)
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string KindLabel(ActivityKind kind) => kind switch
    {
        ActivityKind.Trade => L("Trade", "Giao dịch"),
        ActivityKind.Drop => L("Drop", "Rớt đồ"),
        ActivityKind.Pickup => L("Pickup", "Nhặt đồ"),
        ActivityKind.ChaosMix => L("Chaos mix", "Xoay Chaos"),
        ActivityKind.NpcBuy => L("NPC buy", "Mua NPC"),
        ActivityKind.NpcSell => L("NPC sell", "Bán NPC"),
        ActivityKind.PersonalStore => L("Personal store", "Cửa hàng cá nhân"),
        ActivityKind.ItemMarket => L("Item market", "Chợ đồ"),
        _ => kind.ToString(),
    };

    private static string FormatActor(string account, string character)
    {
        if (string.IsNullOrEmpty(account) && string.IsNullOrEmpty(character))
        {
            return "-";
        }

        return string.IsNullOrEmpty(account)
            ? character
            : string.IsNullOrEmpty(character)
                ? account
                : $"{account} ({character})";
    }

    private static DateTime? ParseSydneyInput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string[] formats =
        [
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-dd",
        ];

        if (!DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, SydneyZone);
    }

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
            var (_, _, saved) = await PlugInSettings.ReadStoredAsync<ActivityLogConfiguration>(
                this.PersistenceContextProvider,
                typeof(ActivityLogPlugIn),
                default).ConfigureAwait(true);

            var cfg = saved ?? new ActivityLogConfiguration();
            this._configuration = JsonSerializer.Deserialize<ActivityLogConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new ActivityLogConfiguration();
            this.ApplyFilter();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Activity Log settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        ActivityKind? parsedKind = Enum.TryParse<ActivityKind>(this._kindFilter, out var k) ? k : null;
        var fromUtc = ParseSydneyInput(this._fromSydneyText);
        var toUtc = ParseSydneyInput(this._toSydneyText);
        if (toUtc is { } upper && this._toSydneyText.Trim().Length == 10)
        {
            // Whole-day date-only filter should include up to 23:59:59 of that Sydney day.
            toUtc = upper.AddDays(1).AddSeconds(-1);
        }

        this._entries = ActivityLogStore.Open(cfg.FileName).Query(new ActivityLogQuery
        {
            Account = this._accountFilter,
            Character = this._characterFilter,
            Item = this._itemFilter,
            Kind = parsedKind,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            MaxResults = 250,
        });
    }

    private void ClearFilter()
    {
        this._accountFilter = string.Empty;
        this._characterFilter = string.Empty;
        this._itemFilter = string.Empty;
        this._kindFilter = string.Empty;
        this._fromSydneyText = string.Empty;
        this._toSydneyText = string.Empty;
        this.ApplyFilter();
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
            var id = typeof(ActivityLogPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L("Saved activity log settings.", "Đã lưu cài đặt nhật ký."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Activity Log settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
