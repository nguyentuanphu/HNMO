// <copyright file="ShowAreaSkillAnimationPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowAreaSkillAnimationPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAreaSkillAnimationPlugIn_Name), Description = nameof(PlugInResources.ShowAreaSkillAnimationPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4cc09cdd-55a3-4191-94fc-b8e684b87cac")]
[MinimumClient(3, 0, ClientLanguage.Invariant)]
public class ShowAreaSkillAnimationPlugIn : IShowAreaSkillAnimationPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAreaSkillAnimationPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAreaSkillAnimationPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAreaSkillAnimationAsync(Player playerWhichPerformsSkill, Skill skill, Point point, byte rotation)
    {
        var skillId = NumberConversionExtensions.ToUnsigned(skill.Number);
        var playerId = playerWhichPerformsSkill.GetId(this._player);
        await this._player.Connection.SendAreaSkillAnimationAsync(skillId, playerId, point.X, point.Y, rotation).ConfigureAwait(false);
    }
}