// <copyright file="IntegrityVerifier.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Integrity;

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;

/// <summary>
/// Verifies the HNMO build integrity manifest: a signed list of the key shipped files and
/// their SHA-256 hashes. The manifest is signed at package time with the private Ed25519 key
/// (which never ships); the public key ships (embedded in this assembly). At startup the server
/// re-hashes the files and checks the signature, so a tampered or rebranded folder is detected.
/// </summary>
public static class IntegrityVerifier
{
    /// <summary>
    /// The file name of the integrity manifest written into the folder at package time.
    /// </summary>
    public const string ManifestFileName = "HNMO.integrity.json";

    /// <summary>
    /// The file name of the detached signature (base64) of the manifest.
    /// </summary>
    public const string SignatureFileName = "HNMO.integrity.sig";

    private const string EmbeddedPublicKeyResourceSuffix = "integrity-pub.pem";

    /// <summary>
    /// Loads the Ed25519 public key (PEM) embedded in this assembly.
    /// </summary>
    /// <returns>The PEM bytes, or <c>null</c> if the resource is missing.</returns>
    public static byte[]? LoadEmbeddedPublicKeyPem()
    {
        var assembly = typeof(IntegrityVerifier).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(EmbeddedPublicKeyResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>
    /// Verifies the build against a manifest and detached signature found in <paramref name="manifestDir"/>,
    /// re-hashing files relative to <paramref name="contentRoot"/>.
    /// </summary>
    /// <param name="manifestDir">The directory holding the manifest and signature files.</param>
    /// <param name="contentRoot">The base directory that manifest paths are relative to.</param>
    /// <param name="publicKeyPem">The Ed25519 public key in PEM form.</param>
    /// <returns>The verification result.</returns>
    public static IntegrityResult VerifyFolder(string manifestDir, string contentRoot, byte[] publicKeyPem)
    {
        var manifestPath = Path.Combine(manifestDir, ManifestFileName);
        var signaturePath = Path.Combine(manifestDir, SignatureFileName);
        byte[]? manifestBytes = File.Exists(manifestPath) ? File.ReadAllBytes(manifestPath) : null;
        byte[]? signature = null;
        if (File.Exists(signaturePath))
        {
            try
            {
                signature = Convert.FromBase64String(File.ReadAllText(signaturePath).Trim());
            }
            catch (FormatException)
            {
                signature = null;
            }
        }

        byte[]? ReadContentFile(string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(contentRoot, relativePath));
            return File.Exists(full) ? File.ReadAllBytes(full) : null;
        }

        return Verify(manifestBytes, signature, publicKeyPem, ReadContentFile);
    }

    /// <summary>
    /// Verifies a manifest and signature against the public key, re-hashing each listed file
    /// through <paramref name="readFile"/>. Pure and side-effect free, so it is unit-testable
    /// with in-memory inputs.
    /// </summary>
    /// <param name="manifestBytes">The exact manifest bytes that were signed, or <c>null</c> if absent.</param>
    /// <param name="signature">The raw Ed25519 signature over the manifest bytes, or <c>null</c> if absent.</param>
    /// <param name="publicKeyPem">The Ed25519 public key in PEM form.</param>
    /// <param name="readFile">Returns the bytes of a listed file by its manifest path, or <c>null</c> if missing.</param>
    /// <returns>The verification result.</returns>
    public static IntegrityResult Verify(byte[]? manifestBytes, byte[]? signature, byte[] publicKeyPem, Func<string, byte[]?> readFile)
    {
        if (manifestBytes is null || manifestBytes.Length == 0 || signature is null || signature.Length == 0)
        {
            return new IntegrityResult(IntegrityStatus.Unsigned, "HNMO integrity: no signed manifest found (unsigned/development build).", Array.Empty<string>());
        }

        if (!VerifySignature(manifestBytes, signature, publicKeyPem))
        {
            return new IntegrityResult(IntegrityStatus.SignatureInvalid, "HNMO integrity: the manifest signature is INVALID — the manifest or signature was altered.", Array.Empty<string>());
        }

        IReadOnlyList<IntegrityManifestEntry> entries;
        string buildStamp;
        try
        {
            (buildStamp, entries) = ParseManifest(manifestBytes);
        }
        catch (JsonException)
        {
            return new IntegrityResult(IntegrityStatus.ManifestInvalid, "HNMO integrity: the manifest could not be parsed.", Array.Empty<string>());
        }

        var mismatches = new List<string>();
        foreach (var entry in entries)
        {
            var bytes = readFile(entry.Path);
            if (bytes is null)
            {
                mismatches.Add(entry.Path + " (missing)");
                continue;
            }

            var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add(entry.Path + " (changed)");
            }
        }

        if (mismatches.Count > 0)
        {
            return new IntegrityResult(IntegrityStatus.FileMismatch, $"HNMO integrity: {mismatches.Count} file(s) do not match the signed manifest ({buildStamp}).", mismatches);
        }

        return new IntegrityResult(IntegrityStatus.Ok, $"HNMO integrity: verified {entries.Count} file(s) against the signed manifest ({buildStamp}).", Array.Empty<string>());
    }

    private static (string BuildStamp, IReadOnlyList<IntegrityManifestEntry> Entries) ParseManifest(byte[] manifestBytes)
    {
        using var document = JsonDocument.Parse(manifestBytes);
        var root = document.RootElement;
        var buildStamp = root.TryGetProperty("buildStamp", out var stampElement) ? stampElement.GetString() ?? string.Empty : string.Empty;
        var entries = new List<IntegrityManifestEntry>();
        if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray())
            {
                var path = file.TryGetProperty("path", out var p) ? p.GetString() : null;
                var hash = file.TryGetProperty("sha256", out var h) ? h.GetString() : null;
                if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(hash))
                {
                    entries.Add(new IntegrityManifestEntry(path, hash));
                }
            }
        }

        return (buildStamp, entries);
    }

    private static bool VerifySignature(byte[] message, byte[] signature, byte[] publicKeyPem)
    {
        try
        {
            using var reader = new StringReader(Encoding.UTF8.GetString(publicKeyPem));
            var keyObject = new PemReader(reader).ReadObject();
            if (keyObject is not Ed25519PublicKeyParameters publicKey)
            {
                return false;
            }

            var verifier = new Ed25519Signer();
            verifier.Init(false, publicKey);
            verifier.BlockUpdate(message, 0, message.Length);
            return verifier.VerifySignature(signature);
        }
        catch (Exception exception) when (exception is IOException or FormatException or ArgumentException)
        {
            return false;
        }
    }
}
