// <copyright file="HostSettingsPage.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

/// <summary>
/// Admin panel page for editing unified host settings (<c>save/hnmo-settings.json</c>).
/// </summary>
public partial class HostSettingsPage
{
    private HostSettings? _settings;
    private LicenseStatusInfo? _licenseStatus;
    private List<string> _errors = [];
    private string _relayAllowedHostsText = string.Empty;
    private string _newDatabasePassword = string.Empty;
    private string? _statusMessage;
    private bool _showHelp;
    private bool _confirmRestart;
    private HashSet<int> _initialPorts = [];
    private string _licenseText = string.Empty;
    private string _requestName = string.Empty;
    private string _requestContact = string.Empty;
    private int? _requestPlayers;
    private string _requestMessage = string.Empty;
    private bool _requestSending;
    private HNMO.GameLogic.DeveloperHubState _hubState = new();
    private bool _copied;

    [Inject]
    private IHostSettingsService SettingsService { get; set; } = default!;

    [Inject]
    private IServerRestartService RestartService { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    /// <summary>Gets a value indicating whether this page was opened on the host computer itself (audit 2.5).</summary>
    private bool IsHostComputer { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // Ports, paths, the database password and the licence: only from the host computer, never through the
        // website's admin path or another proxy.
        this.IsHostComputer = HostComputerRequest.IsFromHostComputer(this.HttpContextAccessor.HttpContext);
        if (!this.IsHostComputer)
        {
            return;
        }

        this._settings = this.SettingsService.Load();
        this._relayAllowedHostsText = string.Join(", ", this._settings.RelayAllowedHosts);
        this._initialPorts = [this._settings.WebPort, this._settings.AdminPort, this._settings.ConnectServerPort, this._settings.DatabasePort];
        this._licenseStatus = this.SettingsService.GetLicenseStatus(this._settings.LicenseFilePath);
        this._hubState = HNMO.GameLogic.DeveloperHub.ReadState();
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private void ToggleHelp() => this._showHelp = !this._showHelp;

    private async Task SaveAsync()
    {
        if (this._settings is null)
        {
            return;
        }

        this._errors.Clear();
        this._statusMessage = null;
        this._settings.RelayAllowedHosts = this._relayAllowedHostsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var validationErrors = this.SettingsService.Validate(this._settings, this._initialPorts);
        if (validationErrors.Count > 0)
        {
            this._errors.AddRange(validationErrors);
            return;
        }

        try
        {
            await this.SettingsService.SaveAsync(this._settings, this._newDatabasePassword).ConfigureAwait(true);
            this._newDatabasePassword = string.Empty;
            this._initialPorts = [this._settings.WebPort, this._settings.AdminPort, this._settings.ConnectServerPort, this._settings.DatabasePort];
            this._licenseStatus = this.SettingsService.GetLicenseStatus(this._settings.LicenseFilePath);
            this._statusMessage = Resources.HostSettingsSaved;
        }
        catch (Exception ex)
        {
            this._errors.Add(ex.Message);
        }
    }

    private async Task OnLicenseUploadAsync(InputFileChangeEventArgs e)
    {
        if (!this.IsHostComputer || this._settings is null)
        {
            return;
        }

        try
        {
            using var ms = new MemoryStream();
            await e.File.OpenReadStream(maxAllowedSize: 64 * 1024).CopyToAsync(ms).ConfigureAwait(true);
            await this.UseLicenseAsync(ms.ToArray()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this._errors.Add(ex.Message);
        }
    }

    private async Task SaveLicenseTextAsync()
    {
        if (!this.IsHostComputer || this._settings is null || string.IsNullOrWhiteSpace(this._licenseText))
        {
            return;
        }

        await this.UseLicenseAsync(System.Text.Encoding.UTF8.GetBytes(this._licenseText.Trim())).ConfigureAwait(true);
        if (this._errors.Count == 0)
        {
            this._licenseText = string.Empty;
        }
    }

    /// <summary>Checks a license first; a good one is saved as license.hnmo and used at once.</summary>
    private async Task UseLicenseAsync(byte[] content)
    {
        this._errors.Clear();
        this._statusMessage = null;
        var check = this.SettingsService.CheckLicense(content);
        if (!check.IsValid)
        {
            this._errors.Add(L($"This license can't be used: {check.Message} Nothing was changed.", $"Không dùng được giấy phép này: {check.Message} Chưa thay đổi gì."));
            return;
        }

        try
        {
            await this.SettingsService.SaveLicenseFileAsync(this._settings!.LicenseFilePath, content).ConfigureAwait(true);
            this._licenseStatus = this.SettingsService.ApplyLicense(this._settings.LicenseFilePath);
            this._statusMessage = L($"License saved and in use: up to {this._licenseStatus.MaximumPlayers} players at once.", $"Đã lưu và đang dùng giấy phép: tối đa {this._licenseStatus.MaximumPlayers} người chơi cùng lúc.");
        }
        catch (Exception ex)
        {
            this._errors.Add(ex.Message);
        }
    }

    private async Task RequestLicenseAsync()
    {
        if (!this.IsHostComputer || this._requestSending)
        {
            return;
        }

        this._errors.Clear();
        this._statusMessage = null;
        if (string.IsNullOrWhiteSpace(this._requestName))
        {
            this._errors.Add(L("Write your name or your server's name.", "Hãy ghi tên bạn hoặc tên server."));
            return;
        }

        this._requestSending = true;
        try
        {
            var (ok, en, vi) = await HNMO.GameLogic.DeveloperHub.RequestLicenceAsync(this._requestName.Trim(), this._requestContact.Trim(), this._requestPlayers ?? 0, this._requestMessage.Trim()).ConfigureAwait(true);
            if (ok)
            {
                this._statusMessage = L(en, vi);
                this._requestMessage = string.Empty;
            }
            else
            {
                this._errors.Add(L(en, vi));
            }

            this._hubState = HNMO.GameLogic.DeveloperHub.ReadState();
        }
        finally
        {
            this._requestSending = false;
        }
    }

    /// <summary>Asks the hub whether the developer issued the requested licence; a licence that came is used at once.</summary>
    private async Task CheckRequestAsync()
    {
        if (!this.IsHostComputer || this._requestSending)
        {
            return;
        }

        this._errors.Clear();
        this._statusMessage = null;
        this._requestSending = true;
        try
        {
            var (status, licence) = await HNMO.GameLogic.DeveloperHub.FetchLicenceAsync().ConfigureAwait(true);
            if (status == "issued" && licence is not null)
            {
                await this.UseLicenseAsync(System.Text.Encoding.UTF8.GetBytes(licence)).ConfigureAwait(true);
            }
            else
            {
                this._statusMessage = status switch
                {
                    "declined" => L("The HNMO developer declined the request.", "Nhà phát triển HNMO đã từ chối yêu cầu."),
                    "open" => L("Not issued yet; it comes here by itself once it is.", "Chưa được cấp; khi cấp xong giấy phép sẽ tự về đây."),
                    _ => L("The hub couldn't be asked now; try later.", "Chưa hỏi được hub lúc này; hãy thử lại sau."),
                };
            }

            this._hubState = HNMO.GameLogic.DeveloperHub.ReadState();
        }
        finally
        {
            this._requestSending = false;
        }
    }

    private async Task CopyMachineIdAsync()
    {
        if (this._licenseStatus is null)
        {
            return;
        }

        try
        {
            await this.JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", this._licenseStatus.MachineId).ConfigureAwait(true);
            this._copied = true;
        }
        catch (JSException)
        {
            // no clipboard (an insecure page): the id is on the page to copy by hand
        }
    }

    private async Task RestartAsync()
    {
        if (!this.IsHostComputer)
        {
            return;
        }

        this._confirmRestart = false;
        this._errors.Clear();
        if (!this.RestartService.IsSupported)
        {
            this._errors.Add(Resources.HostSettingsRestartUnsupported);
            return;
        }

        this._statusMessage = Resources.HostSettingsRestarting;
        await this.RestartService.RequestRestartAsync().ConfigureAwait(true);
    }
}
