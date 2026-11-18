// <copyright file="WorldEditorDataService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HNMO.Web.Shared.Components.TerrainEditor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves game world data assets and synchronizes saved terrain changes to both the
/// server data folder and the web client's data directory (ROADMAP 14.24).
/// </summary>
public sealed class WorldEditorDataService
{
    private const int OzjPrefixBytes = 24;
    private const int OztPrefixBytes = 4;
    private const int PackHeaderSize = 48;

    private static readonly Dictionary<short, int> SharedWorldFolders = new()
    {
        [9] = 10, [32] = 10,
        [11] = 12, [12] = 12, [13] = 12, [14] = 12, [15] = 12, [16] = 12, [17] = 12, [52] = 12,
        [18] = 19, [19] = 19, [20] = 19, [21] = 19, [22] = 19, [23] = 19, [53] = 19,
        [24] = 25, [25] = 25, [26] = 25, [27] = 25, [28] = 25, [29] = 25, [36] = 25,
        [45] = 47, [46] = 47, [47] = 47, [48] = 47, [49] = 47, [50] = 47,
    };

    private static readonly Dictionary<string, string> FallbackExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ".ozj",
        [".jpeg"] = ".ozj",
        [".tga"] = ".ozt",
    };

    private readonly IHostSettingsService? _hostSettings;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<WorldEditorDataService>? _logger;
    private readonly string? _explicitServerDataDir;
    private readonly string? _explicitWebDataDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldEditorDataService"/> class.
    /// </summary>
    /// <param name="hostSettings">Optional host settings service.</param>
    /// <param name="configuration">Optional application configuration.</param>
    /// <param name="logger">Optional logger.</param>
    public WorldEditorDataService(
        IHostSettingsService? hostSettings = null,
        IConfiguration? configuration = null,
        ILogger<WorldEditorDataService>? logger = null)
    {
        this._hostSettings = hostSettings;
        this._configuration = configuration;
        this._logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldEditorDataService"/> class with explicit paths for testing.
    /// </summary>
    /// <param name="serverDataDirectory">The server data directory.</param>
    /// <param name="webDataDirectory">The web client data directory.</param>
    public WorldEditorDataService(string serverDataDirectory, string webDataDirectory)
    {
        this._explicitServerDataDir = serverDataDirectory;
        this._explicitWebDataDir = webDataDirectory;
    }

    /// <summary>Gets the client world folder index (<c>WorldN</c> / <c>ObjectN</c>) for a map number.</summary>
    /// <param name="mapNumber">Zero-based map number.</param>
    /// <returns>One-based folder index.</returns>
    public static int GetWorldFolderNumber(short mapNumber) =>
        SharedWorldFolders.TryGetValue(mapNumber, out var shared) ? shared : mapNumber + 1;

    /// <summary>Determines whether client height/mapping assets exist for a map so full 3D rendering can load.</summary>
    /// <param name="mapNumber">Map number.</param>
    /// <returns><c>true</c> if <c>TerrainHeight.OZB</c> can be resolved.</returns>
    public bool HasWorldAssets(short mapNumber)
    {
        var folder = GetWorldFolderNumber(mapNumber);
        return this.TryResolveDataFilePath($"World{folder}/TerrainHeight.OZB") is not null;
    }

    /// <summary>Reads a relative data file (<c>World1/...</c> or <c>Object1/...</c>), unwrapping <c>.ozj</c>/<c>.ozt</c> when needed.</summary>
    /// <param name="relativePath">Relative path beneath the data root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of bytes and MIME content type, or <c>null</c> when missing or unsafe.</returns>
    public async Task<(byte[] Bytes, string ContentType)?> TryReadDataBytesAsync(string relativePath, CancellationToken cancellationToken)
    {
        var parts = SanitizePathParts(relativePath);
        if (parts is null || parts.Length == 0)
        {
            return null;
        }

        var (serverDir, webDir) = this.ResolveDirectories();
        foreach (var root in new[] { webDir, serverDir })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            if (TryResolveLooseFile(root, parts) is { } filePath)
            {
                var raw = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
                return Unwrap(filePath, raw);
            }
        }

        if (!string.IsNullOrWhiteSpace(serverDir))
        {
            var packsDir = Path.Combine(serverDir, "packs");
            if (!Directory.Exists(packsDir))
            {
                packsDir = Path.GetFullPath(Path.Combine(serverDir, "..", "packs"));
            }

            if (Directory.Exists(packsDir) && TryReadFromPacks(packsDir, string.Join('/', parts)) is { } packed)
            {
                return packed;
            }
        }

        return null;
    }

    /// <summary>Writes encrypted client <c>EncTerrainN.att</c> files to web and server data folders.</summary>
    /// <param name="mapNumber">Map number.</param>
    /// <param name="tileAttributes">65,536 tile attribute bytes.</param>
    /// <returns>List of written file paths.</returns>
    public IReadOnlyList<string> WriteClientAttributes(short mapNumber, ReadOnlySpan<byte> tileAttributes)
    {
        var bytes = TerrainFileConverter.ToClientFile(tileAttributes, mapNumber);
        var folder = GetWorldFolderNumber(mapNumber);
        var relDir = $"World{folder}";
        var fileName = $"EncTerrain{folder}.att";
        var written = new List<string>();
        var (serverDir, webDir) = this.ResolveDirectories();

        foreach (var root in new[] { webDir, serverDir })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            try
            {
                var targetDir = Path.Combine(root, relDir);
                Directory.CreateDirectory(targetDir);
                var targetFile = Path.Combine(targetDir, fileName);
                WriteAtomic(targetFile, bytes);
                written.Add(targetFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this._logger?.LogWarning(ex, "Could not write client terrain attributes to {Root}", root);
            }
        }

        return written;
    }

    private static string[]? SanitizePathParts(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.IndexOf('\0') >= 0)
        {
            return null;
        }

        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part is "." or ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return null;
            }
        }

        return parts;
    }

    private static string? TryResolveLooseFile(string root, string[] parts)
    {
        var dir = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var next = FindEntryCaseInsensitive(dir, parts[i], directoriesOnly: true);
            if (next is null)
            {
                return null;
            }

            dir = next;
        }

        var fileName = parts[^1];
        var direct = FindEntryCaseInsensitive(dir, fileName, directoriesOnly: false);
        if (direct is not null)
        {
            return direct;
        }

        var ext = Path.GetExtension(fileName);
        if (FallbackExtensions.TryGetValue(ext, out var wrappedExt))
        {
            return FindEntryCaseInsensitive(dir, Path.ChangeExtension(fileName, wrappedExt), directoriesOnly: false);
        }

        return null;
    }

    private static string? FindEntryCaseInsensitive(string parentDir, string targetName, bool directoriesOnly)
    {
        var exact = Path.Combine(parentDir, targetName);
        if (directoriesOnly ? Directory.Exists(exact) : File.Exists(exact))
        {
            return exact;
        }

        if (!Directory.Exists(parentDir))
        {
            return null;
        }

        IEnumerable<string> entries = directoriesOnly
            ? Directory.EnumerateDirectories(parentDir)
            : Directory.EnumerateFiles(parentDir);

        foreach (var entry in entries)
        {
            if (string.Equals(Path.GetFileName(entry), targetName, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static (byte[] Bytes, string ContentType) Unwrap(string filePath, byte[] raw)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".ozj" && raw.Length > OzjPrefixBytes)
        {
            return (raw[OzjPrefixBytes..], "image/jpeg");
        }

        if (ext == ".ozt" && raw.Length > OztPrefixBytes)
        {
            return (raw[OztPrefixBytes..], "image/tga");
        }

        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tga" => "image/tga",
            ".json" => "application/json; charset=utf-8",
            _ => "application/octet-stream",
        };
        return (raw, contentType);
    }

    private static (byte[] Bytes, string ContentType)? TryReadFromPacks(string packsDir, string relativeLower)
    {
        var normalized = relativeLower.ToLowerInvariant();
        var packFiles = Directory.GetFiles(packsDir, "*.hpk", SearchOption.TopDirectoryOnly);
        Array.Sort(packFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var packFile in packFiles)
        {
            using var stream = new FileStream(packFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < PackHeaderSize)
            {
                continue;
            }

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var magic = reader.ReadBytes(4);
            if (magic.Length != 4 || magic[0] != (byte)'H' || magic[1] != (byte)'P' || magic[2] != (byte)'K' || magic[3] != (byte)'1')
            {
                continue;
            }

            _ = reader.ReadUInt32(); // version
            var count = reader.ReadUInt32();
            stream.Position = PackHeaderSize;

            for (uint i = 0; i < count; i++)
            {
                var lowerLen = reader.ReadUInt16();
                var lowerPath = Encoding.UTF8.GetString(reader.ReadBytes(lowerLen));
                var origLen = reader.ReadUInt16();
                _ = reader.ReadBytes(origLen);
                var offset = reader.ReadInt64();
                var length = reader.ReadInt64();
                var contentCode = reader.ReadByte();

                if (string.Equals(lowerPath, normalized, StringComparison.Ordinal))
                {
                    stream.Position = offset;
                    var bytes = reader.ReadBytes(checked((int)length));
                    var ct = contentCode switch
                    {
                        1 => "image/jpeg",
                        2 => "image/png",
                        3 => "image/tga",
                        4 => "image/ktx2",
                        _ => "application/octet-stream",
                    };
                    return (bytes, ct);
                }
            }
        }

        return null;
    }

    private static void WriteAtomic(string destinationPath, byte[] bytes)
    {
        var temp = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, destinationPath, overwrite: true);
    }

    private static string? FindFirstExistingDir(params string[] candidates)
    {
        foreach (var dir in candidates)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                return dir;
            }
        }

        return null;
    }

    private (string? ServerDataDir, string? WebDataDir) ResolveDirectories()
    {
        if (this._explicitServerDataDir is not null || this._explicitWebDataDir is not null)
        {
            return (this._explicitServerDataDir, this._explicitWebDataDir);
        }

        string? serverDir = Environment.GetEnvironmentVariable("HNMO_DATA_PATH")
            ?? this._configuration?["HNMO:DataPath"];
        string? webDir = Environment.GetEnvironmentVariable("HNMO_WEB_DATA_PATH")
            ?? this._configuration?["HNMO:WebDataPath"];

        if (this._hostSettings is not null)
        {
            var settings = this._hostSettings.Load();
            var saveDir = Path.GetDirectoryName(this._hostSettings.SettingsFilePath) ?? AppContext.BaseDirectory;
            var rootDir = Path.GetFullPath(Path.Combine(saveDir, ".."));
            var bridgeDir = Directory.Exists(Path.Combine(rootDir, "web"))
                ? Path.Combine(rootDir, "web")
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "web"));

            serverDir ??= Path.GetFullPath(Path.Combine(bridgeDir, settings.DataPath));
            webDir ??= Path.GetFullPath(Path.Combine(bridgeDir, settings.WebRoot, "data"));
        }

        serverDir ??= FindFirstExistingDir(
            Path.Combine(AppContext.BaseDirectory, "Data"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Data")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "server", "src", "bin", "Data")));

        webDir ??= FindFirstExistingDir(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "web", "wwwroot", "data")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "client", "web", "public", "data")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "client", "web", "public", "data")));

        return (serverDir, webDir);
    }

    private string? TryResolveDataFilePath(string relativePath)
    {
        var parts = SanitizePathParts(relativePath);
        if (parts is null || parts.Length == 0)
        {
            return null;
        }

        var (serverDir, webDir) = this.ResolveDirectories();
        foreach (var root in new[] { webDir, serverDir })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            if (TryResolveLooseFile(root, parts) is { } filePath)
            {
                return filePath;
            }
        }

        return null;
    }
}
