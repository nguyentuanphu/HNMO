// <copyright file="LuckyNumber.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns.LuckyNumber;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin panel configuration page for <see cref="LuckyNumberPlugIn"/> (<c>/lucky-number</c>).
/// </summary>
public partial class LuckyNumber
{
    private LuckyNumberConfiguration _configuration = new();
    private List<LuckyNumberRound> _rounds = [];
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isActive;
    private bool _showHelp;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<LuckyNumber> Logger { get; set; } = null!;

    private static string L(string english, string vietnamese) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? vietnamese : english;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
    }

    private double ExactOddsPercent() =>
        new LuckyNumberEngine(this._configuration, LuckyNumberStore.Open(null), true).ComputeOddsPercent().ExactPercent;

    private double LastDigitOddsPercent() =>
        new LuckyNumberEngine(this._configuration, LuckyNumberStore.Open(null), true).ComputeOddsPercent().LastDigitPercent;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<LuckyNumberConfiguration>(
                this.PersistenceContextProvider,
                typeof(LuckyNumberPlugIn),
                default).ConfigureAwait(true);
            this._isActive = isActive;
            var cfg = saved ?? new LuckyNumberConfiguration();
            this._configuration = JsonSerializer.Deserialize<LuckyNumberConfiguration>(JsonSerializer.Serialize(cfg)) ?? new LuckyNumberConfiguration();
            this._rounds = LuckyNumberStore.Open(this._configuration.FileName)
                .Read(d => d.Rounds.OrderByDescending(r => r.DateKey, StringComparer.Ordinal).Take(14).ToList());
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Daily Lucky Number configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        this._configuration.MinNumber = Math.Clamp(this._configuration.MinNumber, 0, 9998);
        this._configuration.MaxNumber = Math.Clamp(this._configuration.MaxNumber, this._configuration.MinNumber + 1, 9999);
        this._configuration.MaxPicksPerAccountPerDay = Math.Clamp(this._configuration.MaxPicksPerAccountPerDay, 1, 20);
        this._isSaving = true;
        try
        {
            var holder = new PlugInConfiguration();
            holder.SetConfiguration(this._configuration, null);
            var json = holder.CustomConfiguration ?? "{}";
            var id = typeof(LuckyNumberPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, true, token, default).ConfigureAwait(true);
            }

            var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
            if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
            {
                await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L("Saved Daily Lucky Number settings.", "Đã lưu cài đặt Con số may mắn."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Daily Lucky Number settings.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task DrawNowAsync()
    {
        var engine = new LuckyNumberEngine(this._configuration, LuckyNumberStore.Open(this._configuration.FileName), this._isActive);
        var outcome = engine.DrawNow(DateTime.UtcNow);
        if (outcome is null)
        {
            this.ToastService.ShowInfo(L("Today's round is already drawn or disabled.", "Kỳ hôm nay đã quay rồi hoặc đang tắt."));
        }
        else
        {
            this.ToastService.ShowSuccess(L($"Drew number {outcome.DrawnNumber:00} ({outcome.Winners.Count} winners)!", $"Đã quay số {outcome.DrawnNumber:00} ({outcome.Winners.Count} người thắng)!"));
        }

        await this.LoadAsync().ConfigureAwait(true);
    }
}
