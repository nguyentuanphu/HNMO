// <copyright file="NewsAdminOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.News;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.PlugIns;

/// <summary>
/// Admin panel operations for <see cref="NewsPlugIn"/>: load/save bilingual posts,
/// read/update plug-in settings, and broadcast live in-game chat notices (<c>C2 F5 2A</c>).
/// </summary>
/// <param name="persistence">Persistence context provider.</param>
/// <param name="servers">Running game servers for live chat broadcast.</param>
/// <param name="executor">Authorization and audit executor.</param>
/// <param name="writeSettings">Optional read-only write protection settings.</param>
public sealed class NewsAdminOperations(
    IPersistenceContextProvider persistence,
    IServerProvider servers,
    AdminOperationExecutor executor,
    AdminWriteSettings? writeSettings = null)
{
    /// <summary>Loads current <see cref="NewsConfiguration"/> and all saved posts.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active flag, configuration, and sorted post list.</returns>
    [NotExposedToAgents("Admin panel News Manager UI only.")]
    public async ValueTask<(bool IsActive, NewsConfiguration Configuration, IReadOnlyList<NewsPost> Posts)> LoadAsync(
        CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var (isActive, cfg) = await this.ReadConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var posts = NewsStore.Open(cfg.FileName).ListAll();
        return (isActive, cfg, posts);
    }

    /// <summary>Creates or updates a bilingual EN/VI news post and optionally broadcasts a chat notice to online players.</summary>
    /// <param name="post">Post data.</param>
    /// <param name="sydneyPublishLocal">Sydney wall-clock date/time string (<c>yyyy-MM-ddTHH:mm</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Saved post and number of game servers notified.</returns>
    [NotExposedToAgents("Admin panel News Manager UI only.")]
    public async ValueTask<(NewsPost Saved, int BroadcastServers)> SavePostAsync(
        NewsPost post,
        string? sydneyPublishLocal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (writeSettings is not null)
        {
            await AdminChangeExecution.RequireAsync(executor, writeSettings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        }

        return await executor.ExecuteAsync(
            AdminRoles.Administrator,
            "news.savePost",
            new
            {
                id = post.Id,
                titleEn = post.TitleEn,
                titleVi = post.TitleVi,
                pinned = post.IsPinned,
                notifyInChat = post.NotifyInChat,
                sydneyPublishLocal,
            },
            async actor =>
            {
                var (isActive, cfg) = await this.ReadConfigurationAsync(cancellationToken).ConfigureAwait(false);
                var publishUtc = NewsStore.ParseSydneyToUtc(sydneyPublishLocal);
                var input = new NewsPost
                {
                    Id = post.Id,
                    TitleEn = post.TitleEn,
                    TitleVi = post.TitleVi,
                    BodyEn = post.BodyEn,
                    BodyVi = post.BodyVi,
                    ImageUrl = post.ImageUrl,
                    PublishAtUtc = publishUtc,
                    IsPinned = post.IsPinned,
                    NotifyInChat = post.NotifyInChat,
                    Author = string.IsNullOrWhiteSpace(post.Author) ? actor.Name : post.Author,
                };

                var saved = NewsStore.Open(cfg.FileName).Upsert(input);
                var broadcastServers = 0;
                if (isActive && cfg.ChatNoticeOnPublish && saved.NotifyInChat && saved.PublishAtUtc <= DateTime.UtcNow)
                {
                    broadcastServers = await this.BroadcastToServersAsync(saved).ConfigureAwait(false);
                }

                return (saved, broadcastServers);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Broadcasts an existing post immediately to all online players.</summary>
    /// <param name="id">Post identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when post existed and broadcast completed, plus server count.</returns>
    [NotExposedToAgents("Admin panel News Manager UI only.")]
    public async ValueTask<(bool Found, int BroadcastServers)> BroadcastPostAsync(string id, CancellationToken cancellationToken)
    {
        if (writeSettings is not null)
        {
            await AdminChangeExecution.RequireAsync(executor, writeSettings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        }

        return await executor.ExecuteAsync(
            AdminRoles.Administrator,
            "news.broadcastPost",
            new { id },
            async _ =>
            {
                var (_, cfg) = await this.ReadConfigurationAsync(cancellationToken).ConfigureAwait(false);
                var post = NewsStore.Open(cfg.FileName).Get(id);
                if (post is null)
                {
                    return (Found: false, BroadcastServers: 0);
                }

                var count = await this.BroadcastToServersAsync(post).ConfigureAwait(false);
                return (Found: true, BroadcastServers: count);
            },
            cancellationToken,
            res => res.Found).ConfigureAwait(false);
    }

    /// <summary>Deletes a news post by id.</summary>
    /// <param name="id">Post identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if deleted.</returns>
    [NotExposedToAgents("Admin panel News Manager UI only.")]
    public async ValueTask<bool> DeletePostAsync(string id, CancellationToken cancellationToken)
    {
        if (writeSettings is not null)
        {
            await AdminChangeExecution.RequireAsync(executor, writeSettings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        }

        return await executor.ExecuteAsync(
            AdminRoles.Administrator,
            "news.deletePost",
            new { id },
            async _ =>
            {
                var (_, cfg) = await this.ReadConfigurationAsync(cancellationToken).ConfigureAwait(false);
                return NewsStore.Open(cfg.FileName).Delete(id);
            },
            cancellationToken,
            deleted => deleted).ConfigureAwait(false);
    }

    private async ValueTask<int> BroadcastToServersAsync(NewsPost post)
    {
        var count = 0;
        foreach (var provider in servers.Servers.OfType<IGameServerContextProvider>())
        {
            await NewsPlugIn.BroadcastPostAsync(provider.Context, post).ConfigureAwait(false);
            count++;
        }

        return count;
    }

    private async ValueTask<(bool IsActive, NewsConfiguration Configuration)> ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        using var context = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), false);
        var configs = await context.GetAsync<PlugInConfiguration>(cancellationToken).ConfigureAwait(false);
        var match = configs.FirstOrDefault(c => c.TypeId == typeof(NewsPlugIn).GUID);
        if (match is null)
        {
            return (true, new NewsConfiguration());
        }

        NewsConfiguration? parsed = null;
        if (!string.IsNullOrWhiteSpace(match.CustomConfiguration))
        {
            try
            {
                parsed = match.GetConfiguration<NewsConfiguration>(null);
            }
            catch (JsonException)
            {
                parsed = null;
            }
        }

        return (match.IsActive, parsed ?? new NewsConfiguration());
    }
}
