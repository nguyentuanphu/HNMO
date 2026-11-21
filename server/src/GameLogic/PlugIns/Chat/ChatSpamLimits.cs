// <copyright file="ChatSpamLimits.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

/// <summary>
/// The limits of the <see cref="ChatSpamGuard"/>.
/// </summary>
/// <param name="MaximumRepeats">How often the same text may be sent within <paramref name="RepeatWindow"/>.</param>
/// <param name="RepeatWindow">The time of <paramref name="MaximumRepeats"/>.</param>
/// <param name="MaximumLines">How many lines may be sent within <paramref name="LineWindow"/>.</param>
/// <param name="LineWindow">The time of <paramref name="MaximumLines"/>.</param>
public readonly record struct ChatSpamLimits(int MaximumRepeats, TimeSpan RepeatWindow, int MaximumLines, TimeSpan LineWindow)
{
    /// <summary>
    /// Gets the default limits: the same text at most 3 times within 10 seconds, at most 8 lines within 5 seconds.
    /// </summary>
    public static ChatSpamLimits Default { get; } = new(3, TimeSpan.FromSeconds(10), 8, TimeSpan.FromSeconds(5));
}
