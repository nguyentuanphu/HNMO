// <copyright file="ShowShopCurrenciesExtendedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.PlayerShop;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Views.PlayerShop;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The extended implementation of the <see cref="IShowShopCurrenciesPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowShopCurrenciesExtendedPlugIn_Name), Description = nameof(PlugInResources.ShowShopCurrenciesExtendedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ED74E1F8-85EC-42E8-BC03-07CB7221E767")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowShopCurrenciesExtendedPlugIn : IShowShopCurrenciesPlugIn
{
    /// <summary>
    /// The maximum number of price types which fit into the message, which has a length of at most 255 bytes.
    /// </summary>
    private const int MaximumPriceTypes = (byte.MaxValue - 5) / 2;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowShopCurrenciesExtendedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowShopCurrenciesExtendedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowShopCurrenciesAsync(IReadOnlyCollection<ItemDefinition> priceItems, IReadOnlyCollection<CashShopCoinType> coinTypes)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var priceItemTypes = priceItems.Select(PlayerShopPriceEncoding.GetPriceItemType)
            .Concat(coinTypes.Select(PlayerShopPriceEncoding.GetPriceItemType))
            .Distinct()
            .Take(MaximumPriceTypes)
            .ToList();

        int Write()
        {
            var size = PlayerShopPriceTypesExtendedRef.GetRequiredSize(priceItemTypes.Count);
            var packet = new PlayerShopPriceTypesExtendedRef(connection.Output.GetSpan(size)[..size])
            {
                Count = (byte)priceItemTypes.Count,
            };

            for (var i = 0; i < priceItemTypes.Count; i++)
            {
                var priceType = packet[i];
                priceType.PriceItemType = priceItemTypes[i];
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
