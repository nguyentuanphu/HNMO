// <copyright file="CommunityStateEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Something that happened, which the web game tells in the chat.
/// </summary>
/// <param name="Type">"discord", "zalo", "code", "goods" or "claim".</param>
/// <param name="Result">The result, e.g. "Ok", "AlreadyPaid", "NotLinked", "Unknown", "BagFull".</param>
/// <param name="Coins">The coins given.</param>
/// <param name="EventId">The community event.</param>
public sealed record CommunityStateEvent(string Type, string? Result = null, int Coins = 0, string? EventId = null);
