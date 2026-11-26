// <copyright file="S9Systems.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for Season 9 progression and combat systems (<c>/s9-systems</c>).
/// </summary>
public partial class S9Systems : ComponentBase
{
    private SeasonNineSystemsConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private string _lang = "en";
    private string? _statusMessage;
    private string _muunCostsText = string.Empty;
    private string _muunPetsJson = string.Empty;
    private string _muunOptionsJson = string.Empty;
    private bool _statusIsError;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<S9Systems> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        this._lang = iso switch
        {
            "vi" => "vi",
            "de" => "de",
            "zh" => "zh-CN",
            _ => "en",
        };
        await this.LoadAsync().ConfigureAwait(true);
    }

    private void SetLanguage(string lang) => this._lang = lang;

    private string T(string en, string vi, string de, string zh) => this._lang switch
    {
        "vi" => vi,
        "de" => de,
        "zh-CN" => zh,
        _ => en,
    };

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<SeasonNineSystemsConfiguration>(
                this.PersistenceContextProvider,
                typeof(SeasonNineSystemsPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new SeasonNineSystemsConfiguration();
            this._configuration = JsonSerializer.Deserialize<SeasonNineSystemsConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new SeasonNineSystemsConfiguration();

            // The Muun tables are shown as they apply: the saved ones, or the built-in ones (which wait for the pack).
            var indented = new JsonSerializerOptions { WriteIndented = true };
            this._muunCostsText = string.Join(", ", this._configuration.MuunLevelUpEnergyCosts ?? []);
            this._muunPetsJson = JsonSerializer.Serialize(this._configuration.MuunPets is { Count: > 0 } pets ? pets : SeasonNineSystemsCatalog.Muuns, indented);
            this._muunOptionsJson = JsonSerializer.Serialize(this._configuration.MuunOptions is { Count: > 0 } options ? options : SeasonNineSystemsCatalog.MuunOptions, indented);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Season 9 systems settings.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    /// <summary>
    /// Reads the Muun cost list and the two Muun tables of the page into the configuration; a table equal to the built-in
    /// one is saved as none, so the built-in values (and their later fixes) keep applying.
    /// </summary>
    /// <param name="cfg">The configuration to save.</param>
    /// <returns><c>false</c> (with a message) when a field can't be read.</returns>
    private bool TryReadMuunTables(SeasonNineSystemsConfiguration cfg)
    {
        try
        {
            var costs = new List<int>();
            foreach (var part in this._muunCostsText.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                costs.Add(Math.Max(0, int.Parse(part, CultureInfo.InvariantCulture)));
            }

            cfg.MuunLevelUpEnergyCosts = costs;
            var pets = JsonSerializer.Deserialize<List<SeasonNineSystemsCatalog.MuunSpec>>(this._muunPetsJson) ?? [];
            var options = JsonSerializer.Deserialize<List<SeasonNineSystemsCatalog.MuunOptionSpec>>(this._muunOptionsJson) ?? [];
            cfg.MuunPets = pets.SequenceEqual(SeasonNineSystemsCatalog.Muuns) ? null : pets;
            cfg.MuunOptions = options.SequenceEqual(SeasonNineSystemsCatalog.MuunOptions) ? null : options;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OverflowException)
        {
            this._statusIsError = true;
            this._statusMessage = this.T(
                $"The Muun tables couldn't be read: {ex.Message}",
                $"Không đọc được bảng Muun: {ex.Message}",
                $"Die Muun-Tabellen konnten nicht gelesen werden: {ex.Message}",
                $"无法读取萌宠表：{ex.Message}");
            this.ToastService.ShowError(this._statusMessage);
            this._isSaving = false;
            return false;
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        this._isSaving = true;
        this._statusMessage = null;
        this._statusIsError = false;
        try
        {
            cfg.MasterLevelMaxCap = (short)Math.Clamp((int)cfg.MasterLevelMaxCap, 200, 400);
            cfg.ShieldBurnSdPercent = Math.Clamp(cfg.ShieldBurnSdPercent, 1, 100);
            cfg.SpellOfProtectionDefensePct = Math.Clamp(cfg.SpellOfProtectionDefensePct, 1, 100);
            if (!this.TryReadMuunTables(cfg))
            {
                return;
            }

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(SeasonNineSystemsPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<SeasonNineSystemsConfiguration>(
                this.PersistenceContextProvider,
                typeof(SeasonNineSystemsPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this._statusMessage = this.T("Season 9 systems settings saved.", "Đã lưu cấu hình hệ thống Season 9.", "Season-9-Systemeinstellungen gespeichert.", "第 9 赛季系统配置已保存。");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Season 9 systems settings.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
