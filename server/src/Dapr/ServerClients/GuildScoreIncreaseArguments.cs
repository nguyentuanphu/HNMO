// <copyright file="GuildScoreIncreaseArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for increasing a guild's score.
/// </summary>
public record GuildScoreIncreaseArguments(uint GuildId, int Amount);
