// <copyright file="IGameClientVersion.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// Defines the game client (binary) version which is supposed to connect.
/// </summary>
public interface IGameClientVersion
{
    /// <summary>
    /// Gets the description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the season.
    /// </summary>
    byte Season { get; }

    /// <summary>
    /// Gets the episode.
    /// </summary>
    byte Episode { get; }

    /// <summary>
    /// Gets the version which is defined in the client binaries.
    /// </summary>
    byte[] Version { get; }

    /// <summary>
    /// Gets the serial which is defined in the client binaries.
    /// </summary>
    byte[] Serial { get; }
}