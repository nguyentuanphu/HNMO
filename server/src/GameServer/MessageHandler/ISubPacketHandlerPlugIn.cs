// <copyright file="ISubPacketHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Interface for a sub-packet handler plugin.
/// </summary>
[CustomPlugInContainer("Sub-Packet Handlers", "Plugin Container for sub packet handlers which belong to a group.")]
[Guid("FE1F03E3-5214-4C99-B537-FCC0555EAB06")]
public interface ISubPacketHandlerPlugIn : IPacketHandlerPlugInBase
{
}