// <copyright file="HostSettingsPage.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

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

    [Inject]
    private IHostSettingsService SettingsService { get; set; } = default!;

    [Inject]
    private IServerRestartService RestartService { get; set; } = default!;

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
    }

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
        if (!this.IsHostComputer)
        {
            return;
        }

        if (this._settings is null)
        {
            return;
        }

        this._errors.Clear();
        this._statusMessage = null;
        try
        {
            using var ms = new MemoryStream();
            await e.File.OpenReadStream(maxAllowedSize: 64 * 1024).CopyToAsync(ms).ConfigureAwait(true);
            await this.SettingsService.SaveLicenseFileAsync(this._settings.LicenseFilePath, ms.ToArray()).ConfigureAwait(true);
            this._licenseStatus = this.SettingsService.GetLicenseStatus(this._settings.LicenseFilePath);
            this._statusMessage = Resources.HostSettingsSaved;
        }
        catch (Exception ex)
        {
            this._errors.Add(ex.Message);
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
