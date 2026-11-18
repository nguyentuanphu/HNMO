// <copyright file="EconomyDashboard.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin panel Economy Dashboard page (ROADMAP 14.25).
/// </summary>
public partial class EconomyDashboard : ComponentBase
{
    private EconomyConfiguration? _configuration;
    private IReadOnlyList<EconomyDailySnapshot> _snapshots = [];
    private IReadOnlyList<EscrowFeeLedgerEntry> _escrowFeeLedger = [];
    private IReadOnlyList<(string Currency, long TotalFee, int Count)> _escrowFeeTotals = [];
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private EconomyDashboardOperations EconomyOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<EconomyDashboard> Logger { get; set; } = null!;

    private EconomyDailySnapshot? Latest => this._snapshots.Count > 0 ? this._snapshots[^1] : null;

    private IReadOnlyList<EconomyAnomaly> AllAnomalies =>
        this._snapshots.SelectMany(s => s.Anomalies).OrderByDescending(a => a.DateSydney, StringComparer.Ordinal).ToList();

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, _, saved) = await PlugInSettings.ReadStoredAsync<EconomyConfiguration>(
                this.PersistenceContextProvider,
                typeof(EconomyDashboardPlugIn),
                default).ConfigureAwait(true);

            var cfg = saved ?? new EconomyConfiguration();
            this._configuration = JsonSerializer.Deserialize<EconomyConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new EconomyConfiguration();

            this._snapshots = await this.EconomyOperations.LoadHistoryAsync(this._configuration.FileName, 30, default).ConfigureAwait(true);

            var (_, _, escrowCfgSaved) = await PlugInSettings.ReadStoredAsync<EscrowConfiguration>(
                this.PersistenceContextProvider,
                typeof(EscrowContractsPlugIn),
                default).ConfigureAwait(true);
            var escrowFile = escrowCfgSaved?.FileName ?? new EscrowConfiguration().FileName;
            this._escrowFeeLedger = EscrowStore.Open(escrowFile).Read(d => d.FeeLedger.Take(20).ToList());
            this._escrowFeeTotals = this._escrowFeeLedger
                .GroupBy(e => e.Currency, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Currency: g.Key, TotalFee: g.Sum(x => x.FeeAmount), Count: g.Count()))
                .OrderByDescending(x => x.TotalFee)
                .ToList();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Economy Dashboard.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task CaptureNowAsync()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            await this.EconomyOperations.CaptureNowAsync(cfg, default).ConfigureAwait(true);
            this._snapshots = await this.EconomyOperations.LoadHistoryAsync(cfg.FileName, 30, default).ConfigureAwait(true);
            this.ToastService.ShowSuccess(L("Captured economy snapshot.", "Đã cập nhật thống kê kinh tế."));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to capture economy snapshot.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
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
            var id = typeof(EconomyDashboardPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L("Saved economy dashboard settings.", "Đã lưu cài đặt bảng kinh tế."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Economy Dashboard settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private string ZenPolyline() => this.BuildPolyline(s => (double)s.TotalZen);

    private string JewelPolyline() => this.BuildPolyline(s => s.TotalJewels);

    private string BuildPolyline(Func<EconomyDailySnapshot, double> selector)
    {
        if (this._snapshots.Count == 0)
        {
            return string.Empty;
        }

        var max = Math.Max(1.0, this._snapshots.Max(selector));
        if (this._snapshots.Count == 1)
        {
            var y = 130.0 - ((selector(this._snapshots[0]) / max) * 110.0);
            return string.Create(CultureInfo.InvariantCulture, $"40,{y:F1} 600,{y:F1}");
        }

        var pts = new List<string>(this._snapshots.Count);
        for (var i = 0; i < this._snapshots.Count; i++)
        {
            var x = 40.0 + ((560.0 * i) / (this._snapshots.Count - 1));
            var y = 130.0 - ((selector(this._snapshots[i]) / max) * 110.0);
            pts.Add(string.Create(CultureInfo.InvariantCulture, $"{x:F1},{y:F1}"));
        }

        return string.Join(' ', pts);
    }

    private IReadOnlyList<(string X, string Text)> ChartLabels()
    {
        if (this._snapshots.Count == 0)
        {
            return [];
        }

        if (this._snapshots.Count == 1)
        {
            return [("320", this._snapshots[0].DateSydney)];
        }

        var labels = new List<(string, string)>();
        var step = Math.Max(1, this._snapshots.Count / 5);
        for (var i = 0; i < this._snapshots.Count; i += step)
        {
            var x = 40.0 + ((560.0 * i) / (this._snapshots.Count - 1));
            labels.Add((x.ToString("F0", CultureInfo.InvariantCulture), this._snapshots[i].DateSydney[5..]));
        }

        return labels;
    }
}
