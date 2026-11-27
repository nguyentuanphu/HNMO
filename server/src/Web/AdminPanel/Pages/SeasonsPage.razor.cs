// <copyright file="SeasonsPage.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unified Admin Panel page for inspecting and enabling Season 6.3, Season 9, and Arena.
/// </summary>
public partial class SeasonsPage
{
    private HostSettings? _settings;
    private IReadOnlyList<SeasonPanel> _panels = [];
    private List<string> _errors = [];
    private string? _statusMessage;
    private bool _showHelp;
    private bool _initialSeasonNine;
    private bool _initialArena;
    private bool _needsRestart;
    private bool _confirmRestart;

    [Inject]
    private IHostSettingsService SettingsService { get; set; } = default!;

    [Inject]
    private IServerRestartService RestartService { get; set; } = default!;

    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private bool IsHostComputer { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this.IsHostComputer = HostComputerRequest.IsFromHostComputer(this.HttpContextAccessor.HttpContext);
        this._settings = this.SettingsService.Load();
        this._initialSeasonNine = this._settings.SeasonNineEnabled;
        this._initialArena = this._settings.ArenaEnabled;
        this.RefreshPanels();
    }

    private void ToggleHelp() => this._showHelp = !this._showHelp;

    private string FormatPorts(int connectPort, int adminPort) =>
        string.Format(CultureInfo.CurrentUICulture, Resources.SeasonsPortsSummary, connectPort, adminPort);

    private void RefreshPanels()
    {
        if (this._settings is null)
        {
            return;
        }

        var uri = new Uri(this.Navigation.Uri);
        this._panels = new Uri(this.Navigation.BaseUri).AbsolutePath.Length > 1
            ? SeasonPanels.ForWebsite(HnmoSeasonInstance.Current, this._settings, this.Navigation.BaseUri)
            : SeasonPanels.For(HnmoSeasonInstance.Current, this._settings, uri.Scheme, uri.Host);
    }

    private async Task SaveAsync()
    {
        if (!this.IsHostComputer || this._settings is null)
        {
            return;
        }

        this._errors.Clear();
        this._statusMessage = null;

        var bound = new HashSet<int>
        {
            this._settings.WebPort,
            this._settings.AdminPort,
            this._settings.ConnectServerPort,
            this._settings.DatabasePort,
        };
        var validationErrors = this.SettingsService.Validate(this._settings, bound);
        if (validationErrors.Count > 0)
        {
            this._errors.AddRange(validationErrors);
            return;
        }

        await this.SettingsService.SaveAsync(this._settings, newDatabasePassword: null).ConfigureAwait(false);
        this._needsRestart = this._settings.SeasonNineEnabled != this._initialSeasonNine
            || this._settings.ArenaEnabled != this._initialArena;
        this._statusMessage = Resources.SeasonsSaved;
        this.RefreshPanels();
    }

    private async Task RestartAsync()
    {
        if (!this.IsHostComputer)
        {
            return;
        }

        this._confirmRestart = false;
        this._statusMessage = Resources.SeasonsRestartSent;
        await this.RestartService.RequestRestartAsync().ConfigureAwait(false);
    }
}
