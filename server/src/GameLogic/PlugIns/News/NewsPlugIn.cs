// <copyright file="NewsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.News;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Manages bilingual EN/VI news posts and pushes chat notices (<c>C2 F5 2A</c>) when new posts are published
/// or when a player enters the world.
/// </summary>
[PlugIn]
[Display(Name = "HNMO News Manager", Description = "Bilingual EN/VI news posts for homepage, login screen news panel, and in-game chat notices.")]
[Guid("8F4625A0-F5EC-4D17-93C8-8437C75B4B19")]
public sealed class NewsPlugIn :
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<NewsConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public NewsConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="NewsPlugIn"/> for a game context.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in or <c>null</c>.</returns>
    public static NewsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<NewsPlugIn>().FirstOrDefault();

    /// <summary>Broadcasts a newly published news post as a chat notice to all entered-world players in <paramref name="gameContext"/>.</summary>
    /// <param name="gameContext">Target game context.</param>
    /// <param name="post">Published news post.</param>
    /// <returns>A task representing the broadcast.</returns>
    public static async ValueTask BroadcastPostAsync(IGameContext gameContext, NewsPost post)
    {
        ArgumentNullException.ThrowIfNull(gameContext);
        ArgumentNullException.ThrowIfNull(post);

        var json = NewsStateWriter.WriteNotice(post, fresh: true);
        await gameContext.ForEachPlayerAsync(async player =>
        {
            if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
            {
                return;
            }

            await player.InvokeViewPlugInAsync<IHnmoNewsViewPlugIn>(v => v.ShowNewsNoticeAsync(json)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new NewsConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld || previousState == PlayerState.EnteredWorld)
        {
            return;
        }

        var cfg = this.Configuration ??= new NewsConfiguration();
        if (!cfg.ChatNoticeOnEnterWorld)
        {
            return;
        }

        var store = NewsStore.Open(cfg.FileName);
        var latest = store.ListPublished(DateTime.UtcNow, 1).FirstOrDefault();
        if (latest is null)
        {
            return;
        }

        var json = NewsStateWriter.WriteNotice(latest, fresh: false);
        await player.InvokeViewPlugInAsync<IHnmoNewsViewPlugIn>(v => v.ShowNewsNoticeAsync(json)).ConfigureAwait(false);
    }
}
