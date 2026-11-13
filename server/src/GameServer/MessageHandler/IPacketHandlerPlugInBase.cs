// <copyright file="IPacketHandlerPlugInBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using HNMO.GameLogic;
using HNMO.PlugIns;

/// <summary>
/// Base interface for a packet handler plugin.
/// </summary>
public interface IPacketHandlerPlugInBase : IStrategyPlugIn<byte>
{
    /// <summary>
    /// Gets a value indicating whether this packet handler instance expects that the handled packet was checked to be encrypted.
    /// </summary>
    bool IsEncryptionExpected { get; }

    /// <summary>
    /// Handles the packet for the specified player.
    /// </summary>
    /// <param name="player">The player for which the packet should be handled.</param>
    /// <param name="packet">The packet which should be handled.</param>
    ValueTask HandlePacketAsync(Player player, Memory<byte> packet);
}