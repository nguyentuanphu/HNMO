// <copyright file="KillMoney.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.NPC;

/// <summary>
/// The experience a monster's zen drop is based on.
/// </summary>
public static class KillMoney
{
    /// <summary>
    /// Gets the experience the zen of a kill is based on: the experience the killers got, without the server's
    /// experience rate. The zen dropped is this amount plus a small base, then times the zen multipliers.
    /// </summary>
    /// <param name="gainedExperience">The experience the killer (or the party) got for the kill.</param>
    /// <param name="serverExperienceRate">The server's experience rate (<see cref="IGameContext.ExperienceRate"/>).</param>
    /// <returns>The experience without the server's rate.</returns>
    public static int ExperienceForMoney(int gainedExperience, float serverExperienceRate)
        => serverExperienceRate > 0 && gainedExperience > 0
            ? (int)Math.Round(gainedExperience / (double)serverExperienceRate)
            : gainedExperience;
}
