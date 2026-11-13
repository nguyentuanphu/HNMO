// <copyright file="RequestArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Common arguments for a request of a player to another player.
/// </summary>
public record RequestArguments(string Requester, string Receiver);