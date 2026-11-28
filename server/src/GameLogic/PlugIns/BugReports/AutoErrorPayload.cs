// <copyright file="AutoErrorPayload.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Validates the automatic error packet and projects it to a small set of diagnostic fields.</summary>
public static class AutoErrorPayload
{
    /// <summary>The maximum JSON length, including ASCII escapes used by older web clients.</summary>
    public const int MaxJsonBytes = 16 * 1024;

    private const int HeaderBytes = 5;
    private static readonly Regex VersionPattern = new(@"\A(?:web [0-9]{8}|[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})\z", RegexOptions.CultureInvariant);
    private static readonly Regex BrowserPattern = new(@"\A(?:Chrome|Edge|Firefox|Opera|Safari) [0-9]{1,3}\z", RegexOptions.CultureInvariant);

    /// <summary>Checks the C2 F5 60 header and reads the flat JSON object without changing the packet.</summary>
    /// <param name="packet">The complete packet.</param>
    /// <param name="fingerprint">The untrusted diagnostic signature.</param>
    /// <param name="sample">The fields used for the report.</param>
    /// <returns>Whether the packet is valid.</returns>
    public static bool TryReadPacket(ReadOnlyMemory<byte> packet, out string fingerprint, out AutoErrorSample sample)
    {
        fingerprint = string.Empty;
        sample = new AutoErrorSample();
        var bytes = packet.Span;
        if (bytes.Length is <= HeaderBytes or > HeaderBytes + MaxJsonBytes + 1
            || bytes[0] != 0xC2 || bytes[3] != 0xF5 || bytes[4] != 0x60
            || BinaryPrimitives.ReadUInt16BigEndian(bytes[1..]) != bytes.Length)
        {
            return false;
        }

        // The web encoder has no terminator. The generated C# writer adds a single NUL.
        var json = bytes[^1] == 0 ? packet[HeaderBytes..^1] : packet[HeaderBytes..];
        if (json.Length > MaxJsonBytes || json.Span.Contains((byte)0))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(field.Name) || field.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                switch (field.Name)
                {
                    case "fingerprint": fingerprint = field.Value.GetString()!; break;
                    case "kind": sample.Kind = field.Value.GetString()!; break;
                    case "version": sample.Version = field.Value.GetString()!; break;
                    case "browser": sample.Browser = field.Value.GetString()!; break;
                    case "os": sample.Os = field.Value.GetString()!; break;
                    case "message" or "stack" or "device" or "map" or "gpu": break;
                    default: return false;
                }
            }

            return fingerprint.Length is > 0 and <= 512
                && !string.IsNullOrWhiteSpace(fingerprint)
                && seen.Contains("kind")
                && AutoErrorEngine.Kinds.Contains(sample.Kind);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    internal static string CleanKind(string? kind) => AutoErrorEngine.Kinds.Contains(kind ?? string.Empty) ? kind! : "error";

    internal static string Label(string kind) => kind switch
    {
        "rejection" => "Unhandled promise rejection",
        "packet" => "Packet handler failed",
        "webgl" => "WebGL context lost",
        "disconnect" => "Game connection lost",
        _ => "Uncaught browser error",
    };

    internal static string CleanFingerprint(string kind, string fingerprint)
    {
        if (kind is "disconnect" or "webgl")
        {
            return kind;
        }

        // Both legacy (message|path|line) and current (kind|path|line) clients end with the source and line.
        var last = fingerprint.LastIndexOf('|');
        var source = last > 0 ? fingerprint[..last] : string.Empty;
        var separator = Math.Max(source.LastIndexOf('|'), source.LastIndexOf('/'));
        var file = source[(separator + 1)..];
        if (file is not ("main.js" or "connection.js" or "session.js" or "world.js" or "renderer.js")
            || !int.TryParse(fingerprint.AsSpan(last + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var line)
            || line is < 1 or > 50000)
        {
            return kind;
        }

        return FormattableString.Invariant($"{kind}|{file}|{line}");
    }

    internal static string CleanVersion(string? version) =>
        version is { Length: <= 16 } && VersionPattern.IsMatch(version) ? version : string.Empty;

    internal static void CleanSample(AutoErrorSample sample)
    {
        sample.Kind = CleanKind(sample.Kind);
        sample.Message = Label(sample.Kind);
        sample.Stack = string.Empty;
        sample.Version = CleanVersion(sample.Version);
        sample.Browser = sample.Browser is { Length: <= 12 } && BrowserPattern.IsMatch(sample.Browser) ? sample.Browser : string.Empty;
        sample.Os = sample.Os is "Windows" or "macOS" or "Linux" or "ChromeOS" or "Android" or "iOS" or "iPadOS" ? sample.Os : string.Empty;
        sample.Device = string.Empty;
        sample.Map = string.Empty;
        sample.Gpu = string.Empty;
        sample.Character = string.Empty;
    }
}
