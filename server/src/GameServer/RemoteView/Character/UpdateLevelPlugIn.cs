// <copyright file="UpdateLevelPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Properties;
using HNMO.GameLogic.Views.Character;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;
using PlugInResources = HNMO.GameServer.Properties.PlugInResources;

/// <summary>
/// The default implementation of the <see cref="IUpdateLevelPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.UpdateLevelPlugIn_Name), Description = nameof(PlugInResources.UpdateLevelPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1ff3709e-d99b-4c00-b926-efce281b3997")]
public class UpdateLevelPlugIn : IUpdateLevelPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateLevelPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public UpdateLevelPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask UpdateLevelAsync()
    {
        var selectedCharacter = this._player.SelectedCharacter;
        var charStats = this._player.Attributes;
        var connection = this._player.Connection;
        if (selectedCharacter is null || charStats is null || connection is null)
        {
            return;
        }

        await connection.SendCharacterLevelUpdateAsync(
            (ushort)charStats[Stats.Level],
            (ushort)Math.Max(selectedCharacter.LevelUpPoints, 0),
            charStats[Stats.MaximumHealth].ToUInt16Clamped(),
            charStats[Stats.MaximumMana].ToUInt16Clamped(),
            charStats[Stats.MaximumShield].ToUInt16Clamped(),
            charStats[Stats.MaximumAbility].ToUInt16Clamped(),
            (ushort)selectedCharacter.UsedFruitPoints,
            selectedCharacter.GetMaximumFruitPoints(),
            (ushort)selectedCharacter.UsedNegFruitPoints,
            selectedCharacter.GetMaximumFruitPoints()).ConfigureAwait(false);

        await this._player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.LevelUpCongrats), charStats[Stats.Level]).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UpdateMasterLevelAsync()
    {
        var selectedCharacter = this._player.SelectedCharacter;
        var charStats = this._player.Attributes;
        var connection = this._player.Connection;
        if (selectedCharacter is null || charStats is null || connection is null)
        {
            return;
        }

        await connection.SendMasterCharacterLevelUpdateAsync(
            (ushort)charStats[Stats.MasterLevel],
            (ushort)charStats[Stats.MasterPointsPerLevelUp],
            (ushort)selectedCharacter.MasterLevelUpPoints,
            (ushort)this._player.GameContext.Configuration.MaximumMasterLevel,
            charStats[Stats.MaximumHealth].ToUInt16Clamped(),
            charStats[Stats.MaximumMana].ToUInt16Clamped(),
            charStats[Stats.MaximumShield].ToUInt16Clamped(),
            charStats[Stats.MaximumAbility].ToUInt16Clamped()).ConfigureAwait(false);

        await this._player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MasterLevelUpCongrats), charStats[Stats.MasterLevel]).ConfigureAwait(false);
    }
}