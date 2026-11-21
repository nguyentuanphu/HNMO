// <copyright file="ChatSpamGuard.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

/// <summary>
/// Remembers the recent chat lines of one player and tells whether a new line is spam.
/// Only the lines which got through count, so a player who is held back can chat again once the time has passed.
/// </summary>
public sealed class ChatSpamGuard
{
    private readonly object _lock = new();
    private readonly Queue<(DateTime Time, string Text)> _recent = new();

    /// <summary>
    /// Checks a new line and remembers it, if it's not spam.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="limits">The limits.</param>
    /// <returns><c>true</c>, if the line may be sent; <c>false</c>, if it's spam and is dropped.</returns>
    public bool TryAccept(string text, DateTime nowUtc, ChatSpamLimits limits)
    {
        var normalized = Normalize(text);
        lock (this._lock)
        {
            var keep = limits.RepeatWindow > limits.LineWindow ? limits.RepeatWindow : limits.LineWindow;
            while (this._recent.Count > 0 && nowUtc - this._recent.Peek().Time > keep)
            {
                this._recent.Dequeue();
            }

            if (limits.MaximumLines > 0 && this._recent.Count(r => nowUtc - r.Time <= limits.LineWindow) >= limits.MaximumLines)
            {
                return false;
            }

            if (limits.MaximumRepeats > 0 && this._recent.Count(r => nowUtc - r.Time <= limits.RepeatWindow && r.Text == normalized) >= limits.MaximumRepeats)
            {
                return false;
            }

            this._recent.Enqueue((nowUtc, normalized));
            return true;
        }
    }

    private static string Normalize(string text) => text.Trim().ToLowerInvariant();
}
