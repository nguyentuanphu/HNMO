// <copyright file="NavMenu.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components.Layout;

using System.Threading;
using Microsoft.AspNetCore.Components;

using HNMO.Network.Analyzer;
using HNMO.Persistence;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Web.Shared.Services;

/// <summary>
/// Navigation menu of the admin panel.
/// </summary>
public partial class NavMenu : IDisposable
{
    private bool _collapseNavMenu = true;

    private bool _isLoadingConfig = false;

    private bool _onlyShowSetup;

    private int _availableConfigUpdates;

    private bool _expandSystem;

    private bool _expandInspect;

    [Inject]
    private IMigratableDatabaseContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private SetupService SetupService { get; set; } = null!;

    [Inject]
    private DataUpdateService UpdateService { get; set; } = null!;

    [Inject]
    private NavigationHistory NavigationHistory { get; set; } = null!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether the network analyzer is available. It needs the
    /// servers in the same process, so it's only registered in the all-in-one deployment.
    /// </summary>
    /// <remarks>
    /// It's determined once, because the menu also renders when its background loading completes,
    /// which can be after the services of the closed connection were disposed.
    /// </remarks>
    private bool IsNetworkAnalyzerAvailable { get; set; }

    private Guid? GameConfigurationId { get; set; }

    /// <summary>
    /// Gets the group with the stored player data which has no own page, edited with the generic editors.
    /// </summary>
    private static IReadOnlyList<NavGroup> PlayerDataGroups { get; } =
    [
        new("player-data", Properties.Resources.NavPlayerData, "oi-folder",
        [
            ConfigNavMenu.GridEntry<HNMO.DataModel.Entities.Character>("oi-person"),
            ConfigNavMenu.GridEntry<HNMO.DataModel.Entities.LetterBody>("oi-envelope-closed"),
            ConfigNavMenu.GridEntry<HNMO.DataModel.Entities.GensMember>("oi-badge"),
            ConfigNavMenu.GridEntry<HNMO.DataModel.Entities.CashShopStorageItem>("oi-basket"),
            ConfigNavMenu.GridEntry<HNMO.DataModel.Entities.CastleSiegeData>("oi-shield"),
            ConfigNavMenu.GridEntry<HNMO.DataModel.Entities.CrywolfData>("oi-moon"),
        ]),
    ];

    /// <summary>
    /// Gets the class for the entries of the navigation menu.
    /// "collapse" is a class of bootstrap which hides it.
    /// In our CSS we also define to show it anyway, if the width sufficient.
    /// </summary>
    private string NavMenuCssClass => this._collapseNavMenu ? "collapse" : string.Empty;

    /// <inheritdoc />
    public void Dispose()
    {
        this.SetupService.DatabaseInitialized -= this.OnDatabaseInitializedAsync;
        this.UpdateService.UpdatesInstalled -= this.OnUpdatesInstalledAsync;
        this.NavigationManager.LocationChanged -= this.OnLocationChanged;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.IsNetworkAnalyzerAvailable = this.ServiceProvider.GetService(typeof(IPacketCaptureService)) is not null;
        await base.OnInitializedAsync().ConfigureAwait(false);
        this.SetupService.DatabaseInitialized += this.OnDatabaseInitializedAsync;
        this.UpdateService.UpdatesInstalled += this.OnUpdatesInstalledAsync;
        this.NavigationManager.LocationChanged += this.OnLocationChanged;
        _ = Task.Run(async () =>
        {
            await this.LoadGameConfigurationAsync().ConfigureAwait(false);
            await this.CheckForUpdatesAsync().ConfigureAwait(false);
        });
    }

    private async ValueTask OnUpdatesInstalledAsync()
    {
        this._availableConfigUpdates = 0;
        _ = Task.Run(this.CheckForUpdatesAsync);
    }

    private async ValueTask OnDatabaseInitializedAsync()
    {
        // We have to reload, because the old links are not correct anymore.
        this.GameConfigurationId = null;
        await this.LoadGameConfigurationAsync().ConfigureAwait(false);
        await this.CheckForUpdatesAsync().ConfigureAwait(false);
    }

    private async Task LoadGameConfigurationAsync()
    {
        if (this.GameConfigurationId is not null || this._isLoadingConfig)
        {
            return;
        }

        this._isLoadingConfig = true;
        await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(false);

        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (!await this.PersistenceContextProvider.CanConnectToDatabaseAsync(cts.Token).ConfigureAwait(true)
                || !await this.PersistenceContextProvider.DatabaseExistsAsync(cts.Token).ConfigureAwait(true))
            {
                return;
            }

            using var context = this.PersistenceContextProvider.CreateNewConfigurationContext();
            this.GameConfigurationId = await context.GetDefaultGameConfigurationIdAsync(cts.Token).ConfigureAwait(true);
        }
        catch
        {
            this.GameConfigurationId = null;
        }
        finally
        {
            this._onlyShowSetup = this.GameConfigurationId is null;
            this._isLoadingConfig = false;
            await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(false);
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            if (this.GameConfigurationId is null)
            {
                this._availableConfigUpdates = 0;
                return;
            }

            var updates = await this.UpdateService.DetermineAvailableUpdatesAsync().ConfigureAwait(false);
            this._availableConfigUpdates = updates.Count;
        }
        catch
        {
            this._availableConfigUpdates = 0;
        }
        finally
        {
            await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(true);
        }
    }

    private void ToggleNavMenu()
    {
        this._collapseNavMenu = !this._collapseNavMenu;
    }

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        // On small screens the menu covers the page, so it closes again after a link was chosen.
        if (!this._collapseNavMenu)
        {
            this._collapseNavMenu = true;
            _ = this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void ToggleSystem()
    {
        this._expandSystem = !this._expandSystem;
    }

    private void ToggleInspect()
    {
        this._expandInspect = !this._expandInspect;
    }
}
