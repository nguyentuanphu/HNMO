// <copyright file="OnboardingAdvance.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The steps which were done by a change, and whether the guide was finished by it.
/// </summary>
/// <param name="Completed">The steps done.</param>
/// <param name="Finished">Whether the last step was done (the final reward waits).</param>
public sealed record OnboardingAdvance(IReadOnlyList<OnboardingStep> Completed, bool Finished)
{
    /// <summary>Gets nothing done.</summary>
    public static OnboardingAdvance None { get; } = new([], false);

    /// <summary>Gets a value indicating whether anything happened.</summary>
    public bool Any => this.Completed.Count > 0 || this.Finished;
}
