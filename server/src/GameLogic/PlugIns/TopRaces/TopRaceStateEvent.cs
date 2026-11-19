// <copyright file="TopRaceStateEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Something that happened, which the web game tells in the chat.
/// </summary>
/// <param name="Type">"start" (a race started), "end" (a race ended), "prize" (a prize was given), "goods" (waiting prizes given), "claim" (the window's button).</param>
/// <param name="Result">The result, e.g. "Ok", "BagFull" or "Nothing".</param>
/// <param name="RaceId">The race.</param>
/// <param name="TextEn">The English notice.</param>
/// <param name="TextVi">The Vietnamese notice.</param>
/// <param name="Rank">The rank of a prize.</param>
/// <param name="Coins">The coins of a prize.</param>
/// <param name="InGame">Whether the server's golden notice told it already (the web game then adds no line of its own).</param>
public sealed record TopRaceStateEvent(string Type, string? Result = null, string? RaceId = null, string? TextEn = null, string? TextVi = null, int Rank = 0, int Coins = 0, bool InGame = false);
