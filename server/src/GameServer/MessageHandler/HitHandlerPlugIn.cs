// <copyright file="HitHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for hit packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HitHandlerPlugIn_Name), Description = nameof(PlugInResources.HitHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("698b8db9-472a-42dd-bdfe-f6b4ba45595e")]
[MinimumClient(1, 0, ClientLanguage.English)]
internal class HitHandlerPlugIn : HitHandlerPlugInBase
{
    /// <inheritdoc/>
    public override byte Key => HitRequest.Code;
}