// <copyright file="CastleSiegeRegistrationHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.CastleSiege.Actions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles Castle Siege guild registration requests.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeRegistrationHandlerPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeRegistrationHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7315C1FD-EE79-490E-B9ED-E0ECC6E87F37")]
[BelongsToGroup(CastleSiegeGroupHandlerPlugIn.GroupKey)]
internal class CastleSiegeRegistrationHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly CastleSiegeRegisterGuildAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CastleSiegeRegistrationRequest.SubCode;

    /// <inheritdoc/>
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        return this._action.RegisterAsync(player, CastleSiegeHandlerContext.Get(player));
    }
}
