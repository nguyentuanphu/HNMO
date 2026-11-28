// <copyright file="BankTopUps.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.News;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using QRCoder;

/// <summary>
/// Bank top-ups (VietQR) of the players: the host's bank accounts, rate and keys, and the top-ups waiting for the
/// host to see the money, with Received / Reject.
/// </summary>
public partial class BankTopUps
{
    private readonly List<string> _errors = [];
    private BankTopUpConfiguration? _config;
    private List<BankTopUpPending> _topUps = [];
    private bool _active;
    private bool _loading = true;
    private bool _saving;
    private bool _busy;
    private bool _isAdministrator;
    private string _filter = "pending";
    private string _search = string.Empty;
    private int _previewIndex;
    private string? _previewSvg;
    private string _previewCaption = string.Empty;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private AdminOperationExecutor Executor { get; set; } = null!;

    [Inject]
    private AdminPolicyGuard PolicyGuard { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<BankTopUps> Logger { get; set; } = null!;

    private static string HelpText => L(
        "Add your bank account(s), the coin rate and the minimum, switch it on and save. Players then pick an amount on the top-up page and get a VietQR code with a transfer code. When the money arrives, press Received: the coins are added once (a second click or Casso/SePay reporting the same transfer adds nothing). Reject closes a top-up without coins. Times are Sydney time.",
        "Thêm tài khoản ngân hàng, tỉ giá coin và mức tối thiểu, bật lên rồi lưu. Người chơi chọn số tiền ở trang nạp và nhận mã VietQR kèm mã chuyển khoản. Khi tiền về, bấm Đã nhận: coin được cộng một lần (bấm lần nữa hay Casso/SePay báo cùng khoản chuyển cũng không cộng thêm). Từ chối đóng lệnh nạp mà không cộng coin. Giờ theo giờ Sydney.");

    private BankTopUpService Service => new(
        new CashShopCoinService(this.PersistenceContextProvider, this.GameConfigurationSource),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<BankTopUpService>.Instance);

    /// <summary>The NAPAS VietQR text of a transfer (EMVCo, the same as the web game's top-up page).</summary>
    /// <param name="bin">The bank id.</param>
    /// <param name="account">The account number.</param>
    /// <param name="holder">The holder's name.</param>
    /// <param name="amount">The amount in VND (0: none).</param>
    /// <param name="message">The transfer message.</param>
    /// <returns>The payload.</returns>
    public static string VietQrPayload(string bin, string account, string holder, long amount, string message) =>
        HNMO.GameLogic.PlugIns.VietQr.BuildPayload(bin, account, holder, amount, message);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._isAdministrator = await this.PolicyGuard.IsAuthorizedAsync(AdminPolicies.Administrator).ConfigureAwait(true);
        var (_, active, saved) = await PlugInSettings.ReadStoredAsync<BankTopUpConfiguration>(this.PersistenceContextProvider, typeof(BankTopUpPlugIn), default).ConfigureAwait(true);
        this._config = saved ?? new BankTopUpConfiguration();
        this._config.MigrateLegacyAccounts();
        this._active = active;
        this.UpdatePreview();
        this.Reload();
        this._loading = false;
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static bool IsListed(string bin) => VietnameseBanks.All.Any(b => b.Bin == bin);

    private static string Sydney(DateTime? utc) => utc is { } value
        ? HnmoStateStore.ToTimeZone(value, NewsStore.ResolveSydneyZone()).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)
        : string.Empty;

    private static string StateLabel(string state) => state switch
    {
        "pending" => L("Waiting", "Đang chờ"),
        "credited" => L("Credited", "Đã cộng"),
        "rejected" => L("Rejected", "Đã từ chối"),
        _ => L("Expired", "Hết hạn"),
    };

    private string StateOf(BankTopUpPending topUp) =>
        topUp.Status == BankTopUpPendingStatus.Pending && !topUp.IsOpen(DateTime.UtcNow) ? "expired" : topUp.Status.ToString().ToLowerInvariant();

