// Serves the game's data files to the browser, as web/server.mjs does in development.
//
// The data files keep their names and folders. Two things are adapted on the fly:
//  - names are matched case-insensitively, as on Windows, because models and maps
//    reference their files with whatever case the original authors used;
//  - .OZJ and .OZT are unwrapped to the JPEG and TGA they contain, and a request for
//    "x.jpg" / "x.tga" falls back to "x.OZJ" / "x.OZT", the names models refer to.

using System.Buffers;
using System.Collections.Concurrent;

internal sealed class GameFiles : IDisposable
{
    private const int OzjPrefixBytes = 24;
    private const int OztPrefixBytes = 4;

    private static readonly Dictionary<string, string> FallbackExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ".ozj",
        [".jpeg"] = ".ozj",
        [".tga"] = ".ozt",
    };

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".json"] = "application/json; charset=utf-8",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".ktx2"] = "image/ktx2",
        [".opus"] = "audio/ogg",
        [".wav"] = "audio/wav",
        [".mp3"] = "audio/mpeg",
    };

    private readonly string _root;
    private readonly ILogger? _logger;
    private readonly DataPackArchive? _packs;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>?> _directories = new();

    public GameFiles(string root, ILogger? logger = null)
    {
        this._root = Path.GetFullPath(root);
        this._logger = logger;
        this._packs = DataPackArchive.TryLoad(Path.Combine(this._root, "packs"));
    }

    public string Root => this._root;

    public bool UsingPacks => this._packs is not null;

    public int PackCount => this._packs?.PackCount ?? 0;

    public int PackedEntryCount => this._packs?.EntryCount ?? 0;

    public void Dispose() => this._packs?.Dispose();

    public async Task ServeAsync(HttpContext context, string relativePath)
    {
        if (this._packs is not null)
        {
            await this.ServeFromPackAsync(context, this._packs, relativePath).ConfigureAwait(false);
            return;
        }

        var file = this.Resolve(relativePath);
        if (file is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var bytes = await File.ReadAllBytesAsync(file, context.RequestAborted).ConfigureAwait(false);
        var (offset, contentType) = Unwrap(file, bytes);
        context.Response.ContentType = contentType;
        context.Response.Headers.CacheControl = "public, max-age=86400";
        await context.Response.Body.WriteAsync(bytes.AsMemory(offset), context.RequestAborted).ConfigureAwait(false);
    }

    private async Task ServeFromPackAsync(HttpContext context, DataPackArchive packs, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (normalized is null || !TryResolveInPack(packs, normalized, out var entry))
        {
            this._logger?.LogWarning("Data file missing from packs: {Path}", relativePath);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.Headers.ETag = entry.ETag;
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        context.Response.Headers.AcceptRanges = "bytes";

        var ifNoneMatch = context.Request.Headers.IfNoneMatch;
        for (var i = 0; i < ifNoneMatch.Count; i++)
        {
            var tag = ifNoneMatch[i]?.Trim();
            if (tag == "*" || string.Equals(tag, entry.ETag, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }
        }

        var totalLength = entry.Length;
        var rangeStart = 0L;
        var rangeLength = totalLength;
        var isPartial = false;

        var rangeHeader = context.Request.GetTypedHeaders().Range;
        if (rangeHeader is { Unit.Value: "bytes", Ranges.Count: 1 })
        {
            var range = rangeHeader.Ranges.First();
            if (totalLength == 0)
            {
                context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                context.Response.Headers.ContentRange = "bytes */0";
                return;
            }

            if (range.From.HasValue)
            {
                rangeStart = range.From.Value;
                var end = range.To.HasValue ? Math.Min(range.To.Value, totalLength - 1) : totalLength - 1;
                if (rangeStart >= totalLength || end < rangeStart)
                {
                    context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                    context.Response.Headers.ContentRange = $"bytes */{totalLength}";
                    return;
                }

                rangeLength = end - rangeStart + 1;
            }
            else if (range.To.HasValue)
            {
                var suffix = range.To.Value;
                if (suffix <= 0)
                {
                    context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                    context.Response.Headers.ContentRange = $"bytes */{totalLength}";
                    return;
                }

                rangeLength = Math.Min(suffix, totalLength);
                rangeStart = totalLength - rangeLength;
            }

            isPartial = true;
        }

        context.Response.ContentType = entry.ContentType;
        context.Response.ContentLength = rangeLength;
        if (isPartial)
        {
            context.Response.StatusCode = StatusCodes.Status206PartialContent;
            context.Response.Headers.ContentRange = $"bytes {rangeStart}-{rangeStart + rangeLength - 1}/{totalLength}";
        }

        if (HttpMethods.IsHead(context.Request.Method) || rangeLength == 0)
        {
            return;
        }

        const int chunkSize = 65536;
        var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(chunkSize, rangeLength));
        try
        {
            var remaining = rangeLength;
            var fileOffset = entry.Offset + rangeStart;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = await RandomAccess.ReadAsync(entry.Handle, buffer.AsMemory(0, toRead), fileOffset, context.RequestAborted).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await context.Response.Body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted).ConfigureAwait(false);
                fileOffset += read;
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryResolveInPack(DataPackArchive packs, string lowerPath, out PackEntry entry)
    {
        if (packs.TryGetEntry(lowerPath, out entry))
        {
            return true;
        }

        var extension = Path.GetExtension(lowerPath);
        return FallbackExtensions.TryGetValue(extension, out var fallback)
            && packs.TryGetEntry(lowerPath[..^extension.Length] + fallback, out entry);
    }

    private static string? NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\\'))
        {
            return null;
        }

        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        foreach (var part in parts)
        {
            if (part is "." or "..")
            {
                return null;
            }
        }

        return string.Join('/', parts).ToLowerInvariant();
    }

    internal static (int Offset, string ContentType) Unwrap(string file, byte[] bytes)
    {
        var extension = Path.GetExtension(file);
        if (extension.Equals(".ozj", StringComparison.OrdinalIgnoreCase))
        {
            var wrapped = bytes.Length > OzjPrefixBytes + 1 && bytes[OzjPrefixBytes] == 0xFF && bytes[OzjPrefixBytes + 1] == 0xD8;
            return (wrapped ? OzjPrefixBytes : 0, "image/jpeg");
        }

        if (extension.Equals(".ozt", StringComparison.OrdinalIgnoreCase))
        {
            return (Math.Min(OztPrefixBytes, bytes.Length), "image/x-tga");
        }

        if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
        {
            return (0, "image/x-tga");
        }

        return (0, ContentTypes.GetValueOrDefault(extension, "application/octet-stream"));
    }

    private string? Resolve(string relativePath)
    {
        var direct = this.ResolveIgnoringCase(relativePath);
        if (direct is not null)
        {
            return direct;
        }

        var extension = Path.GetExtension(relativePath);
        return FallbackExtensions.TryGetValue(extension, out var fallback)
            ? this.ResolveIgnoringCase(relativePath[..^extension.Length] + fallback)
            : null;
    }

    // Walks the path one folder at a time, ignoring case; refuses anything that would leave the root.
    private string? ResolveIgnoringCase(string relativePath)
    {
        var current = this._root;
        foreach (var part in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is "." or ".." || part.Contains('\\'))
            {
                return null;
            }

            var entries = this._directories.GetOrAdd(current, ListDirectory);
            if (entries is null || !entries.TryGetValue(part, out var realName))
            {
                return null;
            }

            current = Path.Combine(current, realName);
        }

        return File.Exists(current) ? current : null;
    }

    private static Dictionary<string, string>? ListDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            entries.TryAdd(Path.GetFileName(entry), Path.GetFileName(entry));
        }

        return entries;
    }
}
