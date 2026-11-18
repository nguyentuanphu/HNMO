// <copyright file="VipPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Runtime.InteropServices;
using System.Threading;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>Opt-in VIP configuration and live account bonus refresh; entering the world never purchases a plan.</summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.VipPlugIn_Name), Description = nameof(PlugInResources.VipPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B5EE9346-9ED8-4CC0-8CA2-2D0E27CA2081")]
public sealed class VipPlugIn : IPlayerStateChangedPlugIn, IPeriodicTaskPlugIn, ISupportCustomConfiguration<VipConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    private readonly VipNotices _notices = new();
    private long _nextTickUtc;

    /// <inheritdoc />
    public VipConfiguration? Configuration { get; set; }

    /// <summary>Finds the explicitly enabled VIP plug-in for a game context.</summary>
    /// <param name="context">The game context.</param>
    /// <returns>The active plug-in, if enabled by the host.</returns>
    public static VipPlugIn? Get(IGameContext context)
        => context.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<VipPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new VipConfiguration();

    /// <summary>Creates an own-session adapter sharing this plug-in's ephemeral notice tracking.</summary>
    /// <returns>The adapter; new purchases remain disabled unless configured.</returns>
    public VipGameActions CreateActions() => new(this.Configuration ?? new(), VipStateStore.OpenHost(), notices: this._notices);

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration?.Enabled == true && currentState == PlayerState.EnteredWorld)
        {
            await this.CheckAsync(player).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (this.Configuration?.Enabled != true || now.Ticks < Interlocked.Read(ref this._nextTickUtc))
        {
            return;
        }

        Interlocked.Exchange(ref this._nextTickUtc, now.AddMinutes(1).Ticks);
        foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
        {
            if (player.Attributes?[Stats.IsVip] > 0)
            {
                await this.CheckAsync(player).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart() => Interlocked.Exchange(ref this._nextTickUtc, 0);

    private ValueTask CheckAsync(Player player)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (player.Account is null || player.PlayerState.CurrentState != PlayerState.EnteredWorld || VipWallet.HasUnconfirmed(player.PersistenceContext))
            {
                return;
            }

            player.RefreshAccountAttributeGrants();
            await this._notices.ShowOwnedAsync(player, this.Configuration ?? new(), VipStateStore.OpenHost()).ConfigureAwait(false);
        });
}
