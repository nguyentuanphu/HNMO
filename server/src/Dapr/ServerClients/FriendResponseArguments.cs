// <copyright file="FriendResponseArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// The arguments for a friend response.
/// </summary>
public record FriendResponseArguments(string CharacterName, string FriendName, bool Accepted);