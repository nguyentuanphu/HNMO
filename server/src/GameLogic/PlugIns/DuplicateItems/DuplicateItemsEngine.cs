// <copyright file="DuplicateItemsEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.DuplicateItems;

using HNMO.Persistence;

/// <summary>
/// Turns what the scan found into cases (pure, no database).
/// </summary>
public static class DuplicateItemsEngine
{
    /// <summary>
    /// Records the duplicates of a scan: a serial seen for the first time is a new open case; an open case gets the
    /// items as found now; a case an administrator decided stays decided (the items stay as they were allowed).
    /// </summary>
    /// <param name="data">The stored data (changed).</param>
    /// <param name="rows">The duplicate items the scan found.</param>
    /// <param name="utcNow">The time of the scan.</param>
    /// <returns>The cases which are new.</returns>
    public static IReadOnlyList<DuplicateItemCase> Record(DuplicateItemsData data, IReadOnlyList<DuplicateItemRow> rows, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(rows);
        var added = new List<DuplicateItemCase>();
        foreach (var group in rows.GroupBy(r => r.Serial))
        {
            var entries = group.Select(r => new DuplicateItemCaseEntry
            {
                ItemId = r.ItemId,
                Place = r.Place,
                CharacterName = r.CharacterName ?? string.Empty,
                AccountLogin = r.AccountLogin ?? string.Empty,
                ItemName = r.ItemName,
                Level = r.Level,
            }).ToList();
            if (entries.Count < 2)
            {
                continue;
            }

            var existing = data.Cases.FirstOrDefault(c => c.Serial == group.Key);
            if (existing is null)
            {
                var found = new DuplicateItemCase { Serial = group.Key, FoundAtUtc = utcNow, Items = entries };
                data.Cases.Add(found);
                added.Add(found);
            }
            else if (!existing.Resolved)
            {
                existing.Items = entries;
            }
        }

        data.LastScanUtc = utcNow;
        return added;
    }

    /// <summary>Gets the serials which are frozen: those of the open cases.</summary>
    /// <param name="data">The stored data.</param>
    /// <returns>The serials.</returns>
    public static HashSet<Guid> FrozenSerials(DuplicateItemsData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Cases.Where(c => !c.Resolved).Select(c => c.Serial).ToHashSet();
    }

    /// <summary>Marks a case decided: its items aren't frozen any more.</summary>
    /// <param name="data">The stored data (changed).</param>
    /// <param name="serial">The case's serial.</param>
    /// <param name="by">Who decided.</param>
    /// <param name="note">The note.</param>
    /// <param name="utcNow">The time.</param>
    /// <returns><c>true</c>, if an open case was decided.</returns>
    public static bool Resolve(DuplicateItemsData data, Guid serial, string by, string note, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Cases.FirstOrDefault(c => c.Serial == serial && !c.Resolved) is not { } open)
        {
            return false;
        }

        open.Resolved = true;
        open.ResolvedAtUtc = utcNow;
        open.ResolvedBy = by ?? string.Empty;
        open.Note = note ?? string.Empty;
        return true;
    }
}
