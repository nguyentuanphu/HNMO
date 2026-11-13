// <copyright file="GuildMessageArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for a guild chat message.
/// </summary>
public record GuildMessageArguments(uint GuildId, string Sender, string Message);