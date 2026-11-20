// <copyright file="LicenseStatusInfo.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// Summary of the active player license.
/// </summary>
/// <param name="MaximumPlayers">Maximum concurrent players allowed.</param>
/// <param name="Message">License status message.</param>
/// <param name="MachineId">This computer's machine identifier.</param>
/// <param name="ResolvedPath">Resolved path on disk.</param>
/// <param name="Exists">Whether a license file exists at the resolved path.</param>
/// <param name="IsValid">Whether the license's signature, date and computer check out.</param>
/// <param name="Licensee">Whom the license is for.</param>
/// <param name="Expires">The last day it is valid, if it has one.</param>
public sealed record LicenseStatusInfo(
    int MaximumPlayers,
    string Message,
    string MachineId,
    string ResolvedPath,
    bool Exists,
    bool IsValid = false,
    string? Licensee = null,
    DateOnly? Expires = null);
