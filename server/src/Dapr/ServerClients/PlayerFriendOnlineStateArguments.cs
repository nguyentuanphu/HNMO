// <copyright file="PlayerFriendOnlineStateArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for a friend online state change.
/// </summary>
public record PlayerFriendOnlineStateArguments(Guid CharacterId, string CharacterName, byte ServerId, bool IsVisible);