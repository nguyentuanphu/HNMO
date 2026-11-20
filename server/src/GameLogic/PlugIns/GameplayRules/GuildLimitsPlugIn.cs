// <copyright file="GuildLimitsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.Guild;
using HNMO.PlugIns;

/// <summary>
/// The servers' guild limits (Common.ini): the level a character needs to create a guild, and the most members a
/// guild can have. HNMO had neither.
/// </summary>
[PlugIn]
[Display(Name = "Guild limits", Description = "The level needed to create a guild and the most members of a guild.")]
[Guid("5B8E2C41-9D37-4F6A-A2E8-61C4D07B93F5")]
public sealed class GuildLimitsPlugIn :
    IGuildCreatingPlugIn,
    IGuildJoinRequestingPlugIn,
    ISupportCustomConfiguration<GuildLimitsConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <summary>Gets or sets the configuration.</summary>
    public GuildLimitsConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public async ValueTask GuildCreatingAsync(Player creator, CancelEventArgs eventArgs)
    {
        if (this.Configuration is not { MinimumCreateLevel: > 0 } cfg
            || creator.Attributes is not { } attributes
            || attributes[Stats.Level] >= cfg.MinimumCreateLevel)
        {
            return;
        }

        eventArgs.Cancel = true;
        await creator.ShowBlueMessageAsync($"A guild can be created from level {cfg.MinimumCreateLevel}. / Cần cấp {cfg.MinimumCreateLevel} để lập bang hội.").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask GuildJoinRequestingAsync(Player requester, Player guildMaster, CancelEventArgs eventArgs)
    {
        if (guildMaster.GuildStatus is not { } status
            || (guildMaster.GameContext as IGameServerContext)?.GuildServer is not { } guildServer
            || this.GetMaximumMembers(guildMaster) is not (> 0 and var maximumMembers))
        {
            return;
        }

        var members = await guildServer.GetGuildListAsync(status.GuildId).ConfigureAwait(false);
        if (members.Count < maximumMembers)
        {
            return;
        }

        eventArgs.Cancel = true;
        await requester.InvokeViewPlugInAsync<IGuildJoinResponsePlugIn>(p => p.ShowGuildJoinResponseAsync(GuildRequestAnswerResult.GuildFull)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the most members the guild of the guild master can have: <see cref="GuildLimitsConfiguration.MaximumMembers"/>
    /// (80 by default, like the Season 6/9 server suites). The master's own guild size (level / 10) is not a limit in HNMO
    /// (owner, 20 Nov 2026).
    /// </summary>
    /// <param name="guildMaster">The guild master.</param>
    /// <returns>The most members of the guild, the master included; 0 when there is no limit.</returns>
    internal int GetMaximumMembers(Player guildMaster)
    {
        return Math.Max(0, (this.Configuration ?? new GuildLimitsConfiguration()).MaximumMembers);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new GuildLimitsConfiguration();
}

/// <summary>The configuration of <see cref="GuildLimitsPlugIn"/>.</summary>
public sealed class GuildLimitsConfiguration
{
    /// <summary>Gets or sets the level a character needs to create a guild (0: any).</summary>
    public int MinimumCreateLevel { get; set; }

    /// <summary>
    /// Gets or sets the most members a guild can have, whatever its master's guild size is (0: no cap). It's 80,
    /// like the Season 6/9 server suites.
    /// </summary>
    public int MaximumMembers { get; set; } = 80;
}
