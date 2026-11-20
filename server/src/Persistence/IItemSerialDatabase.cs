// <copyright file="IItemSerialDatabase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>Reads and gives item serials in the database.</summary>
public interface IItemSerialDatabase
{
    /// <summary>
    /// Gives a new serial to every player item which has none. Items of the NPC merchants and quest reward templates
    /// never get one: what's bought or rewarded is a copy of them.
    /// </summary>
    /// <returns>How many items got a serial.</returns>
    int AssignMissing();

    /// <summary>Finds every item which shares its serial with another item.</summary>
    /// <returns>The items, ordered by serial.</returns>
    IReadOnlyList<DuplicateItemRow> FindDuplicates();
}
