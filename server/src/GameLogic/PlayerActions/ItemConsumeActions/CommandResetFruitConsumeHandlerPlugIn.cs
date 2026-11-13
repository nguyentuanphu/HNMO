// <copyright file="CommandResetFruitConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// Consume handler for the reset fruit of command.
/// </summary>
[Guid("D8D75589-1358-4C26-9C9D-CC9042A86317")]
[PlugIn]
[Display(Name = nameof(PlugInResources.CommandResetFruitConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.CommandResetFruitConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class CommandResetFruitConsumeHandlerPlugIn : ResetFruitConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.CommandResetFruit;

    /// <inheritdoc />
    protected override AttributeDefinition StatAttribute => Stats.BaseLeadership;
}
