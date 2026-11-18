// <copyright file="VipPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>Opt-in VIP configuration and live account bonus refresh; entering the world never purchases a plan.</summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.VipPlugIn_Name), Description = nameof(PlugInResources.VipPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B5EE9346-9ED8-4CC0-8CA2-2D0E27CA2081")]
public sealed class VipPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<VipConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc />
    public VipConfiguration? Configuration { get; set; }

    /// <summary>Finds the explicitly enabled VIP plug-in for a game context.</summary>
    /// <param name="context">The game context.</param>
    /// <returns>The active plug-in, if enabled by the host.</returns>
    public static VipPlugIn? Get(IGameContext context)
        => context.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<VipPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new VipConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration?.Enabled == true && currentState == PlayerState.EnteredWorld)
        {
            player.RefreshAccountAttributeGrants();
        }

        return ValueTask.CompletedTask;
    }
}
