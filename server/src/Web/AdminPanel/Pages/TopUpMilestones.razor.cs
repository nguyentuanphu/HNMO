// <copyright file="TopUpMilestones.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin page for <see cref="TopUpMilestonesPlugIn"/>: configures VND milestone steps,
/// manual transfer confirmations, and inspects account totals (Sydney time).
/// </summary>
public partial class TopUpMilestones : ComponentBase
{
    private readonly List<string> _errors = [];
    private List<StepDraft> _stepDrafts = [];
    private List<AccountSummaryRow> _accounts = [];
    private TopUpMilestonesConfiguration? _configuration;
    private TopUpMilestoneStore? _store;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private string _manualLogin = string.Empty;
    private long _manualVnd = 100_000;
    private string _manualReference = string.Empty;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<TopUpMilestones> Logger { get; set; } = null!;

    /// <summary>Validates step drafts and builds clean configuration steps.</summary>
    /// <param name="drafts">The editable draft rows.</param>
    /// <param name="steps">The validated configuration steps.</param>
    /// <returns>Validation error messages, if any.</returns>
    internal static List<string> ValidateAndBuildSteps(IEnumerable<StepDraft> drafts, out List<TopUpMilestoneStep> steps)
    {
        var errors = new List<string>();
        steps = [];
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var draft in drafts)
        {
            var id = draft.Id.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(L("Each milestone step needs a non-empty ID.", "Mỗi mốc cần mã không để trống.", "Für jede Aufladestufe ist eine Kennung erforderlich.", "每个里程碑都必须有编号。"));
                continue;
            }

            if (!seenIds.Add(id))
            {
                errors.Add(L($"Duplicate step ID '{id}'.", $"Trùng mã mốc '{id}'.", $"Die Stufenkennung '{id}' ist doppelt vorhanden.", $"等级编号“{id}”已存在。"));
                continue;
            }

            if (draft.ThresholdVnd <= 0)
            {
                errors.Add(L($"Step '{id}' threshold must be positive.", $"Số tiền mốc '{id}' phải dương.", $"Der Schwellenwert für Stufe '{id}' muss größer als null sein.", $"等级“{id}”的门槛必须大于零。"));
                continue;
            }

            if (!TryParseItems(draft.ItemsText, out var items, out var itemError))
            {
                errors.Add(L($"Step '{id}': {itemError}", $"Mốc '{id}': {itemError}", $"Stufe '{id}': {itemError}", $"等级“{id}”：{itemError}"));
                continue;
            }

