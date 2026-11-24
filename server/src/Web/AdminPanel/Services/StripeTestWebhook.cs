// <copyright file="StripeTestWebhook.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class StripeTestWebhook
{
    public static bool Verify(byte[] body, string header, string secret, DateTimeOffset now)
    {
        var fields = header.Split(',', StringSplitOptions.TrimEntries);
        var timestamps = fields.Where(field => field.StartsWith("t=", StringComparison.Ordinal)).ToArray();
        if (timestamps.Length != 1 || !long.TryParse(timestamps[0].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || timestamp < now.ToUnixTimeSeconds() - 300 || timestamp > now.ToUnixTimeSeconds() + 300)
        {
            return false;
        }

        var prefix = Encoding.ASCII.GetBytes(timestamps[0][2..] + ".");
        using var signature = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret));
        signature.AppendData(prefix);
        signature.AppendData(body);
        var expected = signature.GetHashAndReset();
        foreach (var field in fields)
        {
            if (field is { Length: 67 } && field.StartsWith("v1=", StringComparison.Ordinal) && field[3..].All(Uri.IsHexDigit)
                && CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(field[3..])))
            {
                return true;
            }
        }

        return false;
    }

    public static string String(JsonElement parent, string property)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    public static bool LooksLikeId(string value, string prefix) => value.Length > prefix.Length && value.Length <= 150
        && value.StartsWith(prefix, StringComparison.Ordinal) && value[prefix.Length..].All(char.IsAsciiLetterOrDigit);
}
