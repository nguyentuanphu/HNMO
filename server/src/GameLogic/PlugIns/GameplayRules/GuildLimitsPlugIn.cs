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
    IGuildRelationshipChangingPlugIn,
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
    public async ValueTask GuildRelationshipChangingAsync(Player requester, Player targetGuildMaster, GuildRelationshipType relationshipType, GuildRelationshipRequestType requestType, CancelEventArgs eventArgs)
    {
        var minimum = this.Configuration?.MinimumAllianceGuildMembers ?? 0;
        if (minimum <= 0
            || relationshipType != GuildRelationshipType.Alliance
            || requestType != GuildRelationshipRequestType.Join
            || (requester.GameContext as IGameServerContext)?.GuildServer is not { } guildServer)
        {
            return;
        }

        foreach (var guildId in new[] { requester.GuildStatus?.GuildId, targetGuildMaster.GuildStatus?.GuildId })
        {
            if (guildId is not { } id || (await guildServer.GetGuildListAsync(id).ConfigureAwait(false)).Count < minimum)
            {
                eventArgs.Cancel = true;
                await requester.ShowBlueMessageAsync($"Each guild of an alliance needs at least {minimum} members. / Mỗi bang hội trong liên minh cần ít nhất {minimum} thành viên.").ConfigureAwait(false);
                return;
            }
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new GuildLimitsConfiguration();

    /// <summary>
    /// Gets the level a character needs to create a guild: <see cref="GuildLimitsConfiguration.MinimumCreateLevel"/>
    /// of the active guild limits, otherwise its default (150, like the Season 6/9 server suites).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The level; 0 when any level may create a guild.</returns>
    public static int GetMinimumCreateLevel(IGameContext gameContext)
    {
        var configuration = gameContext.PlugInManager.GetActivePlugInsOf<IGuildCreatingPlugIn>()
            .OfType<GuildLimitsPlugIn>()
            .FirstOrDefault()?.Configuration;
        return Math.Max(0, (configuration ?? new GuildLimitsConfiguration()).MinimumCreateLevel);
    }
}

/// <summary>The configuration of <see cref="GuildLimitsPlugIn"/>.</summary>
public sealed class GuildLimitsConfiguration
{
    /// <summary>
    /// Gets or sets the level a character needs to create a guild (0: any). It's 150, like the Season 6/9 server
    /// suites (GuildCreateLevel); the guild master asks for it, and the creation request checks it again.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GuildLimitsConfiguration_MinimumCreateLevel_Caption), Description = nameof(Properties.PlugInResources.GuildLimitsConfiguration_MinimumCreateLevel_Description), ResourceType = typeof(Properties.PlugInResources))]
    public int MinimumCreateLevel { get; set; } = 150;

    /// <summary>
    /// Gets or sets the most members a guild can have, whatever its master's guild size is (0: no cap). It's 80,
    /// like the Season 6/9 server suites.
    /// </summary>
    public int MaximumMembers { get; set; } = 80;

    /// <summary>
    /// Gets or sets the members each guild needs to form or join an alliance (0: any; 10 like the Season 6/9 server suites).
    /// </summary>
    public int MinimumAllianceGuildMembers { get; set; } = 10;
}
