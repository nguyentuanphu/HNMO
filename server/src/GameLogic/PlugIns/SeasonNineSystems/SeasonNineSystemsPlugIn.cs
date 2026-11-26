// <copyright file="SeasonNineSystemsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.Properties;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Runtime controller for Season 9 gameplay systems (disabled by default):
/// applies 330-level Master Level cap, Muun pet condition/bonus summaries (<c>/muun</c>),
/// Pentagram elemental info &amp; Errtel rates (<c>/pentagram</c>), and Season 9 skills 618-621 balance knobs.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineSystemsPlugIn_Name), Description = nameof(PlugInResources.SeasonNineSystemsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7C2E9D14-5B08-4F61-8A32-1E9B4C7D8E05")]
public sealed class SeasonNineSystemsPlugIn :
    IPeriodicTaskPlugIn,
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<SeasonNineSystemsConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private short _realmMasterCap;

    /// <inheritdoc />
    public SeasonNineSystemsConfiguration? Configuration { get; set; }

    /// <summary>Gets or sets the season check (tests); the realm's <see cref="HnmoSeasonInstance.IsSeasonNine"/> by default.</summary>
    internal Func<bool> IsSeasonNineRealm { get; set; } = static () => HnmoSeasonInstance.IsSeasonNine;

    /// <summary>Gets active <see cref="SeasonNineSystemsPlugIn"/> from <paramref name="gameContext"/>.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active instance or <c>null</c>.</returns>
    public static SeasonNineSystemsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<SeasonNineSystemsPlugIn>().FirstOrDefault();

    /// <summary>
    /// Gets a value indicating whether the Season 9 systems run: switched on, and only in a Season 9 realm. Season 6.3
    /// and Arena never run them, whatever <see cref="SeasonNineSystemsConfiguration.SeasonNineOnly"/> says.
    /// </summary>
    /// <returns><c>true</c> when they run.</returns>
    public bool IsActive() => this.Configuration is { Enabled: true } && this.IsSeasonNineRealm();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new SeasonNineSystemsConfiguration();

    /// <inheritdoc />
    public void ForceStart()
    {
    }

    /// <inheritdoc />
    public ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var configuration = gameContext.Configuration;
        if (!this.IsActive())
        {
            // Off (or not a Season 9 realm): the realm's own cap comes back. Characters above it keep their level; they
            // just gain no master experience there.
            if (this._realmMasterCap > 0)
            {
                configuration.MaximumMasterLevel = this._realmMasterCap;
                this._realmMasterCap = 0;
                gameContext.RefreshExperienceTables();
            }

            return ValueTask.CompletedTask;
        }

        var targetCap = Math.Clamp(this.Configuration!.MasterLevelMaxCap, (short)200, (short)400);
        if (configuration.MaximumMasterLevel != targetCap)
        {
            if (this._realmMasterCap == 0)
            {
                this._realmMasterCap = configuration.MaximumMasterLevel;
            }

            configuration.MaximumMasterLevel = targetCap;
            gameContext.RefreshExperienceTables();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld || !this.IsActive() || this.Configuration is not { } cfg)
        {
            return;
        }

        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        await player.ShowBlueMessageAsync(
            isVi
                ? $"Season 9 Systems đang bật (Master Cap {cfg.MasterLevelMaxCap}, {SeasonNineSystemsCatalog.Muuns.Count} Muun, {SeasonNineSystemsCatalog.Pentagrams.Count} Pentagram)."
                : $"Season 9 Systems active (Master Cap {cfg.MasterLevelMaxCap}, {SeasonNineSystemsCatalog.Muuns.Count} Muun, {SeasonNineSystemsCatalog.Pentagrams.Count} Pentagram).").ConfigureAwait(false);
    }

    /// <summary>Executes <c>/muun</c> or <c>/pentagram</c> status command cleanly.</summary>
    /// <param name="player">Issuing player.</param>
    /// <param name="topic"><c>muun</c> or <c>pentagram</c>.</param>
    /// <returns>A task representing completion.</returns>
    public static async ValueTask ExecuteStatusCommandAsync(Player player, string topic)
    {
        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        if (Get(player.GameContext) is not { Configuration: { } cfg } plugIn || !plugIn.IsActive())
        {
            await player.ShowBlueMessageAsync(
                isVi ? "Hệ thống Muun / Pentagram Season 9 đang tắt." : "Season 9 Muun / Pentagram system is disabled.").ConfigureAwait(false);
            return;
        }

        if (topic == "muun")
        {
            await player.ShowBlueMessageAsync(
                isVi
                    ? $"[Muun] {(cfg.MuunEnabled ? "Bật" : "Tắt")} | {SeasonNineSystemsCatalog.Muuns.Count} Muun | Hệ số Tiến Hóa x{cfg.MuunEvoPointMultiplier:0.##}"
                    : $"[Muun] {(cfg.MuunEnabled ? "On" : "Off")} | {SeasonNineSystemsCatalog.Muuns.Count} Pets | Evo Mult x{cfg.MuunEvoPointMultiplier:0.##}").ConfigureAwait(false);
        }
        else
        {
            await player.ShowBlueMessageAsync(
                isVi
                    ? $"[Pentagram] {(cfg.PentagramEnabled ? "Bật" : "Tắt")} | Lợi thế Ngũ Hành +{cfg.PentagramElementalAdvantageBonusPct}% | Cộng tỉ lệ ép Errtel +{cfg.PentagramErrtelMixBonusPct}%"
                    : $"[Pentagram] {(cfg.PentagramEnabled ? "On" : "Off")} | Elemental Advantage +{cfg.PentagramElementalAdvantageBonusPct}% | Errtel Mix Bonus +{cfg.PentagramErrtelMixBonusPct}%").ConfigureAwait(false);
        }
    }
}
