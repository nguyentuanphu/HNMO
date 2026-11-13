// <copyright file="PetBehaviourChangedViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Pet;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Pet;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the chat view which is forwarding everything to the game client which specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PetBehaviourChangedViewPlugIn_Name), Description = nameof(PlugInResources.PetBehaviourChangedViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7402B7AA-1DCA-437A-A5D1-1B54C54EC0FC")]
internal class PetBehaviourChangedViewPlugIn : IPetBehaviourChangedViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetBehaviourChangedViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public PetBehaviourChangedViewPlugIn(RemotePlayer player)
    {
        this._player = player;
    }

    /// <inheritdoc />
    public async ValueTask PetBehaviourChangedAsync(Item pet, PetBehaviour behaviour, IAttackable? target)
    {
        await this._player.Connection.SendPetModeAsync(Convert(behaviour), target?.GetId(this._player) ?? 0xFFFF).ConfigureAwait(false);
    }

    private static PetCommandMode Convert(PetBehaviour attackType)
    {
        switch (attackType)
        {
            case PetBehaviour.AttackRandom: return PetCommandMode.AttackRandom;
            case PetBehaviour.AttackWithOwner: return PetCommandMode.AttackWithOwner;
            case PetBehaviour.AttackTarget: return PetCommandMode.AttackTarget;
            case PetBehaviour.Idle: return PetCommandMode.Normal;
            default: throw new ArgumentException($"Unknown pet attack type: {attackType}");
        }
    }
}