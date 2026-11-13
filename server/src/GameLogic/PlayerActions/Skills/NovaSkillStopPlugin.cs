// <copyright file="NovaSkillStopPlugin.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// The stop nova skill action.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.NovaSkillStopPlugin_Name), Description = nameof(PlugInResources.NovaSkillStopPlugin_Description), ResourceType = typeof(PlugInResources))]
[Guid("3cb98892-b3ce-42de-8956-5ed5625c6285")]
public class NovaSkillStopPlugin : TargetedSkillPluginBase
{
    /// <inheritdoc/>
    public override short Key => 40;

    /// <inheritdoc />
    public override async ValueTask PerformSkillAsync(Player player, IAttackable target, ushort skillId)
    {
        player.SkillCancelTokenSource?.CancelWithExtraTarget(target.Id);
    }
}