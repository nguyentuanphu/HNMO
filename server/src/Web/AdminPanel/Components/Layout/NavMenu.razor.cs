// <copyright file="NavMenu.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components.Layout;

using System.Globalization;
using System.Threading;
using HNMO.Network.Analyzer;
using HNMO.Persistence;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Navigation menu of the admin panel.
/// </summary>
public partial class NavMenu : IDisposable
{
    private bool _isDisposed;

    private bool _collapseNavMenu = true;

    private bool _isLoadingConfig = true;

    private int _isLoadingConfigInProgress;

    private bool _onlyShowSetup;

    private int _availableConfigUpdates;

    private bool _expandSystem;

    private bool _expandInspect;

    private AdminNavigationPreferences? _prefs;

    private static readonly HashSet<string> DropdownHandledRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "setup",
        "config-updates",
        "config-captions",
        "users",
        "api-keys",
        "discord",
        "logfiles",
        "host-settings",
        "logging-configuration",
        "network-analyzer",
    };

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private AdminNavigationPreferences Preferences
    {
        get
        {
            if (this._prefs is null)
            {
                this._prefs = this.ServiceProvider.GetService<AdminNavigationPreferences>() ?? new AdminNavigationPreferences();
                this._prefs.Changed += this.OnPreferencesChanged;
            }

            return this._prefs;
        }
    }

    private void OnPreferencesChanged()
    {
        if (!this._isDisposed)
        {
            _ = this.InvokeAsync(this.StateHasChanged);
        }
    }

    private static bool IsCatalogVisibleInSidebar(AdminNavEntry entry) => !DropdownHandledRoutes.Contains(entry.Route);

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
        this._isDisposed = true;
        if (this._prefs is not null)
        {
            this._prefs.Changed -= this.OnPreferencesChanged;
        }

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
        if (this.GameConfigurationId is not null
            || this._isDisposed
            || Interlocked.CompareExchange(ref this._isLoadingConfigInProgress, 1, 0) != 0)
        {
            return;
        }

        if (!this._isLoadingConfig)
        {
            await this.RenderIfAliveAsync(() => this._isLoadingConfig = true).ConfigureAwait(false);
        }

        Guid? gameConfigurationId = null;
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (await this.PersistenceContextProvider.CanConnectToDatabaseAsync(cts.Token).ConfigureAwait(true)
                && await this.PersistenceContextProvider.DatabaseExistsAsync(cts.Token).ConfigureAwait(true))
            {
                using var context = this.PersistenceContextProvider.CreateNewConfigurationContext();
                gameConfigurationId = await context.GetDefaultGameConfigurationIdAsync(cts.Token).ConfigureAwait(true);
            }
        }
        catch
        {
            gameConfigurationId = null;
        }
        finally
        {
            Interlocked.Exchange(ref this._isLoadingConfigInProgress, 0);
            await this.RenderIfAliveAsync(() =>
            {
                this.GameConfigurationId = gameConfigurationId;
                this._onlyShowSetup = gameConfigurationId is null;
                this._isLoadingConfig = false;
            }).ConfigureAwait(false);
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (this._isDisposed)
        {
            return;
        }

        var availableUpdates = 0;
        try
        {
            if (this.GameConfigurationId is not null)
            {
                var updates = await this.UpdateService.DetermineAvailableUpdatesAsync().ConfigureAwait(false);
                availableUpdates = updates.Count;
            }
        }
        catch
        {
            availableUpdates = 0;
        }
        finally
        {
            await this.RenderIfAliveAsync(() => this._availableConfigUpdates = availableUpdates).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Applies a state change and renders the menu on the dispatcher, unless the menu or the
    /// services of its connection have already been disposed.
    /// </summary>
    /// <remarks>
    /// The background loading can finish after the browser tab was closed; creating new child
    /// components in that render would resolve services from the disposed provider.
    /// </remarks>
    /// <param name="update">The state change to apply before rendering.</param>
    private async Task RenderIfAliveAsync(Action update)
    {
        if (this._isDisposed)
        {
            return;
        }

        try
        {
            await this.InvokeAsync(() =>
            {
                if (this._isDisposed)
                {
                    return;
                }

                // A disposed service provider means that the connection is gone; rendering new
                // child components (e.g. NavLink or ConfigNavMenu) would fail on service injection.
                _ = this.ServiceProvider.GetService(typeof(NavigationManager));
                update();
                this.StateHasChanged();
            }).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The connection was closed while the background loading was running.
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
