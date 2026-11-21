// <copyright file="MissionBoardEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Something that happened, which the missions window shows as a notice (in the chat).
/// </summary>
/// <param name="Type">"finished" (missions finished), "claim" (a mission claim), "streak" (the streak bonus claim),
/// "calendar" (the login calendar claim), "invite" (an invite code entered), "coins" (earned coins given).</param>
/// <param name="Result">The result, e.g. "Ok", "BagFull" (<see cref="MissionClaimResult"/> or <see cref="InviteResult"/>).</param>
/// <param name="Ids">The mission ids it is about.</param>
/// <param name="Coins">The coins it gave.</param>
/// <param name="Text">An extra text, e.g. the reason of earned coins.</param>
public sealed record MissionBoardEvent(string Type, string? Result = null, IReadOnlyList<string>? Ids = null, int Coins = 0, string? Text = null);
