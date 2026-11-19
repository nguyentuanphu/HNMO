// <copyright file="WebsiteMissionSubject.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>An account/character pair authenticated by the website API, without forwarding its password.</summary>
/// <param name="ConfigurationId">The expected game configuration, preventing cross-world reads.</param>
/// <param name="AccountId">The authenticated account identifier.</param>
/// <param name="CharacterId">The owned character identifier.</param>
/// <param name="LoginName">The canonical account name used by mission state.</param>
public sealed record WebsiteMissionSubject(Guid ConfigurationId, Guid AccountId, Guid CharacterId, string LoginName);
