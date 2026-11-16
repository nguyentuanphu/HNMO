// <copyright file="Talismans.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Talismans;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="TalismanPlugIn"/> (bag talismans/figurines and capped stat-point consumables).
/// </summary>
public partial class Talismans : ComponentBase
{
    private TalismanConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<Talismans> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void AddTalisman()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._configuration.Talismans.Add(new TalismanDefinitionConfig
        {
            ItemGroup = 13,
            ItemNumber = 82,
            NameEn = "New Talisman",
            NameVi = "Bùa Mới",
            BonusDamage = 15,
            BonusDefense = 15,
        });
    }

    private void RemoveTalisman(int index)
    {
        if (this._configuration is not null && index >= 0 && index < this._configuration.Talismans.Count)
        {
            this._configuration.Talismans.RemoveAt(index);
        }
    }

    private void AddConsumable()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._configuration.StatConsumables.Add(new StatConsumableDefinitionConfig
        {
            ItemGroup = 14,
            ItemNumber = 92,
            NameEn = "New Stat Scroll",
            NameVi = "Sách Tiềm Năng Mới",
            PointsPerUse = 10,
            TargetBucket = "levelUpPoints",
        });
    }

    private void RemoveConsumable(int index)
    {
        if (this._configuration is not null && index >= 0 && index < this._configuration.StatConsumables.Count)
        {
            this._configuration.StatConsumables.RemoveAt(index);
        }
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<TalismanConfiguration>(
                this.PersistenceContextProvider,
                typeof(TalismanPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new TalismanConfiguration();
            this._configuration = JsonSerializer.Deserialize<TalismanConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new TalismanConfiguration();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Talismans configuration.");
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
            cfg.GlobalStatPointsCap = Math.Clamp(cfg.GlobalStatPointsCap, 1, 1_000_000);
            foreach (var t in cfg.Talismans)
            {
                t.MaxStacks = Math.Clamp(t.MaxStacks, 1, 64);
                t.BonusExpPercent = Math.Max(0, t.BonusExpPercent);
                t.BonusZenPercent = Math.Max(0, t.BonusZenPercent);
                t.BonusDamage = Math.Max(0, t.BonusDamage);
                t.BonusDefense = Math.Max(0, t.BonusDefense);
                t.BonusMaxHp = Math.Max(0, t.BonusMaxHp);
                t.BonusAttackSpeed = Math.Max(0, t.BonusAttackSpeed);
                t.CriticalChancePercent = Math.Max(0, t.CriticalChancePercent);
            }

            foreach (var c in cfg.StatConsumables)
            {
                c.PointsPerUse = Math.Clamp(c.PointsPerUse, 1, 10000);
                c.TargetBucket = TalismanEngine.NormalizeBucket(c.TargetBucket);
            }

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(TalismanPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<TalismanConfiguration>(
                this.PersistenceContextProvider,
                typeof(TalismanPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Saved Talismans & Stat Consumables settings.", "Đã lưu cài đặt Bùa Hộ Thân & Vật Phẩm Cộng Điểm."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Talismans settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
