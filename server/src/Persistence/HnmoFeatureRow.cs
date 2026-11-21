// <copyright file="HnmoFeatureRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// Single persisted row for per-account, per-character, or per-guild plug-in state.
/// </summary>
/// <param name="Feature">Logical feature key (e.g. <c>missions.account</c>, <c>accountSafety.account</c>).</param>
/// <param name="ScopeId">Account login, character ID, guild ID, or claim key.</param>
/// <param name="PayloadJson">Row JSON payload.</param>
/// <param name="UpdatedAtUtc">Last update timestamp (UTC).</param>
/// <param name="ExpiresAtUtc">Optional expiry/completion timestamp used for automated archival.</param>
public sealed record HnmoFeatureRow(
    string Feature,
    string ScopeId,
    string PayloadJson,
    DateTime UpdatedAtUtc,
    DateTime? ExpiresAtUtc = null);
