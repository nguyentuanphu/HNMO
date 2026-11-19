// <copyright file="CommunityPlayerFacts.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// What the state of a player is written from (besides the engine).
/// </summary>
/// <param name="Login">The login name.</param>
/// <param name="Character">The character name.</param>
/// <param name="DiscordLinkAvailable">Whether the game server hands out Discord link codes (the Discord integration is on).</param>
/// <param name="DiscordLinked">Whether the account is linked to a Discord user.</param>
public sealed record CommunityPlayerFacts(string Login, string Character, bool DiscordLinkAvailable, bool DiscordLinked);
