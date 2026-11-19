// <copyright file="NewsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.News;

using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> and <see cref="IHnmoPlayerStateRowBackend"/>
/// storing news posts under feature key <c>news.post</c>.
/// </summary>
public sealed class NewsStore
{
    /// <summary>Unified state store section key.</summary>
    public const string SectionKey = "news";

    /// <summary>PostgreSQL/row-store feature key.</summary>
    public const string FeaturePosts = "news.post";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, NewsStore> Shared = new(StringComparer.Ordinal);

    private readonly object _gate = new();
    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<NewsPost> _postSync = new();
    private readonly Dictionary<string, NewsPost> _posts;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsStore"/> class.
    /// </summary>
    /// <param name="filePath">Optional state file path/key; null keeps in-memory state only.</param>
    public NewsStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this._posts = this.Load();
    }

    /// <summary>Gets or creates the shared store for a configured file name/path.</summary>
    /// <param name="fileName">Configured state file path or name.</param>
    /// <returns>The store instance.</returns>
    public static NewsStore Open(string? fileName)
    {
        var key = string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName.Trim();
        var resolved = key.Length == 0
            ? string.Empty
            : (HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(key, AppContext.BaseDirectory));
        return Shared.GetOrAdd(resolved, p => new NewsStore(p.Length == 0 ? null : p));
    }

    /// <summary>Resolves the Australia/Sydney time zone cross-platform.</summary>
    /// <returns>Sydney <see cref="TimeZoneInfo"/>.</returns>
    public static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    /// <summary>Formats a UTC timestamp in Sydney wall-clock time.</summary>
    /// <param name="utc">UTC timestamp.</param>
    /// <returns>Formatted timestamp string (<c>yyyy-MM-dd HH:mm</c>).</returns>
    public static string FormatSydney(DateTime utc)
    {
        var kinded = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(kinded, ResolveSydneyZone())
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Parses a Sydney wall-clock date/time string into UTC.</summary>
    /// <param name="sydneyLocal">Date/time string (<c>yyyy-MM-ddTHH:mm</c> or <c>yyyy-MM-dd HH:mm</c>).</param>
    /// <returns>UTC timestamp.</returns>
    public static DateTime ParseSydneyToUtc(string? sydneyLocal)
    {
        if (string.IsNullOrWhiteSpace(sydneyLocal))
        {
            return DateTime.UtcNow;
        }

        if (!DateTime.TryParse(sydneyLocal.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return DateTime.UtcNow;
        }

        var zone = ResolveSydneyZone();
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }

    /// <summary>Sanitizes an optional image URL to prevent script/data URI injection.</summary>
    /// <param name="rawUrl">Raw URL string.</param>
    /// <returns>Safe normalized URL or empty string.</returns>
    public static string SanitizeImageUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return string.Empty;
        }

        var trimmed = rawUrl.Trim();
        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return trimmed.Length <= 512 ? trimmed : string.Empty;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            var normalized = uri.ToString();
            return normalized.Length <= 512 ? normalized : string.Empty;
        }

        return string.Empty;
    }

    /// <summary>Returns all posts sorted by pinned flag then publish time descending.</summary>
    /// <returns>Cloned post list.</returns>
    public IReadOnlyList<NewsPost> ListAll()
    {
        lock (this._gate)
        {
            return this._posts.Values
                .OrderByDescending(p => p.IsPinned)
                .ThenByDescending(p => p.PublishAtUtc)
                .ThenByDescending(p => p.UpdatedAtUtc)
                .Select(Clone)
                .ToList();
        }
    }

    /// <summary>Returns posts published at or before <paramref name="nowUtc"/>.</summary>
    /// <param name="nowUtc">Reference UTC timestamp.</param>
    /// <param name="maxCount">Maximum number of posts.</param>
    /// <returns>Cloned published posts.</returns>
    public IReadOnlyList<NewsPost> ListPublished(DateTime nowUtc, int maxCount = 20)
    {
        var limit = Math.Clamp(maxCount, 1, 100);
        lock (this._gate)
        {
            return this._posts.Values
                .Where(p => p.PublishAtUtc <= nowUtc)
                .OrderByDescending(p => p.IsPinned)
                .ThenByDescending(p => p.PublishAtUtc)
                .ThenByDescending(p => p.UpdatedAtUtc)
                .Take(limit)
                .Select(Clone)
                .ToList();
        }
    }

    /// <summary>Gets a single post by id.</summary>
    /// <param name="id">Post identifier.</param>
    /// <returns>Cloned post or <c>null</c>.</returns>
    public NewsPost? Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (this._gate)
        {
            return this._posts.TryGetValue(id.Trim(), out var post) ? Clone(post) : null;
        }
    }

    /// <summary>Creates or updates a news post.</summary>
    /// <param name="input">Post payload.</param>
    /// <returns>Saved post snapshot.</returns>
    public NewsPost Upsert(NewsPost input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var titleEn = (input.TitleEn ?? string.Empty).Trim();
        var titleVi = (input.TitleVi ?? string.Empty).Trim();
        var bodyEn = (input.BodyEn ?? string.Empty).Trim();
        var bodyVi = (input.BodyVi ?? string.Empty).Trim();
        if (titleEn.Length == 0 && titleVi.Length == 0)
        {
            throw new ArgumentException("At least one headline (English or Vietnamese) is required.", nameof(input));
        }

        if (titleEn.Length == 0)
        {
            titleEn = titleVi;
        }

        if (titleVi.Length == 0)
        {
            titleVi = titleEn;
        }

        if (bodyEn.Length == 0)
        {
            bodyEn = bodyVi;
        }

        if (bodyVi.Length == 0)
        {
            bodyVi = bodyEn;
        }

        var nowUtc = DateTime.UtcNow;
        var publishUtc = input.PublishAtUtc == default ? nowUtc : DateTime.SpecifyKind(input.PublishAtUtc, DateTimeKind.Utc);
        var id = string.IsNullOrWhiteSpace(input.Id) ? Guid.NewGuid().ToString("N") : input.Id.Trim();

        lock (this._gate)
        {
            var createdAt = this._posts.TryGetValue(id, out var existing) ? existing.CreatedAtUtc : nowUtc;
            var saved = new NewsPost
            {
                Id = id,
                TitleEn = titleEn,
                TitleVi = titleVi,
                BodyEn = bodyEn,
                BodyVi = bodyVi,
                ImageUrl = SanitizeImageUrl(input.ImageUrl),
                PublishAtUtc = publishUtc,
                IsPinned = input.IsPinned,
                NotifyInChat = input.NotifyInChat,
                Author = string.IsNullOrWhiteSpace(input.Author) ? "Admin" : input.Author.Trim(),
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = nowUtc,
            };

            this._posts[id] = saved;
            this.SaveKeyLocked(id);
            return Clone(saved);
        }
    }

    /// <summary>Deletes a post by id.</summary>
    /// <param name="id">Post identifier.</param>
    /// <returns><c>true</c> if deleted.</returns>
    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var key = id.Trim();
        lock (this._gate)
        {
            if (!this._posts.Remove(key))
            {
                return false;
            }

            this.SaveKeyLocked(key);
            return true;
        }
    }

    private static NewsPost Clone(NewsPost source) => new()
    {
        Id = source.Id,
        TitleEn = source.TitleEn,
        TitleVi = source.TitleVi,
        BodyEn = source.BodyEn,
        BodyVi = source.BodyVi,
        ImageUrl = source.ImageUrl,
        PublishAtUtc = source.PublishAtUtc,
        IsPinned = source.IsPinned,
        NotifyInChat = source.NotifyInChat,
        Author = source.Author,
        CreatedAtUtc = source.CreatedAtUtc,
        UpdatedAtUtc = source.UpdatedAtUtc,
    };

    private Dictionary<string, NewsPost> Load()
    {
        var dict = this._stateStore?.ReadSection<Dictionary<string, NewsPost>>(SectionKey, JsonOptions)
            ?? new Dictionary<string, NewsPost>(StringComparer.Ordinal);
        if (this._postSync.LoadOrMigrate(this._rowBackend, FeaturePosts, dict, JsonOptions))
        {
            this._stateStore?.WriteSection(SectionKey, dict, JsonOptions);
        }

        return dict;
    }

    private void SaveKeyLocked(string key)
    {
        this._postSync.MarkDirty(key);
        this._postSync.SaveChanges(this._rowBackend, FeaturePosts, this._posts, JsonOptions);
    }
}
