// <copyright file="CastleSiegeRegistrationStatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeRegistrationStatePlugIn"/>
/// which forwards registration state to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeRegistrationStatePlugIn_Name), Description = nameof(PlugInResources.CastleSiegeRegistrationStatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1CE1AF15-92BB-4022-BC71-189C125F4531")]
public class CastleSiegeRegistrationStatePlugIn : ICastleSiegeRegistrationStatePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeRegistrationStatePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeRegistrationStatePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowRegistrationStateAsync(
        CastleSiegeRegistrationStateResult result,
        string guildName,
        int marks,
        bool isGivingUp,
        byte registrationRank)
        => this._player.Connection.SendCastleSiegeRegistrationStateResponseAsync(
            (byte)result,
            guildName,
            checked((uint)marks),
            isGivingUp,
            registrationRank);
}
