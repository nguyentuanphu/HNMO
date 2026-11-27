// <copyright file="HnmoAutoErrorReportHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.BugReports;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoAutoErrorReport"/> (<c>F5 60</c>): an error the web client met and reported on its own. It is
/// grouped for the server's owner (admin panel, Bug reports, Automatic); the client gets no answer.
/// </summary>
[PlugIn]
[Display(Name = "HNMO automatic error report handler", Description = "Keeps the web client's automatic error reports for the server's owner.")]
[Guid("3B7E5D29-9C41-4A6F-8E12-D0F4A7C35B68")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoAutoErrorReportHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private static readonly HnmoPacketRateLimiter IngressLimit = new(burst: 5, perSecond: 0.2);

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoAutoErrorReport.SubCode;

    /// <inheritdoc />
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.Account is not null
            && BugReportsPlugIn.Get(player.GameContext) is { } plugIn
            && IngressLimit.TryTake(player)
            && AutoErrorPayload.TryReadPacket(packet, out var fingerprint, out var sample))
        {
            plugIn.SubmitAutomatic(player, fingerprint, sample);
        }

        return ValueTask.CompletedTask;
    }
}