            steps.Add(new TopUpMilestoneStep
            {
                Id = id,
                ThresholdVnd = draft.ThresholdVnd,
                TitleEn = string.IsNullOrWhiteSpace(draft.TitleEn) ? $"{draft.ThresholdVnd:N0} VND" : draft.TitleEn.Trim(),
                TitleVi = string.IsNullOrWhiteSpace(draft.TitleVi) ? $"Mốc {draft.ThresholdVnd:N0} đ" : draft.TitleVi.Trim(),
                CoinType = draft.CoinType,
                Coins = Math.Max(0, draft.Coins),
                Zen = Math.Max(0, draft.Zen),
                Items = items,
            });
        }

        steps = steps.OrderBy(s => s.ThresholdVnd).ToList();
        return errors;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
    }

    private static string L(string en, string vi, string de, string zh) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "vi" => vi,
            "de" => de,
            "zh" => zh,
            _ => en,
        };

    private static bool TryParseItems(string? text, out List<TopUpMilestoneItem> items, out string error)
    {
        items = [];
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        foreach (var rawToken in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = rawToken.Split(':');
            if (parts.Length < 2
                || !short.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var group)
                || !short.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                error = L("Invalid item syntax (use group:number[:level[:qty]]).", "Cú pháp vật phẩm không hợp lệ (dùng nhóm:mã[:cấp[:sl]]).", "Ungültige Gegenstandsangabe (Gruppe:Nr.[:Stufe[:Menge]]).", "物品格式无效（请使用组:编号[:等级[:数量]]）。");
                return false;
            }

            byte level = 0;
            if (parts.Length >= 3 && !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
            {
                error = L("Invalid item level.", "Cấp vật phẩm không hợp lệ.", "Ungültige Gegenstandsstufe.", "物品等级无效。");
                return false;
            }

            var qty = 1;
            if (parts.Length >= 4 && (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out qty) || qty <= 0))
            {
                error = L("Invalid item quantity.", "Số lượng vật phẩm không hợp lệ.", "Ungültige Gegenstandsmenge.", "物品数量无效。");
                return false;
            }

            items.Add(new TopUpMilestoneItem
            {
                Group = group,
                Number = number,
                Level = level,
                Quantity = qty,
            });
        }

        return true;
    }

    private static string FormatItems(IEnumerable<TopUpMilestoneItem> items) =>
        string.Join(", ", items.Select(i => $"{i.Group}:{i.Number}:{i.Level}:{i.Quantity}"));

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<TopUpMilestonesConfiguration>(
                this.PersistenceContextProvider,
                typeof(TopUpMilestonesPlugIn),
                default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            this._configuration = saved ?? new TopUpMilestonesConfiguration();
            this._store = TopUpMilestoneStore.Open(this._configuration.FileName);
            this._stepDrafts = this._configuration.Steps
                .OrderBy(s => s.ThresholdVnd)
                .Select(s => new StepDraft
                {
                    Id = s.Id,
                    ThresholdVnd = s.ThresholdVnd,
                    TitleEn = s.TitleEn,
                    TitleVi = s.TitleVi,
                    CoinType = s.CoinType,
                    Coins = s.Coins,
                    Zen = s.Zen,
                    ItemsText = FormatItems(s.Items),
                })
                .ToList();
            this.RefreshAccounts();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to load top-up milestone settings.");
            this._errors.Add(L("Could not load top-up milestone settings. See the server log for details.", "Không tải được cài đặt thưởng mốc nạp. Xem chi tiết trong nhật ký máy chủ.", "Aufladestufen konnten nicht geladen werden. Details stehen im Serverprotokoll.", "无法加载充值里程碑设置。详情请查阅服务器日志。"));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void RefreshAccounts()
    {
        if (this._store is null || this._configuration is null)
        {
            this._accounts = [];
            return;
        }

        var zone = HnmoStateStore.ResolveTimeZone(this._configuration.TimeZone);
        lock (this._store.Lock)
        {
            this._accounts = this._store.Data.Accounts
                .OrderByDescending(kv => kv.Value.TotalVnd)
                .Take(50)
                .Select(kv => new AccountSummaryRow(
                    kv.Key,
                    kv.Value.TotalVnd,
                    kv.Value.ClaimedStepIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList(),
                    kv.Value.History.Count > 0 ? HnmoStateStore.ToTimeZone(kv.Value.History[0].AtUtc, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "—"))
                .ToList();
        }
    }

    private void AddStep()
    {
        var nextThreshold = (this._stepDrafts.LastOrDefault()?.ThresholdVnd ?? 0) + 500_000;
        this._stepDrafts.Add(new StepDraft
        {
            Id = $"step-{nextThreshold}",
            ThresholdVnd = nextThreshold,
            TitleEn = $"{nextThreshold:N0} VND",
            TitleVi = $"Mốc {nextThreshold:N0} đ",
            CoinType = CashShopCoinType.WCoinC,
            Coins = 500,
        });
    }

    private async Task SaveSettingsAsync()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._errors.Clear();
        var errors = ValidateAndBuildSteps(this._stepDrafts, out var steps);
        if (errors.Count > 0)
        {
            this._errors.AddRange(errors);
            return;
        }

        this._configuration.Steps = steps;
        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(this._configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(TopUpMilestonesPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<TopUpMilestonesConfiguration>(
                this.PersistenceContextProvider,
                typeof(TopUpMilestonesPlugIn),
                default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this._store = TopUpMilestoneStore.Open(this._configuration.FileName);
            this.RefreshAccounts();
            this.ToastService.ShowSuccess(L("Top-up milestone settings saved.", "Đã lưu cài đặt thưởng mốc nạp.", "Aufladestufen gespeichert.", "已保存充值里程碑设置。"));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to save top-up milestones configuration.");
            this._errors.Add(L("Could not save top-up milestone settings. See the server log for details.", "Không lưu được cài đặt thưởng mốc nạp. Xem chi tiết trong nhật ký máy chủ.", "Aufladestufen konnten nicht gespeichert werden. Details stehen im Serverprotokoll.", "无法保存充值里程碑设置。详情请查阅服务器日志。"));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private void RecordManualTopUp()
    {
        if (this._configuration is null)
        {
            return;
        }

        this._errors.Clear();
        if (string.IsNullOrWhiteSpace(this._manualLogin) || this._manualVnd <= 0 || string.IsNullOrWhiteSpace(this._manualReference))
        {
            this._errors.Add(L("Enter login name, positive VND amount, and a unique transfer reference.", "Nhập tên tài khoản, số tiền VND dương và mã giao dịch riêng.", "Kontoname, positiver VND-Betrag und eindeutige Überweisungsreferenz sind erforderlich.", "请输入登录账号、正数 VND 金额及唯一的转账编号。"));
            return;
        }

        var store = TopUpMilestoneStore.Open(this._configuration.FileName);
        var engine = new TopUpMilestoneEngine(this._configuration, store, enabled: this._isActive);
        var outcome = engine.RecordTopUp(this._manualLogin, this._manualVnd, this._manualReference, "admin-manual", DateTime.UtcNow);
        lock (store.Lock)
        {
            store.SaveIfChanged();
        }

        if (!outcome.Recorded)
        {
            this._errors.Add(L("That transfer reference was already recorded.", "Mã giao dịch này đã được ghi nhận trước đó.", "Diese Überweisungsreferenz wurde bereits erfasst.", "该转账编号已登记。"));
            return;
        }

        this.RefreshAccounts();
        this.ToastService.ShowSuccess(L(
            $"Recorded {this._manualVnd:N0} VND for {this._manualLogin.Trim()} (total {outcome.NewTotalVnd:N0} VND).",
            $"Đã ghi nhận {this._manualVnd:N0} đ cho {this._manualLogin.Trim()} (tổng {outcome.NewTotalVnd:N0} đ).",
            $"Für {this._manualLogin.Trim()} wurden {this._manualVnd:N0} VND erfasst (insgesamt {outcome.NewTotalVnd:N0} VND).",
            $"已为 {this._manualLogin.Trim()} 登记 {this._manualVnd:N0} VND（总额 {outcome.NewTotalVnd:N0} VND）。"));
        this._manualReference = string.Empty;
    }

    /// <summary>Editable row for one milestone step in the admin UI.</summary>
    internal sealed class StepDraft
    {
        public string Id { get; set; } = string.Empty;

        public long ThresholdVnd { get; set; }

        public string TitleEn { get; set; } = string.Empty;

        public string TitleVi { get; set; } = string.Empty;

        public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

        public int Coins { get; set; }

        public int Zen { get; set; }

        public string ItemsText { get; set; } = string.Empty;
    }

    private sealed record AccountSummaryRow(string Login, long TotalVnd, List<string> Claimed, string LastEventSydney);
}
