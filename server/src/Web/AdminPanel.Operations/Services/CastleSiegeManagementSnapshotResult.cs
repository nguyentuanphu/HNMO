// <copyright file="CastleSiegeManagementSnapshotResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using HNMO.GameLogic.CastleSiege;

/// <summary>
/// The result of a Castle Siege status request.
/// </summary>
/// <param name="Snapshot">The status snapshot, when available.</param>
/// <param name="Error">The error when no snapshot is available.</param>
public sealed record CastleSiegeManagementSnapshotResult(CastleSiegeAdministrationSnapshot? Snapshot, CastleSiegeAdministrationError Error);
