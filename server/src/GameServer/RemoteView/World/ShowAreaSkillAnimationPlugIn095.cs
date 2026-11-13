// <copyright file="ShowAreaSkillAnimationPlugIn095.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowAreaSkillAnimationPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAreaSkillAnimationPlugIn095_Name), Description = nameof(PlugInResources.ShowAreaSkillAnimationPlugIn095_Description), ResourceType = typeof(PlugInResources))]
[Guid("553D9388-3648-4029-959E-F6D74399D51E")]
[MinimumClient(0, 95, ClientLanguage.Invariant)]
public class ShowAreaSkillAnimationPlugIn095 : IShowAreaSkillAnimationPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAreaSkillAnimationPlugIn095"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAreaSkillAnimationPlugIn095(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAreaSkillAnimationAsync(Player playerWhichPerformsSkill, Skill skill, Point point, byte rotation)
    {
        var skillId = (byte)skill.Number;
        var playerId = playerWhichPerformsSkill.GetId(this._player);
        await this._player.Connection.SendAreaSkillAnimation095Async(skillId, playerId, point.X, point.Y, rotation).ConfigureAwait(false);
    }
}