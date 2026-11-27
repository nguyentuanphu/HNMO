// <copyright file="OnboardingTarget.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Where the current step leads: a spot on a map (an NPC, a spot to walk to, a map to enter).
/// </summary>
/// <param name="Map">The map number.</param>
/// <param name="X">The x coordinate.</param>
/// <param name="Y">The y coordinate.</param>
/// <param name="Name">The name of the NPC or place, if known.</param>
public sealed record OnboardingTarget(int Map, int X, int Y, string? Name);
