// <copyright file="ItemIconEndpoint.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel;

using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Serves item editor icons from a single uncompressed archive (<c>items.zip</c>) when packaged,
/// falling back to the loose static web assets in development.
/// </summary>
internal static class ItemIconEndpoint
{
    private const string ItemContentPrefix = "_content/HNMO.Web.ItemEditor/img/items/";

    /// <summary>
    /// Maps the item icon routes on the admin panel application.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapItemIconEndpoints(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var archivePath = Path.Combine(webRoot, "_content", "HNMO.Web.ItemEditor", "img", "items.zip");
        var archive = File.Exists(archivePath) ? ItemIconArchive.Load(archivePath) : null;
        if (archive is not null)
        {
            app.Lifetime.ApplicationStopping.Register(archive.Dispose);
        }

        async Task HandleAsync(HttpContext context, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (archive is not null)
            {
                await archive.ServeAsync(context, fileName).ConfigureAwait(false);
                return;
            }

            var fileInfo = app.Environment.WebRootFileProvider.GetFileInfo(ItemContentPrefix + fileName);
            if (!fileInfo.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "image/png";
            context.Response.Headers.CacheControl = "public, max-age=86400";
            await using var stream = fileInfo.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
        }

        app.MapGet("/item-icons/{fileName}", HandleAsync).AllowAnonymous();
        app.MapGet("/_content/HNMO.Web.ItemEditor/img/items/{fileName}", HandleAsync).AllowAnonymous();
    }

    private sealed class ItemIconArchive : IDisposable
    {
        private readonly SafeFileHandle _handle;
        private readonly Dictionary<string, (long Offset, long Length, string ETag)> _entries;

        private ItemIconArchive(SafeFileHandle handle, Dictionary<string, (long Offset, long Length, string ETag)> entries)
        {
            this._handle = handle;
            this._entries = entries;
        }

        public static ItemIconArchive Load(string zipPath)
        {
            byte[] hash;
            using (var hashStream = File.OpenRead(zipPath))
            {
                hash = SHA256.HashData(hashStream);
            }

            var hashPrefix = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
            var entries = new Dictionary<string, (long Offset, long Length, string ETag)>(StringComparer.OrdinalIgnoreCase);

            using (var zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false))
            {
                var index = 0;
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) || entry.Length != entry.CompressedLength)
                    {
                        continue;
                    }

                    entries[entry.Name] = (GetDataOffset(zipStream, entry), entry.Length, $"\"{hashPrefix}-{index}\"");
                    index++;
                }
            }

            var handle = File.OpenHandle(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
            return new ItemIconArchive(handle, entries);
        }

        public void Dispose() => this._handle.Dispose();

        public async Task ServeAsync(HttpContext context, string fileName)
        {
            if (!this._entries.TryGetValue(fileName, out var entry))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.Headers.ETag = entry.ETag;
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

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

            context.Response.ContentType = "image/png";
            context.Response.ContentLength = entry.Length;

            if (HttpMethods.IsHead(context.Request.Method) || entry.Length == 0)
            {
                return;
            }

            var size = (int)entry.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                var read = 0;
                while (read < size)
                {
                    var n = await RandomAccess.ReadAsync(this._handle, buffer.AsMemory(read, size - read), entry.Offset + read, context.RequestAborted).ConfigureAwait(false);
                    if (n == 0)
                    {
                        break;
                    }

                    read += n;
                }

                await context.Response.Body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static long GetDataOffset(Stream zipStream, ZipArchiveEntry entry)
        {
            // Open() positions the internal stream at the start of the entry's stored payload.
            using var entryStream = entry.Open();
            Span<byte> one = stackalloc byte[1];
            _ = entryStream.Read(one);
            return zipStream.Position - 1;
        }
    }
}
