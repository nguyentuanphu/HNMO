// <copyright file="FixItemOptionsAndAttackSpeedPlugIn095d.cs" company="HNMO">
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
[Guid("C7F90EDB-EC00-467D-826F-9DEFFEA1206A")]
public class FixItemOptionsAndAttackSpeedPlugIn095D : FixItemOptionsAndAttackSpeedPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        await base.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);
        this.FixWeaponsAttackSpeedStat(gameConfiguration);
        this.ChangeDinorantAttackSpeedOption(gameConfiguration);
        this.UpdateExcellentAttackSpeedAndBaseDmgOptions(gameConfiguration);
    }
}