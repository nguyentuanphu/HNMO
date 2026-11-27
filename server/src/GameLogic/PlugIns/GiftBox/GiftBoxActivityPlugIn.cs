// <copyright file="GiftBoxActivityPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using System.IO;
using System.Runtime.InteropServices;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>Records observed player world entry and departure timestamps for gift box recent-player audiences.</summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.GiftBoxActivityPlugIn_Name), Description = nameof(Properties.PlugInResources.GiftBoxActivityPlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("22FA3C8E-5E29-48AB-9C1B-1375B4B90C74")]
public sealed class GiftBoxActivityPlugIn : IPlayerStateChangedPlugIn
{
    private readonly IHnmoPlayerStateRowBackend? _backend;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="GiftBoxActivityPlugIn"/> class.</summary>
    public GiftBoxActivityPlugIn()
        : this(null, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GiftBoxActivityPlugIn"/> class with explicit test dependencies.</summary>
    /// <param name="backend">Player-state row backend.</param>
    /// <param name="clock">Authoritative clock.</param>
    internal GiftBoxActivityPlugIn(IHnmoPlayerStateRowBackend? backend, TimeProvider? clock = null)
    {
        this._backend = backend;
        this._clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (player.IsTemplatePlayer || player.Account is not { IsBot: false } account || account.GetId() == Guid.Empty)
        {
            return ValueTask.CompletedTask;
        }

        if (currentState != PlayerState.EnteredWorld && previousState != PlayerState.EnteredWorld)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            var rows = this._backend ?? HnmoPlayerStateRowStoreRegistry.GetBackend(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName));
            new GiftBoxActivity(rows).Observe(account.GetId(), this._clock.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Could not record gift box activity for account {AccountId}.", account.GetId());
        }

        return ValueTask.CompletedTask;
    }
}
