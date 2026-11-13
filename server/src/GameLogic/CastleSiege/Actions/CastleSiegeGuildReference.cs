// <copyright file="CastleSiegeGuildReference.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.CastleSiege.Actions;

/// <summary>
/// A Castle Siege guild identity spanning runtime and persistent identifiers.
/// </summary>
/// <param name="RuntimeId">The runtime guild identifier.</param>
/// <param name="PersistentId">The persistent guild identifier.</param>
/// <param name="Name">The guild name.</param>
internal sealed record CastleSiegeGuildReference(uint RuntimeId, Guid PersistentId, string Name);
