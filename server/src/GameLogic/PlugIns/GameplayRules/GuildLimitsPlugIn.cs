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
/// The servers' guild limits (IGC_Common.ini): the level a character needs to create a guild, and the most members a
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
        if (this.Configuration is not { MaximumMembers: > 0 } cfg
            || guildMaster.GuildStatus is not { } status
            || (guildMaster.GameContext as IGameServerContext)?.GuildServer is not { } guildServer)
        {
            return;
        }

        var members = await guildServer.GetGuildListAsync(status.GuildId).ConfigureAwait(false);
        if (members.Count < cfg.MaximumMembers)
        {
            return;
        }

        eventArgs.Cancel = true;
        await requester.InvokeViewPlugInAsync<IGuildJoinResponsePlugIn>(p => p.ShowGuildJoinResponseAsync(GuildRequestAnswerResult.GuildFull)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new GuildLimitsConfiguration();
}

/// <summary>The configuration of <see cref="GuildLimitsPlugIn"/>.</summary>
public sealed class GuildLimitsConfiguration
{
    /// <summary>Gets or sets the level a character needs to create a guild (0: any).</summary>
    public int MinimumCreateLevel { get; set; }

    /// <summary>Gets or sets the most members a guild can have (0: no limit).</summary>
    public int MaximumMembers { get; set; }
}
