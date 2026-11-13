// <copyright file="ShowSkillAnimationPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowSkillAnimationPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowSkillAnimationPlugIn075_Name), Description = nameof(PlugInResources.ShowSkillAnimationPlugIn075_Description), ResourceType = typeof(PlugInResources))]
[Guid("8DED7CDF-AB3E-4CCB-A817-604560120320")]
public class ShowSkillAnimationPlugIn075 : IShowSkillAnimationPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowSkillAnimationPlugIn075"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowSkillAnimationPlugIn075(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowSkillAnimationAsync(IAttacker attacker, IAttackable? target, Skill skill, bool effectApplied)
    {
        return this.ShowSkillAnimationAsync(attacker, target, skill.Number, effectApplied);
    }

    /// <inheritdoc/>
    public async ValueTask ShowSkillAnimationAsync(IAttacker attacker, IAttackable? target, short skillNumber, bool effectApplied)
    {
        var playerId = attacker.GetId(this._player);
        var targetId = target.GetId(this._player);
        var skillId = (byte)skillNumber;

        await this._player.Connection.SendSkillAnimation075Async(skillId, playerId, targetId, effectApplied).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask ShowComboAnimationAsync(IAttacker attacker, IAttackable? target)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ShowNovaStartAsync(IAttacker attacker)
    {
        return ValueTask.CompletedTask;
    }
}