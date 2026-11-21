// <copyright file="HostSettingsService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HNMO.Interfaces;

/// <summary>
/// Reads, validates, and updates <see cref="HostSettings"/> and the player license for the admin panel.
/// </summary>
public interface IHostSettingsService
{
    /// <summary>
    /// Gets the resolved path to <c>save/hnmo-settings.json</c>.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Loads the current <see cref="HostSettings"/>.
    /// </summary>
    /// <returns>The loaded settings.</returns>
    HostSettings Load();

    /// <summary>
    /// Validates the given <see cref="HostSettings"/>.
    /// </summary>
    /// <param name="settings">The settings to validate.</param>
    /// <param name="currentlyBoundPorts">Ports already bound by the running instance.</param>
    /// <returns>Validation error messages, or an empty list if valid.</returns>
    IReadOnlyList<string> Validate(HostSettings settings, IReadOnlySet<int>? currentlyBoundPorts = null);

    /// <summary>
    /// Saves the settings atomically and optionally updates the PostgreSQL password.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="newDatabasePassword">Optional new database password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous save.</returns>
    Task SaveAsync(HostSettings settings, string? newDatabasePassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspects the active player license file.
    /// </summary>
    /// <param name="configuredPath">The configured license file path.</param>
    /// <returns>The license status summary.</returns>
    LicenseStatusInfo GetLicenseStatus(string? configuredPath);

    /// <summary>Checks a license file's content before it is saved.</summary>
    /// <param name="content">The file's content.</param>
    /// <returns>What it would give.</returns>
    LicenseStatusInfo CheckLicense(byte[] content) => new(10, "Not checked.", string.Empty, string.Empty, true);

    /// <summary>Applies the saved license to the running server without a restart.</summary>
    /// <param name="configuredPath">The configured license file path.</param>
    /// <returns>The license now in use.</returns>
    LicenseStatusInfo ApplyLicense(string? configuredPath) => this.GetLicenseStatus(configuredPath);

    /// <summary>
    /// Saves an uploaded license file to disk.
    /// </summary>
    /// <param name="configuredPath">The configured license file path.</param>
    /// <param name="content">The raw license file bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved path where the license was written.</returns>
    Task<string> SaveLicenseFileAsync(string? configuredPath, byte[] content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IHostSettingsService"/> backed by <see cref="HostSettingsStore"/>.
/// </summary>
public sealed class HostSettingsService : IHostSettingsService
{
    private readonly string _baseDirectory;
    private readonly IHostLicenseProvider? _licenseProvider;
    private readonly IDatabasePasswordUpdater? _passwordUpdater;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostSettingsService"/> class.
    /// </summary>
    /// <param name="licenseProvider">Optional license provider.</param>
    /// <param name="passwordUpdater">Optional database password updater.</param>
    public HostSettingsService(IHostLicenseProvider? licenseProvider = null, IDatabasePasswordUpdater? passwordUpdater = null)
        : this(AppContext.BaseDirectory, licenseProvider, passwordUpdater)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostSettingsService"/> class.
    /// </summary>
    /// <param name="baseDirectory">The server application base directory.</param>
    /// <param name="licenseProvider">Optional license provider.</param>
    /// <param name="passwordUpdater">Optional database password updater.</param>
    public HostSettingsService(string baseDirectory, IHostLicenseProvider? licenseProvider = null, IDatabasePasswordUpdater? passwordUpdater = null)
    {
        this._baseDirectory = baseDirectory;
        this._licenseProvider = licenseProvider;
        this._passwordUpdater = passwordUpdater;
        this.SettingsFilePath = HostSettingsStore.ResolveDefaultPath(baseDirectory);
    }

    /// <inheritdoc />
    public string SettingsFilePath { get; }

    /// <inheritdoc />
    public HostSettings Load()
    {
        var webAppSettings = Path.GetFullPath(Path.Combine(this._baseDirectory, "..", "web", "appsettings.json"));
        var connectionXml = Path.Combine(this._baseDirectory, "ConnectionSettings.xml");
        return HostSettingsStore.LoadOrMigrate(this.SettingsFilePath, webAppSettings, connectionXml);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(HostSettings settings, IReadOnlySet<int>? currentlyBoundPorts = null) =>
        HostSettingsValidator.Validate(settings, checkPortAvailability: true, currentlyBoundPorts: currentlyBoundPorts);

    /// <inheritdoc />
    public async Task SaveAsync(HostSettings settings, string? newDatabasePassword, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(newDatabasePassword))
        {
            if (this._passwordUpdater is null)
            {
                throw new InvalidOperationException("Changing the database password is only supported when running through HNMO.Startup.");
            }

            await this._passwordUpdater.UpdatePasswordAsync(settings, newDatabasePassword, cancellationToken).ConfigureAwait(false);
            settings.DatabasePassword = newDatabasePassword;
        }

        HostSettingsStore.Save(this.SettingsFilePath, settings);
    }

    /// <inheritdoc />
    public LicenseStatusInfo GetLicenseStatus(string? configuredPath)
    {
        if (this._licenseProvider is not null)
        {
            return this._licenseProvider.GetStatus(configuredPath);
        }

        var resolved = this.ResolveLicensePath(configuredPath);
        var exists = File.Exists(resolved);
        return new LicenseStatusInfo(10, exists ? "License file found (restart to apply)." : "Default limit (up to 10 players).", Environment.MachineName, resolved, exists);
    }

    /// <inheritdoc />
    public LicenseStatusInfo CheckLicense(byte[] content) =>
        this._licenseProvider?.Check(content) ?? new LicenseStatusInfo(10, "The license can't be checked here.", Environment.MachineName, string.Empty, false);

    /// <inheritdoc />
    public LicenseStatusInfo ApplyLicense(string? configuredPath) =>
        this._licenseProvider?.Apply(configuredPath) ?? this.GetLicenseStatus(configuredPath);

    /// <inheritdoc />
    public async Task<string> SaveLicenseFileAsync(string? configuredPath, byte[] content, CancellationToken cancellationToken = default)
    {
        // Writing a file is only allowed inside the HNMO folder (the panel can be reached from the website).
        var folder = Path.GetFullPath(Path.Combine(this._baseDirectory, ".."));
        var resolved = Path.GetFullPath(this.ResolveLicensePath(configuredPath));
        if ((!string.IsNullOrWhiteSpace(configuredPath) && !HostSettingsValidator.IsInsideInstallFolder(configuredPath))
            || !resolved.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The license file must be saved inside the HNMO folder.");
        }

        var dir = Path.GetDirectoryName(resolved);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = resolved + ".tmp";
        await File.WriteAllBytesAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, resolved, overwrite: true);
        return resolved;
    }

    private string ResolveLicensePath(string? configuredPath)
    {
        var folder = Path.GetFullPath(Path.Combine(this._baseDirectory, ".."));
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            var inFolder = Path.Combine(folder, configuredPath);
            if (File.Exists(inFolder))
            {
                return inFolder;
            }

            return Path.Combine(this._baseDirectory, configuredPath);
        }

        var defaultInFolder = Path.Combine(folder, "license.hnmo");
        return File.Exists(defaultInFolder) ? defaultInFolder : Path.Combine(this._baseDirectory, "license.hnmo");
    }
}
