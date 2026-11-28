// <copyright file="VietQr.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Globalization;
using System.Text;

/// <summary>
/// The NAPAS 247 VietQR (EMVCo) payload of a bank transfer, made locally; the same tags, sanitizing and
/// CRC-16/CCITT-FALSE as the web client's <c>client/web/src/vietqr.ts</c>, so both give the same string.
/// </summary>
public static class VietQr
{
    /// <summary>Builds the VietQR string for a transfer.</summary>
    /// <param name="bin">The bank's BIN (6 digits).</param>
    /// <param name="account">The account number.</param>
    /// <param name="accountName">The account holder (shown by banking apps).</param>
    /// <param name="amount">The amount in whole đồng; 0 or less: the payer types it.</param>
    /// <param name="message">The transfer note.</param>
    /// <returns>The payload to put in a QR code.</returns>
    public static string BuildPayload(string bin, string account, string? accountName, decimal amount, string? message)
    {
        ArgumentNullException.ThrowIfNull(bin);
        ArgumentNullException.ThrowIfNull(account);
        var beneficiary = Tlv("00", bin.Trim()) + Tlv("01", account.Trim());
        var merchantInfo = Tlv("00", "A000000727") + Tlv("01", beneficiary) + Tlv("02", "QRIBFTTA");
        var whole = (long)Math.Max(0, Math.Floor(amount));
        var payload = new StringBuilder()
            .Append(Tlv("00", "01"))
            .Append(Tlv("01", whole > 0 ? "12" : "11"))
            .Append(Tlv("38", merchantInfo))
            .Append(Tlv("53", "704"));
        if (whole > 0)
        {
            payload.Append(Tlv("54", whole.ToString(CultureInfo.InvariantCulture)));
        }

        payload.Append(Tlv("58", "VN"));
        var name = Cut(SanitizeAscii(accountName), 25);
        if (name.Length > 0)
        {
            payload.Append(Tlv("59", name));
        }

        var note = Cut(SanitizeAscii(message), 50);
        if (note.Length > 0)
        {
            payload.Append(Tlv("62", Tlv("08", note)));
        }

        var unsigned = payload.Append("6304").ToString();
        return unsigned + Crc16CcittFalse(unsigned);
    }

    /// <summary>The EMVCo checksum: CRC-16/CCITT-FALSE (polynomial 0x1021, start 0xFFFF), 4 upper-case hex digits.</summary>
    /// <param name="text">The text (ASCII).</param>
    /// <returns>The checksum.</returns>
    public static string Crc16CcittFalse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var crc = 0xFFFF;
        foreach (var c in text)
        {
            crc ^= c << 8;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>Vietnamese text as plain ASCII: accents dropped, đ to d, other characters to spaces, spaces collapsed.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The ASCII text.</returns>
    public static string SanitizeAscii(string? text)
    {
        var decomposed = (text ?? string.Empty).Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var space = false;
        foreach (var c in decomposed)
        {
            if (c is >= '̀' and <= 'ͯ')
            {
                continue;
            }

            var ascii = c switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                >= ' ' and <= '~' => c,
                _ => ' ',
            };
            if (char.IsWhiteSpace(ascii))
            {
                space = true;
                continue;
            }

            if (space && result.Length > 0)
            {
                result.Append(' ');
            }

            space = false;
            result.Append(ascii);
        }

        return result.ToString();
    }

    private static string Tlv(string tag, string value) => tag + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;

    private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
}
