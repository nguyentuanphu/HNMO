// <copyright file="WebPushCrypto.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

/// <summary>
/// Pure .NET implementation of RFC 8292 (VAPID P-256 ES256 JWT) and RFC 8291 (aes128gcm content encryption)
/// for sending standard browser Web Push notifications with zero external dependencies.
/// </summary>
public static class WebPushCrypto
{
    private const int RecordSize = 4096;

    /// <summary>Generates a fresh NIST P-256 ECDSA keypair encoded as Base64URL strings.</summary>
    /// <returns>Tuple of uncompressed 65-byte <c>PublicKey</c> and 32-byte <c>PrivateKey</c>.</returns>
    public static (string PublicKeyBase64Url, string PrivateKeyBase64Url) GenerateVapidKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdsa.ExportParameters(includePrivateParameters: true);
        var uncompressedPub = EncodeUncompressedPoint(p.Q);
        return (ToBase64Url(uncompressedPub), ToBase64Url(p.D!));
    }

    /// <summary>Creates a VAPID <c>Authorization</c> header value (<c>vapid t=..., k=...</c>).</summary>
    /// <param name="endpoint">Target push service endpoint URI.</param>
    /// <param name="subject">Host contact URI (<c>mailto:...</c> or <c>https://...</c>).</param>
    /// <param name="publicKeyBase64Url">Uncompressed P-256 public key (Base64URL).</param>
    /// <param name="privateKeyBase64Url">32-byte P-256 private scalar (Base64URL).</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Formatted VAPID authorization header value.</returns>
    public static string CreateVapidAuthorizationHeader(
        Uri endpoint,
        string subject,
        string publicKeyBase64Url,
        string privateKeyBase64Url,
        DateTimeOffset utcNow)
    {
        var audience = $"{endpoint.Scheme}://{endpoint.Authority}";
        var headerJson = JsonSerializer.Serialize(new { typ = "JWT", alg = "ES256" });
        var claimsJson = JsonSerializer.Serialize(new
        {
            aud = audience,
            exp = utcNow.AddHours(12).ToUnixTimeSeconds(),
            sub = string.IsNullOrWhiteSpace(subject) ? "mailto:admin@hnmo.local" : subject.Trim(),
        });

        var unsignedToken = $"{ToBase64Url(Encoding.UTF8.GetBytes(headerJson))}.{ToBase64Url(Encoding.UTF8.GetBytes(claimsJson))}";

        var pubBytes = FromBase64Url(publicKeyBase64Url);
        var privBytes = FromBase64Url(privateKeyBase64Url);
        var ecParams = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = DecodeUncompressedPoint(pubBytes),
            D = privBytes,
        };

        using var ecdsa = ECDsa.Create(ecParams);
        var sig = ecdsa.SignData(
            Encoding.ASCII.GetBytes(unsignedToken),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var jwt = $"{unsignedToken}.{ToBase64Url(sig)}";
        return $"vapid t={jwt}, k={publicKeyBase64Url}";
    }

    /// <summary>Encrypts a UTF-8 push payload per RFC 8291 (<c>aes128gcm</c>).</summary>
    /// <param name="payloadUtf8">JSON payload bytes.</param>
    /// <param name="p256dhBase64Url">Subscriber ECDH public key (Base64URL).</param>
    /// <param name="authBase64Url">Subscriber 16-byte authentication secret (Base64URL).</param>
    /// <returns>Complete binary RFC 8291 body ready for HTTP POST.</returns>
    public static byte[] EncryptPayloadAes128Gcm(
        ReadOnlySpan<byte> payloadUtf8,
        string p256dhBase64Url,
        string authBase64Url)
    {
        var uaPublic = FromBase64Url(p256dhBase64Url);
        var authSecret = FromBase64Url(authBase64Url);
        var salt = RandomNumberGenerator.GetBytes(16);

        using var asEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var asParams = asEcdh.ExportParameters(includePrivateParameters: false);
        var asPublic = EncodeUncompressedPoint(asParams.Q);

        using var uaEcdh = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = DecodeUncompressedPoint(uaPublic),
        });
        var sharedSecret = asEcdh.DeriveRawSecretAgreement(uaEcdh.PublicKey);

        // RFC 8291 Section 3.3: key_info = "WebPush: info\0" || ua_public || as_public
        var keyInfoPrefix = "WebPush: info\0"u8;
        var keyInfo = new byte[keyInfoPrefix.Length + uaPublic.Length + asPublic.Length];
        keyInfoPrefix.CopyTo(keyInfo);
        uaPublic.CopyTo(keyInfo, keyInfoPrefix.Length);
        asPublic.CopyTo(keyInfo, keyInfoPrefix.Length + uaPublic.Length);

        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);

        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());

        // Plaintext || 0x02 final record delimiter
        var plaintext = new byte[payloadUtf8.Length + 1];
        payloadUtf8.CopyTo(plaintext);
        plaintext[^1] = 0x02;

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(cek, 16))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // Header: salt(16) || rs(4 BE) || idlen(1) || asPublic(65)
        var headerLen = 16 + 4 + 1 + asPublic.Length;
        var result = new byte[headerLen + ciphertext.Length + tag.Length];
        salt.CopyTo(result, 0);
        result[16] = (byte)((RecordSize >> 24) & 0xFF);
        result[17] = (byte)((RecordSize >> 16) & 0xFF);
        result[18] = (byte)((RecordSize >> 8) & 0xFF);
        result[19] = (byte)(RecordSize & 0xFF);
        result[20] = (byte)asPublic.Length;
        asPublic.CopyTo(result, 21);
        ciphertext.CopyTo(result, headerLen);
        tag.CopyTo(result, headerLen + ciphertext.Length);
        return result;
    }

    /// <summary>Sends a Web Push message using RFC 8291 encryption and VAPID authentication.</summary>
    /// <param name="http">HttpClient instance.</param>
    /// <param name="subscription">Target subscriber.</param>
    /// <param name="payloadJson">Notification JSON payload.</param>
    /// <param name="subject">VAPID subject.</param>
    /// <param name="publicKey">Host VAPID public key.</param>
    /// <param name="privateKey">Host VAPID private key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Outcome tuple (<c>Ok</c>, <c>Gone</c> when endpoint expired with 404/410).</returns>
    public static async Task<(bool Ok, bool Gone)> SendAsync(
        HttpClient http,
        EventAlertSubscription subscription,
        string payloadJson,
        string subject,
        string publicKey,
        string privateKey,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return (false, true);
        }

        var body = EncryptPayloadAes128Gcm(Encoding.UTF8.GetBytes(payloadJson), subscription.P256dh, subscription.Auth);
        var authHeader = CreateVapidAuthorizationHeader(endpoint, subject, publicKey, privateKey, DateTimeOffset.UtcNow);

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.TryAddWithoutValidation("Authorization", authHeader);
        req.Headers.TryAddWithoutValidation("TTL", "300");
        req.Content = new ByteArrayContent(body);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        req.Content.Headers.ContentEncoding.Add("aes128gcm");

        using var resp = await http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (resp.IsSuccessStatusCode)
        {
            return (true, false);
        }

        var gone = resp.StatusCode == HttpStatusCode.Gone || resp.StatusCode == HttpStatusCode.NotFound;
        return (false, gone);
    }

    /// <summary>Encodes bytes as unpadded Base64URL per RFC 7515.</summary>
    /// <param name="bytes">Input bytes.</param>
    /// <returns>Base64URL string.</returns>
    public static string ToBase64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes an unpadded or padded Base64URL string.</summary>
    /// <param name="base64Url">Encoded text.</param>
    /// <returns>Decoded bytes.</returns>
    public static byte[] FromBase64Url(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }

    private static byte[] EncodeUncompressedPoint(ECPoint q)
    {
        var result = new byte[65];
        result[0] = 0x04;
        var x = q.X!;
        var y = q.Y!;
        Array.Copy(x, 0, result, 1 + (32 - x.Length), x.Length);
        Array.Copy(y, 0, result, 33 + (32 - y.Length), y.Length);
        return result;
    }

    private static ECPoint DecodeUncompressedPoint(byte[] bytes)
    {
        if (bytes.Length != 65 || bytes[0] != 0x04)
        {
            throw new CryptographicException("Expected 65-byte uncompressed P-256 EC point.");
        }

        return new ECPoint
        {
            X = bytes[1..33],
            Y = bytes[33..65],
        };
    }
}
