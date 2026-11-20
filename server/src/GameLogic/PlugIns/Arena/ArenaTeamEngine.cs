// <copyright file="ArenaTeamEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>
/// The rules of the Arena's 5v5 team battles, without the game (owner's choice, 2026-11-20): a queue of groups (one
/// player who queued alone, or a party of up to five that queued together) is matched into two balanced teams; a battle
/// counts kills on the Valley of Loren map, the fallen come back at their gate, and the most kills in ten minutes (or
/// thirty kills first) wins. This class forms the teams and scores a battle; the game moves and respawns the players.
/// </summary>
public static class ArenaTeamEngine
{
    /// <summary>
    /// Forms two teams from the queue, when it can: full teams first, or smaller even teams (down to the minimum) once
    /// the front group has waited long enough (<paramref name="frontWaitedLong"/>). Parties are kept together; a party
    /// larger than a team is skipped. The chosen groups are removed from <paramref name="queue"/>.
    /// </summary>
    /// <param name="queue">The waiting groups (changed: the chosen ones are removed).</param>
    /// <param name="teamSize">The full team size.</param>
    /// <param name="minSize">The fewest players a team may start with.</param>
    /// <param name="frontWaitedLong">Whether the earliest group has waited long enough to accept a smaller battle.</param>
    /// <returns>The match, or <c>null</c> when none can be formed yet.</returns>
    public static Match? FormTeams(List<Group> queue, int teamSize, int minSize, bool frontWaitedLong)
    {
        ArgumentNullException.ThrowIfNull(queue);
        var usable = queue.Where(g => g.Logins.Count is > 0 && g.Logins.Count <= teamSize).OrderBy(g => g.JoinOrder).ToList();
        var waiting = usable.Sum(g => g.Logins.Count);
        if (waiting < minSize * 2)
        {
            return null;
        }

        // The size to aim for: a full 5v5 when enough wait, else the biggest even battle the queue allows, but only down
        // to the minimum, and only below full when the front has waited long enough.
        var target = Math.Min(teamSize, waiting / 2);
        if (target < teamSize && !frontWaitedLong)
        {
            return null;
        }

        if (target < minSize)
        {
            return null;
        }

        // Fill two teams by taking groups in turn, each onto the team with fewer players (and the lower total rating on a
        // tie), so parties stay together and the sides stay even and balanced.
        List<Group> a = [], b = [];
        int aCount = 0, bCount = 0;
        foreach (var group in usable)
        {
            var size = group.Logins.Count;
            var toA = aCount <= bCount;
            if (toA && aCount + size <= target)
            {
                a.Add(group);
                aCount += size;
            }
            else if (!toA && bCount + size <= target)
            {
                b.Add(group);
                bCount += size;
            }
            else if (aCount + size <= target)
            {
                a.Add(group);
                aCount += size;
            }
            else if (bCount + size <= target)
            {
                b.Add(group);
                bCount += size;
            }

            if (aCount >= target && bCount >= target)
            {
                break;
            }
        }

        if (aCount < minSize || bCount < minSize || aCount != bCount)
        {
            return null;
        }

        foreach (var group in a.Concat(b))
        {
            queue.Remove(group);
        }

        return new Match(a.SelectMany(g => g.Logins).ToList(), b.SelectMany(g => g.Logins).ToList());
    }

    /// <summary>Whether a battle is over, and which side won.</summary>
    /// <param name="scoreA">Team A's kills.</param>
    /// <param name="scoreB">Team B's kills.</param>
    /// <param name="killsToWin">The kills that end it at once.</param>
    /// <param name="timeUp">Whether the time ran out.</param>
    /// <returns>Over, and the winner: 1 for A, -1 for B, 0 for a draw; null when not over.</returns>
    public static (bool Over, int Winner)? Outcome(int scoreA, int scoreB, int killsToWin, bool timeUp)
    {
        if (scoreA >= killsToWin || scoreB >= killsToWin || timeUp)
        {
            return (true, Math.Sign(scoreA - scoreB));
        }

        return null;
    }

    /// <summary>A group in the queue: the logins that queued together (one alone, or a party), with a joined order and rating.</summary>
    /// <param name="Logins">The accounts' login names.</param>
    /// <param name="JoinOrder">The order it joined (lower is earlier).</param>
    /// <param name="Rating">The group's average ranked rating, for balancing.</param>
    public sealed record Group(IReadOnlyList<string> Logins, long JoinOrder, int Rating);

    /// <summary>Two teams ready to fight.</summary>
    /// <param name="TeamA">One team's logins.</param>
    /// <param name="TeamB">The other team's logins.</param>
    public sealed record Match(IReadOnlyList<string> TeamA, IReadOnlyList<string> TeamB);
}
