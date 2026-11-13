// <copyright file="CastleSiegeTaxChangeResultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;
using NetworkTaxType = HNMO.Network.Packets.ServerToClient.CastleSiegeTaxType;
using TaxType = HNMO.GameLogic.CastleSiege.CastleSiegeTaxType;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeTaxChangeResultPlugIn"/>
/// which forwards tax-change results to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeTaxChangeResultPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeTaxChangeResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("CAC60298-A993-4F29-A936-B7D1BEFF2A4E")]
public class CastleSiegeTaxChangeResultPlugIn : ICastleSiegeTaxChangeResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeTaxChangeResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeTaxChangeResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowTaxChangeResultAsync(
        CastleSiegeRequestResult result,
        TaxType taxType,
        uint taxRate)
        => this._player.Connection?.SendCastleSiegeTaxChangeResponseAsync(
            (byte)result,
            (NetworkTaxType)taxType,
            taxRate) ?? default;

    /// <inheritdoc />
    public ValueTask ShowTaxRateUpdateAsync(TaxType taxType, byte taxRate)
        => this._player.Connection?.SendCastleSiegeTaxRateNotificationAsync(
            (NetworkTaxType)taxType,
            taxRate) ?? default;
}
