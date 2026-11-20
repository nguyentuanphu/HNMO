// <copyright file="IServerRestartService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Requests a clean server and bridge restart after saving player data.
/// </summary>
public interface IServerRestartService
{
    /// <summary>
    /// Gets a value indicating whether restarting is supported in the current hosting mode.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Disconnects players cleanly, saves state, and exits the server so the launcher restarts HNMO.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous restart request.</returns>
    Task RequestRestartAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides license status inspection and updates for the admin panel.
/// </summary>
public interface IHostLicenseProvider
{
    /// <summary>
    /// Gets the active license status for the configured path.
    /// </summary>
    /// <param name="configuredPath">The configured license file path.</param>
    /// <returns>The license status summary.</returns>
    LicenseStatusInfo GetStatus(string? configuredPath);

    /// <summary>Checks a license file's content before it is saved.</summary>
    /// <param name="content">The file's content.</param>
    /// <returns>What it would give; <see cref="LicenseStatusInfo.IsValid"/> is false when it can't be used.</returns>
    LicenseStatusInfo Check(byte[] content) => new(10, "The license can't be checked here.", string.Empty, string.Empty, true);

    /// <summary>Applies the saved license to the running server (the players allowed at once), without a restart.</summary>
    /// <param name="configuredPath">The configured license file path.</param>
    /// <returns>The license now in use.</returns>
    LicenseStatusInfo Apply(string? configuredPath) => this.GetStatus(configuredPath);
}

/// <summary>
/// Updates the database user password when changed in host settings.
/// </summary>
public interface IDatabasePasswordUpdater
{
    /// <summary>
    /// Updates the PostgreSQL user password.
    /// </summary>
    /// <param name="settings">Current host settings.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous update.</returns>
    Task UpdatePasswordAsync(HostSettings settings, string newPassword, CancellationToken cancellationToken = default);
}
