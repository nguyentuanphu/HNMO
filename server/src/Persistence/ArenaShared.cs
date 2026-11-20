// <copyright file="ArenaShared.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>The <see cref="IArenaShared"/> of this program; none without a database (the demo).</summary>
public static class ArenaShared
{
    /// <summary>Gets or sets the shared Arena data.</summary>
    public static IArenaShared? Current { get; set; }
}
