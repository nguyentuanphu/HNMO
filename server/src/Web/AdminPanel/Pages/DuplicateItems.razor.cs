// <copyright file="DuplicateItems.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.GameLogic.PlugIns.DuplicateItems;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

/// <summary>
/// The duplicate items the hourly scan found (<see cref="DuplicateItemsPlugIn"/>): frozen until the administrator
/// decides here.
/// </summary>
public partial class DuplicateItems : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();

    private readonly Dictionary<Guid, string> _notes = [];
    private DuplicateItemsData _data = new();
    private DuplicateItemsConfiguration _configuration = new();
    private bool _isLoading = true;
    private bool _isScanning;
    private bool _hasDatabase;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<DuplicateItems> Logger { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string SydneyTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SydneyZone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string PlaceLabel(string place) => place switch
    {
        "inventory" => L("bag", "túi đồ"),
        "vault" => L("vault", "rương"),
        _ => L("held (market, escrow or another vault page)", "đang giữ (chợ, ký gửi hoặc trang rương khác)"),
    };

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
            var (_, _, saved) = await PlugInSettings.ReadStoredAsync<DuplicateItemsConfiguration>(this.PersistenceContextProvider, typeof(DuplicateItemsPlugIn), default).ConfigureAwait(true);
            this._configuration = saved ?? new DuplicateItemsConfiguration();
            this._data = DuplicateItemsStore.Open(this._configuration.FileName).Read(d => d);
            this._hasDatabase = ItemSerials.Database is not null;
            foreach (var c in this._data.Cases.Where(c => !c.Resolved))
            {
                this._notes.TryAdd(c.Serial, string.Empty);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "The duplicate items couldn't be read.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task ScanNowAsync()
    {
        if (ItemSerials.Database is not { } database)
        {
            return;
        }

        this._isScanning = true;
        try
        {
            var fileName = this._configuration.FileName;
            var added = await Task.Run(() => DuplicateItemsPlugIn.Scan(database, fileName, this.Logger)).ConfigureAwait(true);
            this.ToastService.ShowSuccess(L($"Scan done: {added.Count} new case(s).", $"Đã quét xong: {added.Count} vụ mới."));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "The duplicate scan failed.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isScanning = false;
        }

        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task ResolveAsync(Guid serial)
    {
        var by = this.AuthenticationState is { } state ? (await state.ConfigureAwait(true)).User.Identity?.Name ?? "admin" : "admin";
        var note = this._notes.TryGetValue(serial, out var text) ? text : string.Empty;
        var decided = DuplicateItemsStore.Open(this._configuration.FileName).Mutate(data =>
        {
            var ok = DuplicateItemsEngine.Resolve(data, serial, by, note, DateTime.UtcNow);
            return (ok, ok);
        });
        DuplicateItemsPlugIn.Refresh(this._configuration.FileName, this._configuration.FreezeDuplicates);
        if (decided)
        {
            this.ToastService.ShowSuccess(L("Decided: the items aren't frozen any more.", "Đã quyết định: các món hết bị đóng băng."));
        }

        await this.LoadAsync().ConfigureAwait(true);
    }
}
