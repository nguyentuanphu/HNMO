// <copyright file="GuildCreationArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for a guild creation.
/// </summary>
public record GuildCreationArguments(string Name, string MasterName, Guid MasterId, byte[] Logo, byte ServerId);