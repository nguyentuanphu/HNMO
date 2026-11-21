// <copyright file="NewsStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.News;

using System.Text.Json;

/// <summary>
/// Serializes public website and in-game packet payloads for <see cref="NewsPlugIn"/>.
/// </summary>
public static class NewsStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Writes public news JSON for website and login screen.</summary>
    /// <param name="configuration">News configuration.</param>
    /// <param name="store">News store.</param>
    /// <param name="nowUtc">Reference UTC timestamp.</param>
    /// <returns>Serialized JSON payload.</returns>
    public static string WritePublic(NewsConfiguration configuration, NewsStore store, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(store);

        var posts = store.ListPublished(nowUtc, configuration.MaxVisiblePosts)
            .Select(ToDto)
            .ToArray();

        return JsonSerializer.Serialize(
            new
            {
                v = 1,
                enabled = true,
                allowWebsite = configuration.AllowWebsite,
                showLoginPanel = configuration.ShowLoginPanel,
                sydneyOffsetMinutes = (int)NewsStore.ResolveSydneyZone().GetUtcOffset(nowUtc).TotalMinutes,
                posts,
            },
            JsonOptions);
    }

    /// <summary>Writes the <c>C2 F5 2A</c> notice packet JSON for in-game chat alerts.</summary>
    /// <param name="post">Published post.</param>
    /// <param name="fresh">True when pushed live at publish time; false when pushed on login.</param>
    /// <returns>Serialized JSON payload.</returns>
    public static string WriteNotice(NewsPost post, bool fresh)
    {
        ArgumentNullException.ThrowIfNull(post);
        return JsonSerializer.Serialize(
            new
            {
                v = 1,
                fresh,
                latest = ToDto(post),
            },
            JsonOptions);
    }

    private static object ToDto(NewsPost post) => new
    {
        id = post.Id,
        titleEn = post.TitleEn,
        titleVi = post.TitleVi,
        bodyEn = post.BodyEn,
        bodyVi = post.BodyVi,
        imageUrl = post.ImageUrl,
        publishAtUtc = new DateTimeOffset(DateTime.SpecifyKind(post.PublishAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
        publishSydney = NewsStore.FormatSydney(post.PublishAtUtc),
        pinned = post.IsPinned,
        author = post.Author,
    };
}
