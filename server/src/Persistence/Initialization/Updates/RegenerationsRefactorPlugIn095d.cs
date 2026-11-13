// <copyright file="RegenerationsRefactorPlugIn095d.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// This update fixes and reworks some regeneration attributes (health, mana, ability). It also adds default running (and fast swimming) speed for tier 2 chars (MG).
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.RegenerationsRefactorPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.RegenerationsRefactorPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("A7C2E9F4-5B1D-4E8A-9C6F-3D2B7A1E5F90")]
public class RegenerationsRefactorPlugIn095D : RegenerationsRefactorPlugInBase
{
    /// <summary>
    /// The plug in description.
    /// </summary>
    internal new const string PlugInDescription = "This update fixes and reworks some regeneration attributes (health, mana, ability). It also adds default running (and fast swimming) speed for tier 2 chars (MG).";

    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        await base.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);

        var movementSpeed = Stats.MovementSpeed.GetPersistent(gameConfiguration);
        var movementSpeedUnderwater = Stats.MovementSpeedUnderwater.GetPersistent(gameConfiguration);

        gameConfiguration.CharacterClasses.ForEach(charClass =>
        {
            // Add default movement speeds for tier 2 chars
            // MG classes
            if (charClass.Number == 12 || charClass.Number == 13)
            {
                charClass.BaseAttributeValues.Add(context.CreateNew<ConstValueAttribute>(MovementSpeedConstants.RunningGearMovementSpeed, movementSpeed, AggregateType.Maximum));
                charClass.BaseAttributeValues.Add(context.CreateNew<ConstValueAttribute>(MovementSpeedConstants.RunningGearMovementSpeed, movementSpeedUnderwater, AggregateType.Maximum));
            }
        });
    }
}
