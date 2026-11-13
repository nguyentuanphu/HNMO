// <copyright file="DeActivateMagicEffectPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IActivateMagicEffectPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DeActivateMagicEffectPlugIn075_Name), Description = nameof(PlugInResources.DeActivateMagicEffectPlugIn075_Description), ResourceType = typeof(PlugInResources))]
[Guid("3CF8BFBA-FBDA-431D-9806-86B3DEC3AD54")]
[MaximumClient(0, 89, ClientLanguage.Invariant)]
public class DeActivateMagicEffectPlugIn075 : IDeactivateMagicEffectPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeActivateMagicEffectPlugIn075"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public DeActivateMagicEffectPlugIn075(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask DeactivateMagicEffectAsync(MagicEffect effect, IAttackable affectedObject)
    {
        if (effect.Definition.Number <= 0)
        {
            return;
        }

        // We assume, that the magic effect number is equal to the skill number.
        // In early versions, the magic effect number is not used elsewhere.
        await this._player.Connection.SendMagicEffectCancelled075Async((byte)effect.Definition.Number, affectedObject.GetId(this._player)).ConfigureAwait(false);
    }
}