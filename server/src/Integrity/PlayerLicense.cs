// <copyright file="PlayerLicense.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Integrity;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// The player limit of an HNMO folder. Without a license, <see cref="DefaultMaximumPlayers"/> players may be
/// in the game at once. A license file (<see cref="FileName"/>), signed with the owner's private key (the
/// same Ed25519 key as the build integrity manifest), raises it for one host: optionally until a date and
/// optionally only on one computer (<see cref="MachineId"/>).
/// </summary>
/// <remarks>
/// File format (JSON): <c>{ "payload": "&lt;base64 of the license JSON&gt;", "signature": "&lt;base64 Ed25519 signature over the payload bytes&gt;" }</c>;
/// the license JSON: <c>{ "licensee": "…", "maxPlayers": 50, "expires": "2027-12-31" | null, "machine": "&lt;machine id&gt;" | null, "issued": "…" }</c>.
/// </remarks>
public sealed record PlayerLicense(bool IsValid, int MaximumPlayers, string? Licensee, DateOnly? Expires, string Message)
{
    /// <summary>The players in the game at once without a license.</summary>
    public const int DefaultMaximumPlayers = 10;

    /// <summary>The license file's name, next to the start files of the folder (or in the server folder).</summary>
    public const string FileName = "license.hnmo";

    /// <summary>
    /// Gets the id of this computer for a license bound to it: the first 16 hex digits of the SHA-256 of the
    /// computer's name. The server writes it to its log at startup.
    /// </summary>
    public static string MachineId
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName.ToUpperInvariant())))[..16].ToLowerInvariant();

    /// <summary>The limit without a (valid) license.</summary>
    /// <param name="message">Why.</param>
    /// <returns>The default.</returns>
    public static PlayerLicense Default(string message) => new(false, DefaultMaximumPlayers, null, null, message);

    /// <summary>
    /// Reads and checks a license file against the HNMO build key and, once embedded, the developer hub's licence key.
    /// </summary>
    /// <param name="fileBytes">The file's content; <c>null</c> when there is none.</param>
    /// <param name="machineId">This computer's id.</param>
    /// <param name="today">Today's date.</param>
    /// <returns>The license, or the default limit with the reason.</returns>
    public static PlayerLicense CheckWithEmbeddedKeys(byte[]? fileBytes, string machineId, DateOnly today)
    {
        var buildKey = IntegrityVerifier.LoadEmbeddedPublicKeyPem();
        if (buildKey is null)
        {
            return Default($"No public key is embedded: up to {DefaultMaximumPlayers} players at once.");
        }

        var result = Check(fileBytes, buildKey, machineId, today);
        if (!result.IsValid && IntegrityVerifier.LoadEmbeddedLicenceKeyPem() is { } licenceKey)
        {
            var withLicenceKey = Check(fileBytes, licenceKey, machineId, today);
            if (withLicenceKey.IsValid || !withLicenceKey.Message.Contains("signature", StringComparison.Ordinal))
            {
                return withLicenceKey;
            }
        }

        return result;
    }

    /// <summary>
    /// Reads and checks a license file.
    /// </summary>
    /// <param name="fileBytes">The file's content; <c>null</c> when there is none.</param>
    /// <param name="publicKeyPem">The public key (PEM).</param>
    /// <param name="machineId">This computer's id.</param>
    /// <param name="today">Today's date.</param>
    /// <returns>The license, or the default limit with the reason.</returns>
    public static PlayerLicense Check(byte[]? fileBytes, byte[] publicKeyPem, string machineId, DateOnly today)
    {
        if (fileBytes is null)
        {
            return Default($"No license: up to {DefaultMaximumPlayers} players at once.");
        }

        try
        {
            using var file = JsonDocument.Parse(fileBytes);
            var payload = Convert.FromBase64String(file.RootElement.GetProperty("payload").GetString() ?? string.Empty);
            var signature = Convert.FromBase64String(file.RootElement.GetProperty("signature").GetString() ?? string.Empty);
            if (!IntegrityVerifier.VerifySignature(payload, signature, publicKeyPem))
            {
                return Default($"The license's signature is not valid: up to {DefaultMaximumPlayers} players at once.");
            }

            using var license = JsonDocument.Parse(payload);
            var root = license.RootElement;
            var maximum = root.GetProperty("maxPlayers").GetInt32();
            var licensee = root.TryGetProperty("licensee", out var name) ? name.GetString() : null;
            DateOnly? expires = root.TryGetProperty("expires", out var date) && date.ValueKind == JsonValueKind.String
                ? DateOnly.ParseExact(date.GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                : null;
            var machine = root.TryGetProperty("machine", out var bound) && bound.ValueKind == JsonValueKind.String ? bound.GetString() : null;
            if (maximum < 1)
            {
                return Default("The license allows no players; ignored.");
            }

            if (expires is { } end && today > end)
            {
                return Default($"The license of {licensee} expired on {end:yyyy-MM-dd}: up to {DefaultMaximumPlayers} players at once.");
            }

            if (!string.IsNullOrEmpty(machine) && !string.Equals(machine, machineId, StringComparison.OrdinalIgnoreCase))
            {
                return Default($"The license of {licensee} is for another computer (this one is {machineId}): up to {DefaultMaximumPlayers} players at once.");
            }

            var until = expires is { } last ? $" until {last:yyyy-MM-dd}" : string.Empty;
            return new PlayerLicense(true, maximum, licensee, expires, $"Licensed to {licensee}: up to {maximum} players at once{until}.");
        }
        catch (Exception error) when (error is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return Default($"The license file can't be read: up to {DefaultMaximumPlayers} players at once.");
        }
    }
}