    private IEnumerable<BankTopUpPending> Shown()
    {
        var search = this._search.Trim();
        return this._topUps
            .Where(t => this._filter == "all" || this.StateOf(t) == this._filter)
            .Where(t => search.Length == 0 || t.Login.Contains(search, StringComparison.OrdinalIgnoreCase) || t.Code.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(300);
    }

    private void Reload() => this._topUps = this.Service.Pending.All();

    private void AddAccount()
    {
        this._config?.Accounts.Add(new BankTopUpAccount { Bin = VietnameseBanks.All[0].Bin });
        this.Preview((this._config?.Accounts.Count ?? 1) - 1);
    }

    private void RemoveAccount(int index)
    {
        if (this._config is not null && index < this._config.Accounts.Count)
        {
            this._config.Accounts.RemoveAt(index);
            this.Preview(0);
        }
    }

    private void PickBank(int index, string? bin)
    {
        if (this._config is null || index >= this._config.Accounts.Count)
        {
            return;
        }

        this._config.Accounts[index].Bin = bin is null or "other" ? string.Empty : bin;
        this.Preview(index);
    }

    private void Preview(int index)
    {
        this._previewIndex = index;
        this.UpdatePreview();
    }

    private void UpdatePreview()
    {
        this._previewSvg = null;
        if (this._config is null || this._config.Accounts.Count == 0)
        {
            return;
        }

        var account = this._config.Accounts[Math.Clamp(this._previewIndex, 0, this._config.Accounts.Count - 1)];
        if (account.Bin.Length != 6 || account.Number.Trim().Length == 0)
        {
            return;
        }

        var code = $"{this._config.TransferPrefix.Trim().ToUpperInvariant()}7K3QX2";
        var payload = VietQrPayload(account.Bin, account.Number, this._config.HolderOf(account), 50_000, code);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        this._previewSvg = new SvgQRCode(data).GetGraphic(4, "#000000", "#ffffff", drawQuietZones: true);
        this._previewCaption = $"{VietnameseBanks.NameOf(account.Bin)} · {account.Number.Trim()} · {this._config.HolderOf(account)} · {code}";
    }

    private async Task SaveAsync()
    {
        if (this._config is null || !await this.PolicyGuard.IsAuthorizedAsync(AdminPolicies.Administrator).ConfigureAwait(true))
        {
            return;
        }

        this._errors.Clear();
        this._config.Accounts.RemoveAll(a => a.Number.Trim().Length == 0);
        foreach (var account in this._config.Accounts)
        {
            account.Bin = account.Bin.Trim();
            account.Number = account.Number.Trim();
            if (account.Bin.Length != 6 || !account.Bin.All(char.IsAsciiDigit))
            {
                this._errors.Add(L($"{account.Number}: the bank id must be 6 digits.", $"{account.Number}: mã ngân hàng phải có 6 chữ số."));
            }
        }

        if (this._active && this._config.UsableAccounts().Count == 0)
        {
            this._errors.Add(L("Add at least one shown account before switching it on.", "Hãy thêm ít nhất một tài khoản đang hiện trước khi bật."));
        }

        if (this._config.VndPerCoin < 1 || this._config.MinimumVnd < 1000 || this._config.TransferPrefix.Trim().Length is < 2 or > 10 || !this._config.TransferPrefix.Trim().All(char.IsAsciiLetterOrDigit))
        {
            this._errors.Add(L("Check the rate (1 or more), the minimum (1,000 VND or more) and the transfer word (2-10 letters or digits).", "Kiểm tra tỉ giá (từ 1), mức tối thiểu (từ 1.000 đ) và tiền tố (2-10 chữ hoặc số)."));
        }

        if (this._errors.Count > 0)
        {
            return;
        }

        this._saving = true;
        try
        {
            var id = typeof(BankTopUpPlugIn).GUID;
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(this._config, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, this._config, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, this._config, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<BankTopUpConfiguration>(this.PersistenceContextProvider, typeof(BankTopUpPlugIn), default).ConfigureAwait(true);
            if (active != this._active)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._active, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._active, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.UpdatePreview();
            this.ToastService.ShowSuccess(L("Saved.", "Đã lưu."));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "The bank top-up settings couldn't be saved.");
            this._errors.Add(L("Could not save. See the server log.", "Không lưu được. Xem nhật ký máy chủ."));
        }
        finally
        {
            this._saving = false;
        }
    }

    private async Task ConfirmAsync(string id)
    {
        this._busy = true;
        try
        {
            var (_, _, saved) = await PlugInSettings.ReadStoredAsync<BankTopUpConfiguration>(this.PersistenceContextProvider, typeof(BankTopUpPlugIn), default).ConfigureAwait(true);
            var config = saved ?? new BankTopUpConfiguration();
            var result = await this.Executor.ExecuteAsync(
                AdminRoles.CashShop,
                "bank_topup_confirm",
                new { id },
                actor => this.Service.ConfirmPendingAsync(id, config, actor.Name),
                default,
                value => value is BankTopUpResult.Credited or BankTopUpResult.AlreadyCredited).ConfigureAwait(true);
            switch (result)
            {
                case BankTopUpResult.Credited:
                    this.ToastService.ShowSuccess(L("The coins were added.", "Đã cộng coin."));
                    break;
                case BankTopUpResult.AlreadyCredited:
                    this.ToastService.ShowInfo(L("Already credited: nothing added again.", "Đã cộng trước đó: không cộng thêm."));
                    break;
                default:
                    this.ToastService.ShowError(L("Not credited: the account or the top-up no longer exists.", "Không cộng được: tài khoản hoặc lệnh nạp không còn."));
                    break;
            }
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._busy = false;
            this.Reload();
        }
    }

    private async Task RejectAsync(string id)
    {
        this._busy = true;
        try
        {
            var result = await this.Executor.ExecuteAsync(
                AdminRoles.CashShop,
                "bank_topup_reject",
                new { id },
                actor => ValueTask.FromResult(this.Service.RejectPending(id, actor.Name)),
                default,
                value => value is BankTopUpResult.Rejected).ConfigureAwait(true);
            if (result == BankTopUpResult.AlreadyCredited)
            {
                this.ToastService.ShowInfo(L("It was already credited; it stays credited.", "Lệnh này đã cộng coin; giữ nguyên."));
            }
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._busy = false;
            this.Reload();
        }
    }
}
