// <copyright file="WorldEditorDataService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    private const uint PackFormatVersion = 1;
    private const byte InitialMapKey = 0x5E;
    private const byte MapKeyIncrement = 0x3D;

    private static readonly byte[] PackMagicBytes = "HPK1"u8.ToArray();
    private static readonly byte[] MapXorKey = [0xD1, 0x73, 0x52, 0xF6, 0xD2, 0x9A, 0xCB, 0x27, 0x3E, 0xAF, 0x59, 0x31, 0x37, 0xB3, 0xE7, 0xA2];

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

    /// <summary>Encrypts raw world binary data (<c>EncTerrainN.obj</c> / <c>.map</c>) using the client map cipher.</summary>
    /// <param name="rawData">Plaintext buffer.</param>
    /// <returns>Encrypted buffer.</returns>
    public static byte[] EncryptMapBuffer(ReadOnlySpan<byte> rawData)
    {
        var encrypted = rawData.ToArray();
        byte mapKey = InitialMapKey;
        for (var i = 0; i < encrypted.Length; i++)
        {
            encrypted[i] = (byte)((encrypted[i] + mapKey) ^ MapXorKey[i % MapXorKey.Length]);
            mapKey = (byte)(encrypted[i] + MapKeyIncrement);
        }

        return encrypted;
    }

    /// <summary>Encodes top-down RGBA pixels into an uncompressed 32-bit bottom-up TGA image.</summary>
    /// <param name="rgba">RGBA pixels (<c>width * height * 4</c>).</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>Complete TGA byte payload.</returns>
    public static byte[] EncodeUncompressedTga(ReadOnlySpan<byte> rgba, int width, int height)
    {
        var expected = width * height * 4;
        if (width <= 0 || height <= 0 || rgba.Length != expected)
        {
            throw new ArgumentException($"Expected {expected} RGBA bytes for {width}x{height} TGA.", nameof(rgba));
        }

        var tga = new byte[18 + expected];
        tga[2] = 2; // uncompressed true-color image
        BinaryPrimitives.WriteUInt16LittleEndian(tga.AsSpan(12, 2), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(tga.AsSpan(14, 2), (ushort)height);
        tga[16] = 32; // 32-bit BGRA
        tga[17] = 8;  // 8-bit alpha, bottom-left origin

        var dst = 18;
        for (var y = height - 1; y >= 0; y--)
        {
            var row = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var src = row + (x * 4);
                tga[dst++] = rgba[src + 2]; // B
                tga[dst++] = rgba[src + 1]; // G
                tga[dst++] = rgba[src];     // R
                tga[dst++] = rgba[src + 3]; // A
            }
        }

        return tga;
    }

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
        return this.WriteWorldFileToRoots(folder, $"EncTerrain{folder}.att", bytes);
    }

    /// <summary>Encrypts raw world object records (<c>EncTerrainN.obj</c>) and writes to web and server data folders.</summary>
    /// <param name="mapNumber">Map number.</param>
    /// <param name="rawObjectData">Unencrypted 4-byte header + N x 30-byte records.</param>
    /// <returns>List of written file paths.</returns>
    public IReadOnlyList<string> WriteClientObjects(short mapNumber, ReadOnlySpan<byte> rawObjectData)
    {
        if (rawObjectData.Length < 4 || ((rawObjectData.Length - 4) % 30) != 0)
        {
            throw new ArgumentException("Invalid world object buffer length.", nameof(rawObjectData));
        }

        var encrypted = EncryptMapBuffer(rawObjectData);
        var folder = GetWorldFolderNumber(mapNumber);
        return this.WriteWorldFileToRoots(folder, $"EncTerrain{folder}.obj", encrypted);
    }

    /// <summary>Writes updated <c>TerrainLight.jpg</c> lightmap files to web and server data folders.</summary>
    /// <param name="mapNumber">Map number.</param>
    /// <param name="jpegBytes">JPEG bytes.</param>
    /// <returns>List of written file paths.</returns>
    public IReadOnlyList<string> WriteClientLightMap(short mapNumber, byte[] jpegBytes)
    {
        if (jpegBytes is null || jpegBytes.Length < 4 || jpegBytes[0] != 0xFF || jpegBytes[1] != 0xD8)
        {
            throw new ArgumentException("Invalid JPEG lightmap payload.", nameof(jpegBytes));
        }

        var folder = GetWorldFolderNumber(mapNumber);
        return this.WriteWorldFileToRoots(folder, "TerrainLight.jpg", jpegBytes);
    }

    /// <summary>Encodes and writes regenerated <c>mini_map.tga</c> (and <c>.ozt</c> for server data) across web and server roots.</summary>
    /// <param name="mapNumber">Map number.</param>
    /// <param name="rgbaBytes">Top-down RGBA pixel bytes.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <returns>List of written file paths.</returns>
    public IReadOnlyList<string> WriteClientMinimap(short mapNumber, ReadOnlySpan<byte> rgbaBytes, int width, int height)
    {
        var tga = EncodeUncompressedTga(rgbaBytes, width, height);
        var folder = GetWorldFolderNumber(mapNumber);
        var relDir = $"World{folder}";
        var written = new List<string>();
        var (serverDir, webDir) = this.ResolveDirectories();

        if (!string.IsNullOrWhiteSpace(webDir))
        {
            var webPath = Path.Combine(webDir, relDir, "mini_map.tga");
            Directory.CreateDirectory(Path.GetDirectoryName(webPath)!);
            WriteAtomic(webPath, tga);
            written.Add(webPath);
        }

        if (!string.IsNullOrWhiteSpace(serverDir))
        {
            var ozt = new byte[OztPrefixBytes + tga.Length];
            tga.CopyTo(ozt.AsSpan(OztPrefixBytes));
            var serverPath = Path.Combine(serverDir, relDir, "mini_map.ozt");
            Directory.CreateDirectory(Path.GetDirectoryName(serverPath)!);
            WriteAtomic(serverPath, ozt);
            written.Add(serverPath);
        }

        return written;
    }

    /// <summary>Rebuilds the map's <c>worldN.hpk</c> pack archive combining existing packed entries with edited loose files.</summary>
    /// <param name="mapNumber">Map number.</param>
    /// <returns>Tuple of pack path and packed entry count.</returns>
    public (string PackPath, int EntryCount) RebuildWorldPack(short mapNumber)
    {
        var folder = GetWorldFolderNumber(mapNumber);
        var (serverDir, webDir) = this.ResolveDirectories();
        var packRoot = !string.IsNullOrWhiteSpace(serverDir)
            ? serverDir
            : !string.IsNullOrWhiteSpace(webDir)
                ? webDir
                : throw new InvalidOperationException("No data directory is configured.");

        var packsDir = Path.Combine(packRoot, "packs");
        if (!Directory.Exists(packsDir))
        {
            var siblingPacks = Path.GetFullPath(Path.Combine(packRoot, "..", "packs"));
            if (Directory.Exists(siblingPacks))
            {
                packsDir = siblingPacks;
            }
        }

        Directory.CreateDirectory(packsDir);
        var packPath = Path.Combine(packsDir, $"world{folder}.hpk");

        var entries = new Dictionary<string, (string OrigPath, byte[] Payload, byte ContentCode)>(StringComparer.Ordinal);
        if (File.Exists(packPath))
        {
            ReadPackEntries(packPath, entries);
        }

        foreach (var root in new[] { serverDir, webDir })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var sub in new[] { $"World{folder}", $"Object{folder}" })
            {
                var dir = Path.Combine(root, sub);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var relPath = Path.GetRelativePath(root, file).Replace('\\', '/');
                    var raw = File.ReadAllBytes(file);
                    var (unwrappedBytes, mime) = Unwrap(file, raw);
                    var code = EncodeContentType(mime);
                    entries[relPath.ToLowerInvariant()] = (relPath, unwrappedBytes, code);
                }
            }
        }

        var ordered = entries.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, kv.Value.OrigPath, kv.Value.Payload, kv.Value.ContentCode)).ToList();
        WriteHpkArchive(packPath, ordered);
        return (packPath, ordered.Count);
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
                    return (bytes, DecodePackContentType(contentCode));
                }
            }
        }

        return null;
    }

    private static string DecodePackContentType(byte contentCode) => contentCode switch
    {
        1 => "image/jpeg",
        2 => "image/x-tga",
        3 => "application/json; charset=utf-8",
        4 => "image/png",
        5 => "audio/ogg",
        6 => "audio/wav",
        7 => "audio/mpeg",
        8 => "image/ktx2",
        _ => "application/octet-stream",
    };

    private static byte EncodeContentType(string contentType) => contentType switch
    {
        "image/jpeg" => 1,
        "image/tga" or "image/x-tga" => 2,
        "application/json; charset=utf-8" => 3,
        "image/png" => 4,
        "audio/ogg" => 5,
        "audio/wav" => 6,
        "audio/mpeg" => 7,
        "image/ktx2" => 8,
        _ => 0,
    };

    private static void ReadPackEntries(
        string packPath,
        Dictionary<string, (string OrigPath, byte[] Payload, byte ContentCode)> entries)
    {
        using var stream = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < PackHeaderSize)
        {
            return;
        }

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != PackMagicBytes[0] || magic[1] != PackMagicBytes[1] || magic[2] != PackMagicBytes[2] || magic[3] != PackMagicBytes[3])
        {
            return;
        }

        _ = reader.ReadUInt32(); // version
        var count = reader.ReadUInt32();
        stream.Position = PackHeaderSize;

        var meta = new List<(string Lower, string Orig, long Offset, long Length, byte Code)>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var lowerLen = reader.ReadUInt16();
            var lowerPath = Encoding.UTF8.GetString(reader.ReadBytes(lowerLen));
            var origLen = reader.ReadUInt16();
            var origPath = Encoding.UTF8.GetString(reader.ReadBytes(origLen));
            var offset = reader.ReadInt64();
            var length = reader.ReadInt64();
            var code = reader.ReadByte();
            meta.Add((lowerPath, origPath, offset, length, code));
        }

        foreach (var item in meta)
        {
            stream.Position = item.Offset;
            var payload = reader.ReadBytes(checked((int)item.Length));
            entries[item.Lower] = (item.Orig, payload, item.Code);
        }
    }

    private static void WriteHpkArchive(
        string packPath,
        List<(string LowerPath, string OrigPath, byte[] Payload, byte ContentCode)> items)
    {
        var encoded = new List<(byte[] LowerBytes, byte[] OrigBytes, byte[] Payload, byte Code)>(items.Count);
        long indexSize = 0;
        foreach (var item in items)
        {
            var lowerBytes = Encoding.UTF8.GetBytes(item.LowerPath);
            var origBytes = Encoding.UTF8.GetBytes(item.OrigPath);
            encoded.Add((lowerBytes, origBytes, item.Payload, item.ContentCode));
            indexSize += 2 + lowerBytes.Length + 2 + origBytes.Length + 8 + 8 + 1;
        }

        var tempPath = $"{packPath}.{Guid.NewGuid():N}.tmp";
        var payloadStart = PackHeaderSize + indexSize;
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 65536))
        {
            stream.Write(new byte[PackHeaderSize]);
            var offset = payloadStart;
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                foreach (var e in encoded)
                {
                    writer.Write((ushort)e.LowerBytes.Length);
                    writer.Write(e.LowerBytes);
                    writer.Write((ushort)e.OrigBytes.Length);
                    writer.Write(e.OrigBytes);
                    writer.Write(offset);
                    writer.Write((long)e.Payload.Length);
                    writer.Write(e.Code);
                    offset += e.Payload.Length;
                }
            }

            foreach (var e in encoded)
            {
                stream.Write(e.Payload);
            }

            stream.Flush();
            stream.Position = PackHeaderSize;
            var hash = SHA256.HashData(stream);

            Span<byte> header = stackalloc byte[PackHeaderSize];
            PackMagicBytes.CopyTo(header[..4]);
            BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], PackFormatVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(header[8..12], (uint)encoded.Count);
            BinaryPrimitives.WriteUInt32LittleEndian(header[12..16], 0);
            hash.CopyTo(header[16..48]);

            stream.Position = 0;
            stream.Write(header);
        }

        File.Move(tempPath, packPath, overwrite: true);
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

    private IReadOnlyList<string> WriteWorldFileToRoots(int folder, string fileName, byte[] bytes)
    {
        var relDir = $"World{folder}";
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
                this._logger?.LogWarning(ex, "Could not write world editor file {FileName} under {Root}", fileName, root);
            }
        }

        return written;
    }
}
