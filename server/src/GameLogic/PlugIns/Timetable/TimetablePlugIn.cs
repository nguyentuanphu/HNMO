// <copyright file="TimetablePlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Event and boss timetable plug-in (OFF by default): serves live schedules, maps, and drops
/// in Sydney and local time for events, invasions, and world bosses via packet <c>F5 33</c>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Event & Boss Timetable", Description = "Publishes events, invasions (golden, white wizard, rabbits, snowmen, find the GM) and boss schedules with maps and drops (OFF by default).")]
[Guid("B62D3841-904F-4E2A-B7C5-13D904A8E612")]
public sealed class TimetablePlugIn :
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<TimetableConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <inheritdoc />
    public TimetableConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="TimetablePlugIn"/> for a game context, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in instance, or <c>null</c>.</returns>
    public static TimetablePlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<TimetablePlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new TimetableConfiguration();

    /// <summary>Builds the timetable snapshot for a game context.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="utcNow">Optional UTC clock override.</param>
    /// <returns>The computed snapshot.</returns>
    public TimetableSnapshot BuildSnapshot(IGameContext gameContext, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var config = this.Configuration ??= new TimetableConfiguration();
        var sources = TimetableSourceCollector.CollectFromContext(gameContext, config, now);
        var winners = TimetableStore.Open(config.FileName).ReadFindGmWinners();
        return TimetableEngine.BuildSnapshot(config, enabled: true, now, sources, winners);
    }

    /// <summary>Sends the current timetable JSON to a player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether requested explicitly by the client.</param>
    /// <returns>A task representing the send operation.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested)
    {
        var json = TimetableStateWriter.Write(this.BuildSnapshot(player.GameContext));
        await player.InvokeViewPlugInAsync<IHnmoTimetableViewPlugIn>(p => p.ShowTimetableAsync(json, requested)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
        }
    }
}
