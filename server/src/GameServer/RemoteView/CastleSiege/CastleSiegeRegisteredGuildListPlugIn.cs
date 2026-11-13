// <copyright file="CastleSiegeRegisteredGuildListPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeRegisteredGuildListPlugIn"/>
/// which forwards the registered guild list to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeRegisteredGuildListPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeRegisteredGuildListPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7634BFFD-F6BF-400A-952F-81986A8B8A94")]
public class CastleSiegeRegisteredGuildListPlugIn : ICastleSiegeRegisteredGuildListPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeRegisteredGuildListPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeRegisteredGuildListPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowRegisteredGuildListAsync(
        IReadOnlyCollection<CastleSiegeGuildRegistration> registrations)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var registrationList = registrations.ToList();

        int Write()
        {
            var size = CastleSiegeRegisteredGuildListRef.GetRequiredSize(registrationList.Count);
            var packet = new CastleSiegeRegisteredGuildListRef(connection.Output.GetSpan(size)[..size])
            {
                Result = 1,
                GuildCount = checked((uint)registrationList.Count),
            };

            for (var i = 0; i < registrationList.Count; i++)
            {
                var registration = registrationList[i];
                var entry = packet[i];
                entry.GuildName = registration.GuildName;
                entry.GuildMarkCount = checked((uint)registration.Marks);
                entry.IsGivingUp = false;
                entry.SequenceNumber = (byte)Math.Clamp(registration.RegistrationOrder, 0, byte.MaxValue);
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
