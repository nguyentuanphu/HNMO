// <copyright file="ItemSerials.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>
/// The database side of HNMO's item serials: every player item gets an origin serial once, and two live items with
/// one serial are a duplicate. The game logic reaches the database through <see cref="Database"/>, which the
/// database layer sets (none without a database, e.g. in the demo).
/// </summary>
public static class ItemSerials
{
    /// <summary>Gets the database access, or <c>null</c> when the server has no database.</summary>
    public static IItemSerialDatabase? Database { get; private set; }

    /// <summary>Sets the database access.</summary>
    /// <param name="database">The database access, or <c>null</c> to remove it (tests).</param>
    public static void Configure(IItemSerialDatabase? database) => Database = database;
}
