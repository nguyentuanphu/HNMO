// <copyright file="VaultChaosRewards.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin panel page for Chaos Machine rates (14.4), Multi-vault (14.5), and Event rewards per level (14.7).
/// </summary>
public partial class VaultChaosRewards : ComponentBase
{
    private ChaosRatesConfiguration _chaosConfig = new();
    private MultiVaultConfiguration _vaultConfig = new();
    private IReadOnlyList<(byte Number, string Name)> _recipes = [];
    private IReadOnlyList<(Guid Id, MiniGameType Type, byte Level, string Name)> _definitions = [];
    private List<MiniGameRewardEntryDto> _allRewards = [];
    private List<MiniGameRewardEntryDto> _editableRewards = [];
    private IReadOnlyList<(Guid Id, string Label)> _dropGroups = [];
    private Guid _selectedDefinitionId;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private EventRewardsOperations RewardsOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<VaultChaosRewards> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static T Clone<T>(T value)
        where T : new() =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value)) ?? new T();

    private string LookupRecipeName(byte num) =>
        this._recipes.FirstOrDefault(r => r.Number == num).Name ?? $"Recipe #{num}";

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, _, chaosSaved) = await PlugInSettings.ReadStoredAsync<ChaosRatesConfiguration>(this.PersistenceContextProvider, typeof(ChaosRatesPlugIn), default).ConfigureAwait(true);
            var (_, _, vaultSaved) = await PlugInSettings.ReadStoredAsync<MultiVaultConfiguration>(this.PersistenceContextProvider, typeof(MultiVaultPlugIn), default).ConfigureAwait(true);

            this._chaosConfig = Clone(chaosSaved ?? new ChaosRatesConfiguration());
            this._vaultConfig = Clone(vaultSaved ?? new MultiVaultConfiguration());
            this._recipes = await this.RewardsOperations.LoadChaosRecipesAsync(default).ConfigureAwait(true);
            var (defs, rows, groups) = await this.RewardsOperations.LoadRewardsAsync(default).ConfigureAwait(true);
            this._definitions = defs;
            this._allRewards = rows.ToList();
            this._dropGroups = groups;

            if (this._selectedDefinitionId == Guid.Empty && defs.Count > 0)
            {
                this._selectedDefinitionId = defs[0].Id;
            }

            this.RefreshSelectedRewards();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Chaos/Vault/Rewards page.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void OnEventChanged(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var id))
        {
            this._selectedDefinitionId = id;
            this.RefreshSelectedRewards();
        }
    }

    private void RefreshSelectedRewards() =>
        this._editableRewards = this._allRewards.Where(r => r.MiniGameDefinitionId == this._selectedDefinitionId).ToList();

    private void AddChaosOverride()
    {
        var first = this._recipes.FirstOrDefault();
        this._chaosConfig.RecipeOverrides.Add(new ChaosRecipeOverride
        {
            RecipeNumber = first.Name is null ? (byte)1 : first.Number,
            RecipeName = first.Name ?? "Chaos Mix",
            RateMultiplier = 1.0,
        });
    }

    private void AddRewardRow()
    {
        var def = this._definitions.FirstOrDefault(d => d.Id == this._selectedDefinitionId);
        this._editableRewards.Add(new MiniGameRewardEntryDto
        {
            MiniGameDefinitionId = this._selectedDefinitionId,
            Type = def.Type,
            GameLevel = def.Level,
            EventName = def.Name ?? string.Empty,
            RewardType = MiniGameRewardType.Money,
            RewardAmount = 100000,
            RequiredSuccess = MiniGameSuccessFlags.Winner,
        });
    }

    private Task SaveChaosAsync() =>
        this.SavePlugInAsync(typeof(ChaosRatesPlugIn).GUID, this._chaosConfig, L("Saved Chaos Machine rates.", "Đã lưu tỉ lệ Máy Chaos."));

    private Task SaveVaultAsync() =>
        this.SavePlugInAsync(typeof(MultiVaultPlugIn).GUID, this._vaultConfig, L("Saved multi-vault settings.", "Đã lưu cài đặt nhiều rương."));

    private async Task SaveEventRewardsAsync()
    {
        if (this._selectedDefinitionId == Guid.Empty)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            await this.RewardsOperations.SaveRewardsForDefinitionAsync(this._selectedDefinitionId, this._editableRewards, default).ConfigureAwait(true);
            this.ToastService.ShowSuccess(L("Saved event level rewards.", "Đã lưu phần thưởng cấp sự kiện."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving event rewards.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task SavePlugInAsync<T>(Guid plugInTypeId, T cfg, string successMessage)
        where T : notnull
    {
        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(plugInTypeId, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(plugInTypeId, cfg, json, true, token, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(successMessage);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed saving plugin configuration.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
