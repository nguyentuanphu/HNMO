// <copyright file="WorldBossBoardEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Something that happened, which the web game tells in the chat.
/// </summary>
/// <param name="Type">"soon", "spawn", "kill", "escape" (announcements), "reward" (rewards given or waiting), "claim" (the window's button).</param>
/// <param name="Result">The result, e.g. "Ok", "BagFull" or "Nothing".</param>
/// <param name="BossId">The boss it is about.</param>
/// <param name="TextEn">The English text of an announcement.</param>
/// <param name="TextVi">The Vietnamese text of an announcement.</param>
/// <param name="Count">The rewards given.</param>
public sealed record WorldBossBoardEvent(string Type, string? Result = null, string? BossId = null, string? TextEn = null, string? TextVi = null, int Count = 0, bool InGame = false);
