// <copyright file="ConsumeSpecialItemPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IConsumeSpecialItemPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ConsumeSpecialItemPlugIn_Name), Description = nameof(PlugInResources.ConsumeSpecialItemPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("a31546d8-bf79-43dd-872c-52f24ea9bca9")]
public class ConsumeSpecialItemPlugIn : IConsumeSpecialItemPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumeSpecialItemPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ConsumeSpecialItemPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ConsumeSpecialItemAsync(Item item, ushort effectTimeInSeconds)
    {
        if (item.Definition is null)
        {
            return;
        }

        var itemIdentifier = new ItemIdentifier(item.Definition.Number, item.Definition.Group);
        if (itemIdentifier == ItemConstants.Alcohol)
        {
            await this._player.Connection.SendConsumeItemWithEffectAsync(ConsumeItemWithEffect.ConsumedItemType.Ale, effectTimeInSeconds).ConfigureAwait(false);
        }
        else if (itemIdentifier == ItemConstants.SiegePotion && item.Level == 1)
        {
            await this._player.Connection.SendConsumeItemWithEffectAsync(ConsumeItemWithEffect.ConsumedItemType.PotionOfSoul, effectTimeInSeconds).ConfigureAwait(false);
        }
        else
        {
            // do nothing
        }
    }
}