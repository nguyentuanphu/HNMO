// <copyright file="GoldenArcherRegistrationStrategy.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Items.ItemRegistration;

using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.NPC;
using HNMO.PlugIns;

/// <summary>
/// The item registration strategy for the Golden Archer NPC.
/// </summary>
[Guid("43A3B5CA-9E4B-4E3F-8C7F-854BC50E0C49")]
[PlugIn]
public class GoldenArcherRegistrationStrategy : BaseItemRegistrationStrategy
{
    /// <inheritdoc />
    public override short NpcNumber => 236;

    /// <inheritdoc />
    public override AttributeDefinition TargetStat => Stats.RegisteredRenas;

    /// <inheritdoc />
    public override AttributeDefinition TargetTotalStat => Stats.TotalRegisteredRenas;

    /// <inheritdoc />
    public override async ValueTask OpenDialogAsync(Player player)
    {
        await player.InvokeViewPlugInAsync<IItemRegistrationResultPlugIn>(
            p => p.RegistrationResultAsync(this.NpcNumber, ItemRegistrationOperation.OpenRegistrationDialog)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnMissingItemAsync(Player player)
    {
        await player.InvokeViewPlugInAsync<IItemRegistrationResultPlugIn>(
            p => p.RegistrationResultAsync(this.NpcNumber, ItemRegistrationOperation.MissingItem)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnRegistrationCompletedAsync(Player player)
    {
        await player.InvokeViewPlugInAsync<IItemRegistrationResultPlugIn>(
            p => p.RegistrationResultAsync(this.NpcNumber, ItemRegistrationOperation.RegistrationCompleted)).ConfigureAwait(false);
    }
}