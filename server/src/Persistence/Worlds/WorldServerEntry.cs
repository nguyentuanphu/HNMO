// <copyright file="WorldServerEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

/// <summary>
/// Public server entry grouped inside a <see cref="WorldPublicSummary"/>.
/// </summary>
public sealed class WorldServerEntry
{
    /// <summary>Gets or sets the server identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the current player count.</summary>
    public int Players { get; set; }

    /// <summary>Gets or sets the player capacity.</summary>
    public int Capacity { get; set; }

    /// <summary>Gets or sets a value indicating whether this server is currently started.</summary>
    public bool IsRunning { get; set; }
}
