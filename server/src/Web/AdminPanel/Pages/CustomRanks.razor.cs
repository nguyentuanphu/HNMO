// <copyright file="CustomRanks.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.CustomRanks;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="CustomRanksPlugIn"/> (custom rank/title tiers and achievement keys).
/// </summary>
public partial class CustomRanks : ComponentBase
{
    private CustomRankConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;
    private IReadOnlyList<CustomRankCharacterRecord> _holders = [];
    private string _grantCharacter = string.Empty;
    private string _grantKey = string.Empty;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<CustomRanks> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void RefreshHolders()
    {
        var store = CustomRankStore.Open(this._configuration?.FileName);
        this._holders = store.ListAchievementHolders();
    }

    private void AddTier()
    {
        if (this._configuration is null)
        {
            return;
        }

        var next = this._configuration.Tiers.Count + 1;
        this._configuration.Tiers.Add(new CustomRankTierConfig
        {
            Id = $"rank_{next}",
            Enabled = true,
            Priority = next * 10,
            TitleEn = $"[Rank {next}]",
            TitleVi = $"[Danh Hiệu {next}]",
            ColorHex = "#ffd166",
        });
    }

    private void RemoveTier(int index)
    {
        if (this._configuration is null || index < 0 || index >= this._configuration.Tiers.Count)
        {
            return;
        }

        this._configuration.Tiers.RemoveAt(index);
    }

    private void RestoreDefaults()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._configuration.Tiers = CustomRankConfiguration.CreateDefaultTiers();
        this.ToastService.ShowSuccess(this.L("Restored starter rank tiers (click Save to apply).", "Đã khôi phục danh hiệu mẫu (bấm Lưu để áp dụng)."));
    }

    private void GrantKey()
    {
        if (string.IsNullOrWhiteSpace(this._grantCharacter) || string.IsNullOrWhiteSpace(this._grantKey))
        {
            this.ToastService.ShowError(this.L("Enter both character name and achievement key.", "Vui lòng nhập tên nhân vật và mã thành tựu."));
            return;
        }

        var store = CustomRankStore.Open(this._configuration?.FileName);
        if (store.GrantAchievement(this._grantCharacter, this._grantKey))
        {
            this.RefreshHolders();
            this.ToastService.ShowSuccess(this.L("Achievement key granted.", "Đã trao mã thành tựu."));
        }
        else
        {
            this.ToastService.ShowError(this.L("Character already holds this achievement key.", "Nhân vật đã có mã thành tựu này."));
        }
    }

    private void RevokeKey()
    {
        if (string.IsNullOrWhiteSpace(this._grantCharacter) || string.IsNullOrWhiteSpace(this._grantKey))
        {
            this.ToastService.ShowError(this.L("Enter both character name and achievement key.", "Vui lòng nhập tên nhân vật và mã thành tựu."));
            return;
        }

        var store = CustomRankStore.Open(this._configuration?.FileName);
        if (store.RevokeAchievement(this._grantCharacter, this._grantKey))
        {
            this.RefreshHolders();
            this.ToastService.ShowSuccess(this.L("Achievement key revoked.", "Đã thu hồi mã thành tựu."));
        }
        else
        {
            this.ToastService.ShowError(this.L("Achievement key was not found on this character.", "Không tìm thấy mã thành tựu trên nhân vật này."));
        }
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<CustomRankConfiguration>(
                this.PersistenceContextProvider,
                typeof(CustomRanksPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new CustomRankConfiguration();
            this._configuration = JsonSerializer.Deserialize<CustomRankConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new CustomRankConfiguration();
            this.RefreshHolders();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Custom Ranks configuration.");
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
            foreach (var t in cfg.Tiers)
            {
                t.Id = (t.Id ?? string.Empty).Trim();
                t.TitleEn = (t.TitleEn ?? string.Empty).Trim();
                t.TitleVi = (t.TitleVi ?? string.Empty).Trim();
                t.ColorHex = string.IsNullOrWhiteSpace(t.ColorHex) ? "#ffd166" : t.ColorHex.Trim();
                t.MinLevel = Math.Max(0, t.MinLevel);
                t.MinMasterLevel = Math.Max(0, t.MinMasterLevel);
                t.MinResets = Math.Max(0, t.MinResets);
                t.MinVipLevel = Math.Max(0, t.MinVipLevel);
                t.MinMissionsClaimed = Math.Max(0, t.MinMissionsClaimed);
                t.RequiredAchievementKey = (t.RequiredAchievementKey ?? string.Empty).Trim();
            }

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(CustomRanksPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<CustomRankConfiguration>(
                this.PersistenceContextProvider,
                typeof(CustomRanksPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Custom rank settings saved.", "Đã lưu cài đặt danh hiệu tùy chỉnh."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Custom Ranks settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
