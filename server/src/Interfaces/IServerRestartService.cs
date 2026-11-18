// <copyright file="IServerRestartService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Summary of the active player license.
/// </summary>
/// <param name="MaximumPlayers">Maximum concurrent players allowed.</param>
/// <param name="Message">License status message.</param>
/// <param name="MachineId">This computer's machine identifier.</param>
/// <param name="ResolvedPath">Resolved path on disk.</param>
/// <param name="Exists">Whether a license file exists at the resolved path.</param>
public sealed record LicenseStatusInfo(
    int MaximumPlayers,
    string Message,
    string MachineId,
    string ResolvedPath,
    bool Exists);

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
