// <copyright file="IClientVersionProvider.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer;

using HNMO.Network.PlugIns;

/// <summary>
/// Interface for an object which can provide its client version.
/// </summary>
public interface IClientVersionProvider
{
    /// <summary>
    /// Occurs when the client version has been changed.
    /// </summary>
    event EventHandler ClientVersionChanged;

    /// <summary>
    /// Gets the client version.
    /// </summary>
    ClientVersion ClientVersion { get; }
}