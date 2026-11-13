// <copyright file="CastleSiegeTributeWithdrawHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.CastleSiege.Actions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles Castle Siege treasury withdrawal requests.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeTributeWithdrawHandlerPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeTributeWithdrawHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("79B23122-5450-4CEB-AA6F-9FD879BCCB0C")]
[BelongsToGroup(CastleSiegeGroupHandlerPlugIn.GroupKey)]
internal sealed class CastleSiegeTributeWithdrawHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private const short SeniorNumber = 223;
    private readonly CastleSiegeTributeWithdrawAction _action = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => CastleSiegeTaxMoneyWithdraw.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < CastleSiegeTaxMoneyWithdraw.Length
            || player.OpenedNpc?.Definition.Number != SeniorNumber)
        {
            return;
        }

        var request = new CastleSiegeTaxMoneyWithdraw(packet);
        _ = await this._action.WithdrawAsync(
                player,
                CastleSiegeHandlerContext.Get(player),
                request.Amount)
            .ConfigureAwait(false);
    }
}
