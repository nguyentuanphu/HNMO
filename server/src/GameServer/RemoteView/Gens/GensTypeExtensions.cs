// <copyright file="GensTypeExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Gens;

using GensType = HNMO.DataModel.Entities.GensType;
using PacketGensType = HNMO.Network.Packets.ServerToClient.GensType;

/// <summary>
/// Extensions to convert the <see cref="GensType"/> into the one of the packets.
/// </summary>
internal static class GensTypeExtensions
{
    /// <summary>
    /// Converts the gens type into the one of the packets.
    /// </summary>
    /// <param name="gens">The gens type.</param>
    /// <returns>The gens type of the packets.</returns>
    public static PacketGensType ToPacketGensType(this GensType gens)
    {
        return gens switch
        {
            GensType.Duprian => PacketGensType.Duprian,
            GensType.Vanert => PacketGensType.Vanert,
            _ => PacketGensType.Undefined,
        };
    }
}
