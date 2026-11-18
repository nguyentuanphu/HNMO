// <copyright file="BannedWords.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin configuration page for <see cref="BannedWordsPlugIn"/>.
/// </summary>
public partial class BannedWords : ComponentBase
{
    private BannedWordsConfiguration? _configuration;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;
    private string _englishWordsText = string.Empty;
    private string _vietnameseWordsText = string.Empty;
    private string _previewSample = string.Empty;
    private bool _previewAsName;
    private BannedWordsMatchResult? _previewResult;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<BannedWords> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void ResetEnglishDefaults()
    {
        this._englishWordsText = string.Join(Environment.NewLine, BannedWordsConfiguration.DefaultEnglishWords);
        this.RunPreview();
    }

    private void ResetVietnameseDefaults()
    {
        this._vietnameseWordsText = string.Join(Environment.NewLine, BannedWordsConfiguration.DefaultVietnameseWords);
        this.RunPreview();
    }

    private void RunPreview()
    {
        if (this._configuration is null || string.IsNullOrWhiteSpace(this._previewSample))
        {
            this._previewResult = null;
            return;
        }

        var draft = this.BuildDraftConfiguration();
        this._previewResult = this._previewAsName
            ? BannedWordsEngine.EvaluateName(this._previewSample, draft)
            : BannedWordsEngine.EvaluateChat(this._previewSample, draft);
    }

    private BannedWordsConfiguration BuildDraftConfiguration()
    {
        var baseCfg = this._configuration ?? new BannedWordsConfiguration();
        return new BannedWordsConfiguration
        {
            EnglishWords = BannedWordsEngine.ParseWordList(this._englishWordsText),
            VietnameseWords = BannedWordsEngine.ParseWordList(this._vietnameseWordsText),
            ChatAction = baseCfg.ChatAction,
            FilterChat = baseCfg.FilterChat,
            FilterCharacterNames = baseCfg.FilterCharacterNames,
            FilterGuildNames = baseCfg.FilterGuildNames,
            IgnoreVietnameseDiacritics = baseCfg.IgnoreVietnameseDiacritics,
            CheckNameSubstrings = baseCfg.CheckNameSubstrings,
            ExemptGameMasters = baseCfg.ExemptGameMasters,
        };
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<BannedWordsConfiguration>(
                this.PersistenceContextProvider,
                typeof(BannedWordsPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new BannedWordsConfiguration();
            cfg = JsonSerializer.Deserialize<BannedWordsConfiguration>(JsonSerializer.Serialize(cfg)) ?? new BannedWordsConfiguration();
            this._configuration = cfg;

            this._englishWordsText = string.Join(Environment.NewLine, cfg.EnglishWords ?? []);
            this._vietnameseWordsText = string.Join(Environment.NewLine, cfg.VietnameseWords ?? []);
            this._previewResult = null;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Banned Words configuration.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            var cfg = this.BuildDraftConfiguration();
            this._configuration = cfg;

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(BannedWordsPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<BannedWordsConfiguration>(
                this.PersistenceContextProvider,
                typeof(BannedWordsPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.ToastService.ShowSuccess(this.L("Saved Banned Words settings.", "Đã lưu cài đặt bộ lọc từ cấm."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Banned Words settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
