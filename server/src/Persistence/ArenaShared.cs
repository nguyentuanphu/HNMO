// <copyright file="ArenaShared.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Persistence;

/// <summary>The <see cref="IArenaShared"/> of this program; none without a database (the demo).</summary>
public static class ArenaShared
{
    /// <summary>Gets or sets the shared Arena data.</summary>
    public static IArenaShared? Current { get; set; }
}
