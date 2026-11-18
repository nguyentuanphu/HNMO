// <copyright file="TeleportNpc.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.TeleportNpc;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="TeleportNpcPlugIn"/>.
/// </summary>
public partial class TeleportNpc : ComponentBase
{
    private TeleportNpcConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;
    private string _npcNumbersText = "247, 540";

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<TeleportNpc> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private static List<short> ParseNpcNumbers(string raw)
    {
        var list = new List<short>();
        foreach (var part in (raw ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (short.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) && num > 0 && !list.Contains(num))
            {
                list.Add(num);
            }
        }

        return list;
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void AddDestination()
    {
        if (this._configuration is null)
        {
            return;
        }

        var next = this._configuration.Destinations.Count + 1;
        this._configuration.Destinations.Add(new TeleportDestinationConfig
        {
            Id = $"dest_{next}",
            Enabled = true,
            NameEn = $"Destination {next}",
            NameVi = $"Điểm đến {next}",
            MapNumber = 0,
            TargetX = 130,
            TargetY = 127,
            MinLevel = 1,
            ZenCost = 1000,
        });
    }

    private void RemoveDestination(int index)
    {
        if (this._configuration is null || index < 0 || index >= this._configuration.Destinations.Count)
        {
            return;
        }

        this._configuration.Destinations.RemoveAt(index);
    }

    private void RestoreDefaults()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._configuration.Destinations = TeleportNpcConfiguration.CreateDefaultDestinations();
        this.ToastService.ShowSuccess(this.L("Restored starter destinations (click Save to apply).", "Đã khôi phục danh sách mẫu (bấm Lưu để áp dụng)."));
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<TeleportNpcConfiguration>(
                this.PersistenceContextProvider,
                typeof(TeleportNpcPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new TeleportNpcConfiguration();
            this._configuration = JsonSerializer.Deserialize<TeleportNpcConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new TeleportNpcConfiguration();
            this._npcNumbersText = string.Join(", ", this._configuration.NpcNumbers ?? []);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Teleport NPC configuration.");
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

        this._isSaving = true;
        try
        {
            cfg.Enabled = this._isActive;
            cfg.NpcNumbers = ParseNpcNumbers(this._npcNumbersText);
            foreach (var d in cfg.Destinations)
            {
                d.Id = (d.Id ?? string.Empty).Trim();
                d.NameEn = (d.NameEn ?? string.Empty).Trim();
                d.NameVi = (d.NameVi ?? string.Empty).Trim();
                d.MinLevel = Math.Max(0, d.MinLevel);
                d.MaxLevel = Math.Max(0, d.MaxLevel);
                d.MinResets = Math.Max(0, d.MinResets);
                d.ZenCost = Math.Max(0, d.ZenCost);
            }

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(TeleportNpcPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<TeleportNpcConfiguration>(
                this.PersistenceContextProvider,
                typeof(TeleportNpcPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Teleport NPC settings saved.", "Đã lưu cài đặt NPC Dịch Chuyển."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Teleport NPC settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
