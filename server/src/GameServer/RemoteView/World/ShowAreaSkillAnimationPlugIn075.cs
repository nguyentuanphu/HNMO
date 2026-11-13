// <copyright file="ShowAreaSkillAnimationPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowAreaSkillAnimationPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAreaSkillAnimationPlugIn075_Name), Description = nameof(PlugInResources.ShowAreaSkillAnimationPlugIn075_Description), ResourceType = typeof(PlugInResources))]
[Guid("DB239A54-A5BD-4796-A96C-50D247D288F3")]
public class ShowAreaSkillAnimationPlugIn075 : IShowAreaSkillAnimationPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAreaSkillAnimationPlugIn075"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAreaSkillAnimationPlugIn075(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAreaSkillAnimationAsync(Player playerWhichPerformsSkill, Skill skill, Point point, byte rotation)
    {
        var skillId = (byte)skill.Number;
        var playerId = playerWhichPerformsSkill.GetId(this._player);
        await this._player.Connection.SendAreaSkillAnimation075Async(skillId, playerId, point.X, point.Y, rotation).ConfigureAwait(false);
    }
}