using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed class DataPackArchive : IDisposable
{
    public const int HeaderSize = 48;
    public const uint FormatVersion = 1;
    private static readonly byte[] MagicBytes = "HPK1"u8.ToArray();

    private static readonly HashSet<string> ManifestFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Audio", "Buffs", "Effects", "Grading", "Items", "Maps", "Models", "Monsters", "Render",
    };

    private readonly List<SafeFileHandle> _handles;
    private readonly Dictionary<string, PackEntry> _entries;

    private DataPackArchive(List<SafeFileHandle> handles, Dictionary<string, PackEntry> entries)
    {
        this._handles = handles;
        this._entries = entries;
    }

    public int PackCount => this._handles.Count;

    public int EntryCount => this._entries.Count;

    public bool TryGetEntry(string lowerRelativePath, out PackEntry entry) =>
        this._entries.TryGetValue(lowerRelativePath, out entry);

    public void Dispose()
    {
        foreach (var handle in this._handles)
        {
            handle.Dispose();
        }
    }

    public static DataPackArchive? TryLoad(string packsDirectory)
    {
        if (!Directory.Exists(packsDirectory))
        {
            return null;
        }

        var packFiles = Directory.GetFiles(packsDirectory, "*.hpk", SearchOption.TopDirectoryOnly);
        if (packFiles.Length == 0)
        {
            return null;
        }

        Array.Sort(packFiles, StringComparer.OrdinalIgnoreCase);
        var handles = new List<SafeFileHandle>(packFiles.Length);
        var entries = new Dictionary<string, PackEntry>(StringComparer.Ordinal);
        Span<byte> header = stackalloc byte[HeaderSize];

        try
        {
            foreach (var packFile in packFiles)
            {
                var handle = File.OpenHandle(packFile, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
                handles.Add(handle);

                ReadExact(handle, header, 0);
                if (!header[..4].SequenceEqual(MagicBytes))
                {
                    throw new InvalidDataException($"Invalid pack magic in '{packFile}'.");
                }

                var version = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
                if (version != FormatVersion)
                {
                    throw new InvalidDataException($"Unsupported pack version {version} in '{packFile}'.");
                }

                var entryCount = BinaryPrimitives.ReadUInt32LittleEndian(header[8..12]);
                var hashPrefix = Convert.ToHexString(header.Slice(16, 8)).ToLowerInvariant();

                using var stream = new FileStream(packFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536);
                stream.Position = HeaderSize;
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

                for (uint i = 0; i < entryCount; i++)
                {
                    var lowerLen = reader.ReadUInt16();
                    var lowerPath = Encoding.UTF8.GetString(reader.ReadBytes(lowerLen));
                    var origLen = reader.ReadUInt16();
                    var origPath = Encoding.UTF8.GetString(reader.ReadBytes(origLen));
                    var offset = reader.ReadInt64();
                    var length = reader.ReadInt64();
                    var contentTypeCode = reader.ReadByte();

                    var etag = $"\"{hashPrefix}-{i}\"";
                    entries[lowerPath] = new PackEntry(
                        handle,
                        offset,
                        length,
                        origPath,
                        etag,
                        DecodeContentType(contentTypeCode, origPath));
                }
            }

            return new DataPackArchive(handles, entries);
        }
        catch
        {
            foreach (var handle in handles)
            {
                handle.Dispose();
            }

            throw;
        }
    }

    public static void BuildPacks(string dataRoot, string outputPacksDirectory, string? allowlistPath)
    {
        var fullDataRoot = Path.GetFullPath(dataRoot);
        HashSet<string>? allowlist = null;
        if (!string.IsNullOrWhiteSpace(allowlistPath))
        {
            allowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(allowlistPath))
            {
                var line = rawLine.Trim().Replace('\\', '/').TrimStart('/');
                if (line.Length > 0 && !line.StartsWith('#'))
                {
                    allowlist.Add(line);
                }
            }
        }

        if (Directory.Exists(outputPacksDirectory))
        {
            Directory.Delete(outputPacksDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputPacksDirectory);

        var groups = new Dictionary<string, List<(string FullPath, string RelPath)>>(StringComparer.OrdinalIgnoreCase);
        long sourceBytes = 0;
        int sourceFiles = 0;

        foreach (var file in Directory.EnumerateFiles(fullDataRoot, "*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(fullDataRoot, file).Replace('\\', '/');
            if (relPath.StartsWith("packs/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (allowlist is not null && !allowlist.Contains(relPath))
            {
                continue;
            }

            sourceFiles++;
            sourceBytes += new FileInfo(file).Length;
            var groupName = GetPackGroupName(relPath);
            if (!groups.TryGetValue(groupName, out var list))
            {
                list = [];
                groups[groupName] = list;
            }

            list.Add((file, relPath));
        }

        long packedBytes = 0;
        foreach (var (groupName, files) in groups.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            files.Sort((a, b) => string.Compare(a.RelPath, b.RelPath, StringComparison.OrdinalIgnoreCase));
            var outPath = Path.Combine(outputPacksDirectory, $"{groupName}.hpk");
            WriteSinglePack(outPath, files);
            packedBytes += new FileInfo(outPath).Length;
        }

        Console.WriteLine($"Packed {sourceFiles} files ({sourceBytes / (1024.0 * 1024.0):F2} MB) into {groups.Count} packs ({packedBytes / (1024.0 * 1024.0):F2} MB) at {outputPacksDirectory}");
    }

    public static void BuildItemIconsZip(string itemsDirectory, string outputZipPath)
    {
        if (!Directory.Exists(itemsDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(itemsDirectory, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        if (File.Exists(outputZipPath))
        {
            File.Delete(outputZipPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputZipPath))!);
        long sourceBytes = 0;
        using (var zip = ZipFile.Open(outputZipPath, ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                sourceBytes += new FileInfo(file).Length;
                zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.NoCompression);
            }
        }

        var zipBytes = new FileInfo(outputZipPath).Length;
        Console.WriteLine($"Packed {files.Length} item icons ({sourceBytes / (1024.0 * 1024.0):F2} MB) into {outputZipPath} ({zipBytes / (1024.0 * 1024.0):F2} MB)");
    }

    private static void WriteSinglePack(string packPath, List<(string FullPath, string RelPath)> files)
    {
        var indexEntries = new List<(byte[] LowerBytes, byte[] OrigBytes, long PayloadLength, byte ContentTypeCode, byte[] Payload)>(files.Count);
        long indexSize = 0;

        foreach (var (fullPath, relPath) in files)
        {
            var lower = relPath.ToLowerInvariant();
            var lowerBytes = Encoding.UTF8.GetBytes(lower);
            var origBytes = Encoding.UTF8.GetBytes(relPath);
            var raw = File.ReadAllBytes(fullPath);
            var (offset, contentType) = GameFiles.Unwrap(fullPath, raw);
            var payload = offset == 0 ? raw : raw[offset..];
            var code = EncodeContentType(contentType);

            indexEntries.Add((lowerBytes, origBytes, payload.Length, code, payload));
            indexSize += 2 + lowerBytes.Length + 2 + origBytes.Length + 8 + 8 + 1;
        }

        var payloadStart = HeaderSize + indexSize;
        using var stream = new FileStream(packPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 65536);
        stream.Write(new byte[HeaderSize]);

        var currentOffset = payloadStart;
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var entry in indexEntries)
            {
                writer.Write((ushort)entry.LowerBytes.Length);
                writer.Write(entry.LowerBytes);
                writer.Write((ushort)entry.OrigBytes.Length);
                writer.Write(entry.OrigBytes);
                writer.Write(currentOffset);
                writer.Write(entry.PayloadLength);
                writer.Write(entry.ContentTypeCode);
                currentOffset += entry.PayloadLength;
            }
        }

        foreach (var entry in indexEntries)
        {
            stream.Write(entry.Payload);
        }

        stream.Flush();
        stream.Position = HeaderSize;
        var hash = SHA256.HashData(stream);

        Span<byte> header = stackalloc byte[HeaderSize];
        MagicBytes.CopyTo(header[..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], FormatVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..12], (uint)indexEntries.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..16], 0);
        hash.CopyTo(header[16..48]);

        stream.Position = 0;
        stream.Write(header);
    }

    private static string GetPackGroupName(string relPath)
    {
        var slash = relPath.IndexOf('/');
        if (slash < 0)
        {
            return "root";
        }

        var top = relPath[..slash];
        if (ManifestFolders.Contains(top))
        {
            return "manifests";
        }

        if (top.StartsWith("World", StringComparison.OrdinalIgnoreCase) && int.TryParse(top[5..], out var worldNum))
        {
            return $"world{worldNum}";
        }

        if (top.StartsWith("Object", StringComparison.OrdinalIgnoreCase) && int.TryParse(top[6..], out var objNum))
        {
            return $"world{objNum}";
        }

        return top.ToLowerInvariant();
    }

    private static void ReadExact(SafeFileHandle handle, Span<byte> buffer, long offset)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = RandomAccess.Read(handle, buffer[total..], offset + total);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of pack header.");
            }

            total += read;
        }
    }

    private static byte EncodeContentType(string contentType) => contentType switch
    {
        "image/jpeg" => 1,
        "image/x-tga" => 2,
        "application/json; charset=utf-8" => 3,
        "image/png" => 4,
        "audio/ogg" => 5,
        "audio/wav" => 6,
        "audio/mpeg" => 7,
        _ => 0,
    };

    private static string DecodeContentType(byte code, string origPath) => code switch
    {
        1 => "image/jpeg",
        2 => "image/x-tga",
        3 => "application/json; charset=utf-8",
        4 => "image/png",
        5 => "audio/ogg",
        6 => "audio/wav",
        7 => "audio/mpeg",
        _ => Path.GetExtension(origPath).ToLowerInvariant() switch
        {
            ".opus" => "audio/ogg",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream",
        },
    };
}

internal readonly record struct PackEntry(
    SafeFileHandle Handle,
    long Offset,
    long Length,
    string OriginalPath,
    string ETag,
    string ContentType);
