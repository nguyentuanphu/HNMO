// <copyright file="OnboardingBoardEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Something that happened, which the guide panel tells in the chat.
/// </summary>
/// <param name="Type">"step" (steps done), "final" (the guide finished), "kit" (the starter kit was given), "reward" (waiting
/// rewards were given), "skip" (the guide was skipped), "skipStep" (an optional step was skipped), "claim" (the take button).</param>
/// <param name="Result">The result, e.g. "Ok", "BagFull".</param>
/// <param name="Ids">The step ids it is about.</param>
/// <param name="Count">How many rewards were given.</param>
/// <param name="Coins">The coins it gave.</param>
public sealed record OnboardingBoardEvent(string Type, string? Result = null, IReadOnlyList<string>? Ids = null, int Count = 0, int Coins = 0);
