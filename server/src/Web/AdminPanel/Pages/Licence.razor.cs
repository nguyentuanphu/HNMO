// <copyright file="Licence.razor.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Threading;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using QRCoder;

/// <summary>
/// The licence shop (owner, 2026-11-28): the licence in use, the hub's packages, a 14-day trial, and paying by card,
/// PayOS or a bank transfer (VietQR drawn here); the licence arrives and is used without a restart.
/// </summary>
public partial class Licence : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _closing = new();
    private HostSettings? _settings;
    private LicenseStatusInfo? _status;
    private DeveloperHubState _hub = new();
    private HubLicenceCatalogue? _catalogue;
    private string _currency = "vnd";
    private string _billing = "year";
    private string? _payUrl;
    private HubBankTransfer? _bank;
    private string? _bankQrSvg;
    private string? _message;
    private bool _messageIsError;
    private bool _busy;
    private string _upgradeTo = string.Empty;
    private string _toMachine = string.Empty;

    [Inject]
    private IHostSettingsService SettingsService { get; set; } = null!;

    [Inject]
    private AdminPolicyGuard PolicyGuard { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    private ILogger<Licence> Logger { get; set; } = null!;

    /// <summary>Gets a value indicating whether the hub is off on this server (tests and offline hosts).</summary>
    private static bool HubOff => DeveloperHub.Url.Length == 0;

    private bool Waiting => this._hub.Purchase is not null;

    /// <inheritdoc />
    public void Dispose()
    {
        this._closing.Cancel();
        this._closing.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>The price as the host reads it: whole đồng, or cents shown as dollars.</summary>
    /// <param name="currency">vnd, aud or usd.</param>
    /// <param name="amount">The amount (đồng, or cents).</param>
    /// <returns>The text.</returns>
    internal static string PriceText(string currency, long amount) => currency switch
    {
        "vnd" => string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} ₫", amount),
        "aud" => string.Format(CultureInfo.InvariantCulture, "A${0:0.00}", amount / 100m),
        _ => string.Format(CultureInfo.InvariantCulture, "US${0:0.00}", amount / 100m),
    };

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.Reload();
        if (HubOff)
        {
            return;
        }

        this._catalogue = await DeveloperHub.GetPackagesAsync().ConfigureAwait(true);
        if (this._catalogue.Packages.Any(p => p.Prices.ContainsKey(this._currency)) is false)
        {
            this._currency = this._catalogue.Packages.SelectMany(p => p.Prices.Keys).FirstOrDefault() ?? "vnd";
        }

        // Opening the page while a licence is awaited asks once (at most every 30 seconds).
        if (this.Waiting)
        {
            await this.CheckNowAsync(fromButton: false).ConfigureAwait(true);
        }
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string Localized(string en, string vi) => L(en, vi.Length > 0 ? vi : en);

    private static string SeasonText(string season) => season switch
    {
        "s9" => "Season 9",
        "arena" => "Arena",
        _ => "Season 6.3",
    };

    private static string BillingText(HubLicencePackage p) => p.Billing switch
    {
        "month" => L("a month", "một tháng"),
        "year" => L("a year", "một năm"),
        "lifetime" => L("for good", "vĩnh viễn"),
        _ => p.DurationDays is { } days ? L($"{days} days", $"{days} ngày") : L("once", "một lần"),
    };

    private void Reload()
    {
        this._settings = this.SettingsService.Load();
        this._status = this.SettingsService.GetLicenseStatus(this._settings.LicenseFilePath);
        this._hub = DeveloperHub.ReadState();
        if (this._hub.Purchase?.Bank is { } bank && this._bank is null)
        {
            this.ShowBank(bank);
        }
    }

    private IEnumerable<HubLicencePackage> Plans() =>
        (this._catalogue?.Packages ?? []).Where(p => p.Kind == "plan" && p.Billing == this._billing && p.Prices.ContainsKey(this._currency));

    private IEnumerable<HubLicencePackage> AddOns() =>
        (this._catalogue?.Packages ?? []).Where(p => p.Kind == "addon" && p.Prices.ContainsKey(this._currency));

    private IEnumerable<HubLicencePackage> UpgradeTargets() =>
        (this._catalogue?.Packages ?? []).Where(p => p.Kind == "plan" && p.MaxPlayers > (this._status?.MaximumPlayers ?? 0)).GroupBy(p => p.Id).Select(g => g.First());

    private HubLicencePackage? Trial() =>
        this._hub.TrialUsed || this._status?.IsValid == true ? null : this._catalogue?.Packages.FirstOrDefault(p => p.Kind == "trial");

    private IEnumerable<string> Currencies() =>
        (this._catalogue?.Packages ?? []).SelectMany(p => p.Prices.Keys).Distinct().OrderBy(c => c == "vnd" ? 0 : c == "aud" ? 1 : 2);

    private async Task<bool> AllowedAsync()
    {
        if (await this.PolicyGuard.IsAuthorizedAsync(AdminPolicies.Administrator).ConfigureAwait(true))
        {
            return true;
        }

        this.Say(L("Only an administrator can buy a licence.", "Chỉ quản trị viên mới mua được giấy phép."), error: true);
        return false;
    }

    private async Task BuyAsync(HubLicencePackage package, string provider)
    {
        if (this._busy || !await this.AllowedAsync().ConfigureAwait(true))
        {
            return;
        }

        this._busy = true;
        try
        {
            var currency = provider == "bank" ? "vnd" : this._currency;
            var upgradeTo = package.Addon == "upgrade" ? this._upgradeTo : null;
            var toMachine = package.Addon is "move" or "second-machine" ? this._toMachine.Trim() : null;
            if ((package.Addon == "upgrade" && string.IsNullOrEmpty(upgradeTo))
                || (package.Addon is "move" or "second-machine" && (toMachine is not { Length: >= 8 and <= 64 } || !toMachine.All(char.IsAsciiLetterOrDigit))))
            {
                this.Say(package.Addon == "upgrade"
                    ? L("Pick the package to move up to.", "Hãy chọn gói muốn nâng lên.")
                    : L("Enter the other computer's id (on its Host settings page, \"Copy\").", "Hãy nhập mã máy kia (ở trang Host settings của máy đó, nút \"Copy\")."), error: true);
                return;
            }

            var checkout = await DeveloperHub.StartCheckoutAsync(package.Id, currency, provider, this._hub.HostName, this._hub.HostContact, upgradeTo, toMachine).ConfigureAwait(true);
            this.Say(L(checkout.En, checkout.Vi), error: !checkout.Started);
            this._payUrl = checkout.Url;
            if (checkout.Bank is { } bank)
            {
                this.ShowBank(bank);
            }

            if (checkout.Url is { } url)
            {
                await this.JsRuntime.InvokeVoidAsync("open", this._closing.Token, url, "_blank", "noopener").ConfigureAwait(true);
            }
        }
        catch (JSException ex)
        {
            // A blocked pop-up: the link on the page still opens it.
            this.Logger.LogDebug(ex, "The payment page couldn't be opened in a new tab.");
        }
        finally
        {
            this._busy = false;
            this._hub = DeveloperHub.ReadState();
        }
    }

    private async Task StartTrialAsync()
    {
        if (this._busy || !await this.AllowedAsync().ConfigureAwait(true))
        {
            return;
        }

        this._busy = true;
        try
        {
            var (licence, en, vi) = await DeveloperHub.StartTrialAsync(this._hub.HostName, this._hub.HostContact).ConfigureAwait(true);
            if (licence is null)
            {
                this.Say(L(en, vi), error: true);
            }
            else
            {
                await this.UseAsync(licence).ConfigureAwait(true);
            }
        }
        finally
        {
            this._busy = false;
            this.Reload();
        }
    }

    private async Task CheckNowAsync(bool fromButton)
    {
        if (fromButton && !await this.AllowedAsync().ConfigureAwait(true))
        {
            return;
        }

        var licence = await DeveloperHub.CheckPurchaseNowAsync(DateTime.UtcNow).ConfigureAwait(true);
        if (licence is not null)
        {
            await this.UseAsync(licence).ConfigureAwait(true);
        }
        else if (fromButton)
        {
            this.Say(L("Not here yet. HNMO keeps checking by itself; you can close this page.", "Chưa về. HNMO vẫn tự kiểm tra; anh/chị có thể đóng trang này."), error: false);
        }

        this.Reload();
    }

    private void CancelWait()
    {
        DeveloperHub.StopWaiting();
        (this._payUrl, this._bank, this._bankQrSvg) = (null, null, null);
        this.Reload();
    }

    private async Task RefreshPackagesAsync()
    {
        this._catalogue = await DeveloperHub.GetPackagesAsync(force: true).ConfigureAwait(true);
    }

    private async Task UseAsync(string licence)
    {
        var content = System.Text.Encoding.UTF8.GetBytes(licence);
        var check = this.SettingsService.CheckLicense(content);
        if (!check.IsValid)
        {
            this.Say(check.Message, error: true);
            return;
        }

        await this.SettingsService.SaveLicenseFileAsync(this._settings?.LicenseFilePath, content).ConfigureAwait(true);
        this._status = this.SettingsService.ApplyLicense(this._settings?.LicenseFilePath);
        (this._payUrl, this._bank, this._bankQrSvg) = (null, null, null);
        this.ToastService.ShowSuccess(L($"The licence is in use: up to {this._status.MaximumPlayers} players at once.", $"Giấy phép đang được dùng: tối đa {this._status.MaximumPlayers} người chơi cùng lúc."));
        this.Say(L("The licence arrived and is in use. No restart is needed.", "Giấy phép đã về và đang được dùng. Không cần khởi động lại."), error: false);
    }

    private void ShowBank(HubBankTransfer bank)
    {
        this._bank = bank;
        var payload = VietQr.BuildPayload(bank.Bin, bank.Account, bank.AccountName, bank.Amount, bank.Note);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        this._bankQrSvg = new SvgQRCode(data).GetGraphic(4, "#000000", "#ffffff", drawQuietZones: true);
    }

    private async Task CopyAsync(string text)
    {
        try
        {
            await this.JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", this._closing.Token, text).ConfigureAwait(true);
            this.ToastService.ShowSuccess(L("Copied.", "Đã copy."));
        }
        catch (JSException)
        {
            this.Say(text, error: false);
        }
    }

    private void Say(string message, bool error) => (this._message, this._messageIsError) = (message, error);
}
