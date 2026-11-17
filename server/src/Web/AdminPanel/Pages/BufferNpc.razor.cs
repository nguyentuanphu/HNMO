// <copyright file="BufferNpc.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.BufferNpc;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin configuration page for <see cref="BufferNpcPlugIn"/>.
/// </summary>
public partial class BufferNpc : ComponentBase
{
    private readonly HashSet<short> _selectedEffects = [];
    private BufferNpcConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;
    private string _npcIdsText = "257";
    private string _extraEffectsText = string.Empty;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<BufferNpc> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void TogglePreset(short effectId, bool enabled)
    {
        if (enabled)
        {
            this._selectedEffects.Add(effectId);
        }
        else
        {
            this._selectedEffects.Remove(effectId);
        }
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<BufferNpcConfiguration>(
                this.PersistenceContextProvider,
                typeof(BufferNpcPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new BufferNpcConfiguration();
            cfg = JsonSerializer.Deserialize<BufferNpcConfiguration>(JsonSerializer.Serialize(cfg)) ?? new BufferNpcConfiguration();
            this._configuration = cfg;

            this._npcIdsText = cfg.NpcNumbers is { Count: > 0 }
                ? string.Join(", ", cfg.NpcNumbers)
                : "257";

            var presetIds = BufferNpcConfiguration.Presets.Select(p => p.EffectNumber).ToHashSet();
            this._selectedEffects.Clear();
            var extras = new List<short>();
            foreach (var id in cfg.BuffEffectNumbers ?? [])
            {
                if (presetIds.Contains(id))
                {
                    this._selectedEffects.Add(id);
                }
                else if (id > 0 && !extras.Contains(id))
                {
                    extras.Add(id);
                }
            }

            this._extraEffectsText = extras.Count > 0 ? string.Join(", ", extras) : string.Empty;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Buffer NPC configuration.");
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
            cfg.MaxLevel = Math.Clamp(cfg.MaxLevel, 1, 1000);
            cfg.MaxResets = Math.Clamp(cfg.MaxResets, -1, 10000);
            cfg.DurationSeconds = Math.Clamp(cfg.DurationSeconds, 0, 86400);
            cfg.ZenCost = Math.Max(0, cfg.ZenCost);

            var npcs = BufferNpcEngine.ParseShortIdList(this._npcIdsText);
            cfg.NpcNumbers = npcs.Count > 0 ? npcs : [257];

            var combined = new List<short>();
            foreach (var preset in BufferNpcConfiguration.Presets)
            {
                if (this._selectedEffects.Contains(preset.EffectNumber))
                {
                    combined.Add(preset.EffectNumber);
                }
            }

            foreach (var extra in BufferNpcEngine.ParseShortIdList(this._extraEffectsText))
            {
                if (!combined.Contains(extra))
                {
                    combined.Add(extra);
                }
            }

            cfg.BuffEffectNumbers = combined;

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(BufferNpcPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<BufferNpcConfiguration>(this.PersistenceContextProvider, typeof(BufferNpcPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Saved Buffer NPC settings.", "Đã lưu cài đặt NPC Buffer."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Buffer NPC settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
