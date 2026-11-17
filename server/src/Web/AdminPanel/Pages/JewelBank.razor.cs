// <copyright file="JewelBank.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin configuration and activity log page for <see cref="JewelBankPlugIn"/>.
/// </summary>
public partial class JewelBank : ComponentBase
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private JewelBankConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private List<JewelBankLogEntry> _log = [];

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<JewelBank> Logger { get; set; } = null!;

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

    private void AddJewel()
    {
        this._configuration?.Jewels.Add(new JewelBankEntryConfig
        {
            Key = $"jewel{this._configuration.Jewels.Count + 1}",
            NameEn = "Custom Jewel",
            NameVi = "Ngọc mới",
            Group = 14,
            Number = 43,
            Enabled = true,
            MaxCapacity = 10_000,
        });
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<JewelBankConfiguration>(
                this.PersistenceContextProvider,
                typeof(JewelBankPlugIn),
                default).ConfigureAwait(true);

            this._isActive = isActive;
            var cfg = saved ?? new JewelBankConfiguration();
            cfg = JsonSerializer.Deserialize<JewelBankConfiguration>(JsonSerializer.Serialize(cfg)) ?? new JewelBankConfiguration();
            this._configuration = cfg;

            var store = JewelBankStore.Open(cfg.FileName);
            this._log = store.Read(d => d.Log.Take(100).ToList());
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Jewel Bank settings.");
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

        cfg.Jewels = cfg.Jewels.Where(j => !string.IsNullOrWhiteSpace(j.Key)).ToList();
        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(JewelBankPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<JewelBankConfiguration>(this.PersistenceContextProvider, typeof(JewelBankPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(L("Saved Jewel Bank settings.", "Đã lưu cài đặt Ngân hàng ngọc."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Jewel Bank settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
