// <copyright file="FixItemOptionsAndAttackSpeedPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update fixes some item options (damage, defense rate) and weapons attack speed.
/// It also refactors attack speed attributes for simplification.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixItemOptionsAndAttackSpeedPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixItemOptionsAndAttackSpeedPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("7733CDA9-6F4B-48D2-94F1-796C937F032A")]
public class FixItemOptionsAndAttackSpeedPlugIn075 : FixItemOptionsAndAttackSpeedPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        await base.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);
        this.FixWeaponsAttackSpeedStat(gameConfiguration);
    }
}