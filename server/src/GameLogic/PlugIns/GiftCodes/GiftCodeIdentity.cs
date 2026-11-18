// <copyright file="GiftCodeIdentity.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Net;
using System.Security.Cryptography;
using System.Text;

/// <summary>Canonicalizes transient redemption identities and creates salted SHA-256 claims.</summary>
public static class GiftCodeIdentity
{
    /// <summary>Hashes an address, treating IPv4 and its IPv6-mapped representation as the same network.</summary>
    /// <param name="salt">The store's persistent random secret.</param>
    /// <param name="address">The connection address.</param>
    /// <returns>A network claim, or null when the address is unknown.</returns>
    public static string? NetworkClaim(string salt, string? address)
    {
        if (!IPAddress.TryParse(address, out var parsed))
        {
            return null;
        }

        return Hash(salt, "n", (parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed).ToString());
    }

    /// <summary>Hashes a browser UUID; missing or malformed ids are unknown, including legacy desktop clients.</summary>
    /// <param name="salt">The store's persistent random secret.</param>
    /// <param name="deviceId">The optional device UUID.</param>
    /// <returns>A device claim, or null when the device is unknown.</returns>
    public static string? DeviceClaim(string salt, string? deviceId) =>
        Guid.TryParse(deviceId, out var id) && id != Guid.Empty ? Hash(salt, "d", id.ToString("D")) : null;

    private static string Hash(string salt, string kind, string value) =>
        kind + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salt + "\0" + kind + "\0" + value)));
}
