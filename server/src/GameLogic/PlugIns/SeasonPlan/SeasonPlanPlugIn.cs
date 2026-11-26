// <copyright file="SeasonPlanPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonPlan;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Progressive Season Plan plug-in (OFF by default, hotkey <kbd>F8</kbd>): opens stages on scheduled dates,
/// boss kills, or player level counts, and gates Chaos mixes, Harmony, +10..+15, Master Levels, Wings, and 380 items until unlocked.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Season Plan", Description = "Locks and unlocks progression stages (+9, Master, Harmony, Wing 2, Fenrir, 380, Wing 3) on dates, boss kills, or player counts (OFF by default).")]
[Guid("F08D3B19-7C42-4850-A6B2-2E1C8D503792")]
public sealed class SeasonPlanPlugIn :
    IPeriodicTaskPlugIn,
    IPlayerStateChangedPlugIn,
    ICharacterLevelUpPlugIn,
    ISupportCustomConfiguration<SeasonPlanConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private (SeasonPlanConfiguration Config, SeasonPlanEngine Engine)? _engine;
    private DateTime _lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public SeasonPlanConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="SeasonPlanPlugIn"/> for a game context, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static SeasonPlanPlugIn? Get(IGameContext gameContext)
    {
        var candidates = gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<SeasonPlanPlugIn>();
        return candidates.FirstOrDefault(p => p.Configuration is not null) ?? candidates.FirstOrDefault();
    }

    /// <summary>Checks whether <paramref name="gate"/> is currently unlocked (<c>true</c> whenever Season Plan is disabled).</summary>
    /// <param name="gameContext">Game context.</param>
    /// <param name="gate">Feature gate.</param>
    /// <returns><c>true</c> if unlocked/permitted.</returns>
    public static bool IsFeatureUnlocked(IGameContext gameContext, SeasonFeatureGate gate) =>
        Get(gameContext) is not { Configuration: not null } plugIn || plugIn.GetEngine().IsFeatureUnlocked(gate, DateTime.UtcNow);

    /// <summary>Returns the current active Season Stage's <see cref="SeasonStageDefinition.MaxBoxLevel"/> (0 if unrestricted).</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Max box level, or 0.</returns>
    public static int GetCurrentMaxBoxLevel(IGameContext gameContext) =>
        Get(gameContext) is { Configuration: not null } plugIn ? plugIn.GetEngine().GetCurrentMaxBoxLevel(DateTime.UtcNow) : 0;

    /// <summary>Records a World Boss kill against any waiting Season Plan stage conditions.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <param name="bossId">Boss identifier.</param>
    /// <returns>A task representing condition check &amp; announcement.</returns>
    public static async ValueTask NotifyBossKilledAsync(IGameContext? gameContext, string? bossId)
    {
        if (gameContext is null || string.IsNullOrWhiteSpace(bossId) || Get(gameContext) is not { } plugIn)
        {
            return;
        }

        var unlocked = plugIn.GetEngine().ObserveBossKill(bossId, DateTime.UtcNow);
        await plugIn.BroadcastNewStagesAsync(gameContext, unlocked).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new SeasonPlanConfiguration();

    /// <summary>Gets or creates the engine.</summary>
    /// <returns>Season Plan engine.</returns>
    public SeasonPlanEngine GetEngine()
    {
        var cfg = this.Configuration ?? new SeasonPlanConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new SeasonPlanEngine(cfg, SeasonPlanStore.Open(cfg.FileName), enabled: this.Configuration is not null);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public void ForceStart() => this._lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this.Configuration is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - this._lastTickUtc < TimeSpan.FromSeconds(15))
        {
            return;
        }

        this._lastTickUtc = now;
        var newlyUnlocked = this.GetEngine().EvaluateConditions(now);
        await this.BroadcastNewStagesAsync(gameContext, newlyUnlocked).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration is null || currentState != PlayerState.EnteredWorld || player.SelectedCharacter is not { } ch)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var lvl = (int)(player.Attributes?[Stats.Level] ?? 1);
        var newly = this.GetEngine().ObserveCharacterLevel(ch.Id.ToString(), lvl, now);
        await this.BroadcastNewStagesAsync(player.GameContext, newly).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player)
    {
        if (this.Configuration is null || player.SelectedCharacter is not { } ch || player.Attributes is null)
        {
            return;
        }

        var lvl = (int)player.Attributes[Stats.Level];
        var newly = this.GetEngine().ObserveCharacterLevel(ch.Id.ToString(), lvl, DateTime.UtcNow);
        if (newly.Count > 0)
        {
            _ = this.BroadcastNewStagesAsync(player.GameContext, newly);
        }
    }

    /// <summary>Sends Season Plan timeline JSON to a player.</summary>
    /// <param name="player">Player.</param>
    /// <param name="requested">Whether requested by UI.</param>
    /// <returns>A task representing the send.</returns>
    public ValueTask SendStateAsync(Player player, bool requested)
    {
        var json = SeasonPlanStateWriter.WritePublic(this.GetEngine(), DateTime.UtcNow);
        return player.InvokeViewPlugInAsync<IHnmoSeasonPlanViewPlugIn>(p => p.ShowSeasonPlanAsync(json, requested));
    }

    private async ValueTask BroadcastNewStagesAsync(IGameContext gameContext, IReadOnlyList<SeasonStageDefinition> newlyUnlocked)
    {
        if (newlyUnlocked.Count == 0)
        {
            return;
        }

        var json = SeasonPlanStateWriter.WritePublic(this.GetEngine(), DateTime.UtcNow);
        var announce = this.GetEngine().Configuration.AnnounceInGame;
        await gameContext.ForEachPlayerAsync(async p =>
        {
            if (announce)
            {
                var isVi = string.Equals(p.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
                foreach (var st in newlyUnlocked)
                {
                    var msg = isVi
                        ? $"Lộ trình mùa giải: Đã mở khóa {(string.IsNullOrWhiteSpace(st.NameVi) ? st.NameEn : st.NameVi)}!"
                        : $"Season Plan unlocked: {st.NameEn}!";
                    await p.InvokeViewPlugInAsync<IShowMessagePlugIn>(v => v.ShowMessageAsync(msg, MessageType.GoldenCenter)).ConfigureAwait(false);
                }
            }

            await p.InvokeViewPlugInAsync<IHnmoSeasonPlanViewPlugIn>(v => v.ShowSeasonPlanAsync(json, requested: false)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
