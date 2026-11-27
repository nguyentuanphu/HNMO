// <copyright file="OnboardingFacts.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// What the server knows of a character in the game, for the steps it checks by looking (not by an event).
/// </summary>
/// <param name="Level">The level.</param>
/// <param name="Map">The map number.</param>
/// <param name="X">The x coordinate.</param>
/// <param name="Y">The y coordinate.</param>
/// <param name="InParty">Whether it's in a party.</param>
/// <param name="StatPoints">The sum of the base stats (strength, agility, vitality, energy, command).</param>
/// <param name="LearnedSkills">The number of learned skills.</param>
/// <param name="CashShopOpen">Whether the cash shop is open.</param>
public sealed record OnboardingFacts(int Level, int Map, int X, int Y, bool InParty, int StatPoints, int LearnedSkills, bool CashShopOpen);
