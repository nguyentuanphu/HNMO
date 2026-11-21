// <copyright file="GuildStateEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Something that happened, which the web game tells in the chat.
/// </summary>
/// <param name="Type">"levelup" (the guild reached a level), "ladder" (a ladder reward was given), "siege" (the guild won the castle siege), "claim" (the window's button).</param>
/// <param name="Result">The result, e.g. "Ok", "BagFull" or "Nothing".</param>
/// <param name="Level">The new level.</param>
/// <param name="Rank">The ladder rank.</param>
/// <param name="Coins">The coins given.</param>
/// <param name="Week">The week of a ladder reward.</param>
/// <param name="InGame">Whether the server's system message told it already (the web game then adds no line).</param>
public sealed record GuildStateEvent(string Type, string? Result = null, int Level = 0, int Rank = 0, int Coins = 0, string? Week = null, bool InGame = false);
