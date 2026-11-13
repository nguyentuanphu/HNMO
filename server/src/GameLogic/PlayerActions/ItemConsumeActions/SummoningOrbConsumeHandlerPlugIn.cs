// <copyright file="SummoningOrbConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration.Items;
using HNMO.PlugIns;

/// <summary>
/// The summoning orb consume handler.
/// There is only one "Orb" item definition which allows to learn different skills, depending on the item level.
/// This consume handler determines the skill by adding the item level to the skill number
/// of the <see cref="ItemDefinition.Skill"/>.
/// </summary>
[Guid("71C8E542-4868-487E-BC92-0B7CC7CAEC8B")]
[PlugIn]
[Display(Name = nameof(PlugInResources.SummoningOrbConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.SummoningOrbConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class SummoningOrbConsumeHandlerPlugIn : LearnablesConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.SummonOrb;

    /// <summary>
    /// Gets the learnable skill.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>
    /// The skill to learn.
    /// </returns>
    protected override Skill GetLearnableSkill(Item item, GameConfiguration gameConfiguration)
    {
        item.ThrowNotInitializedProperty(item.Definition?.Skill is null, "Definition.Skill");

        var baseSkillNumber = item.Definition.Skill.Number;
        var targetSkillNumber = baseSkillNumber + item.Level;
        return gameConfiguration.Skills.First(s => s.Number == targetSkillNumber);
    }
}