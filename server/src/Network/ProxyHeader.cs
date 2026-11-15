// <copyright file="ProxyHeader.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Network;

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Reads the "PROXY" line (version 1 of the proxy protocol) which HNMO's web bridge sends first when it relays a
/// browser's game connection, so that the server knows the player's real address (else every web player would come
/// from the bridge's address). It's only trusted from the same computer (a loopback address).
/// </summary>
public static class ProxyHeader
{
    /// <summary>The longest line of version 1 of the proxy protocol.</summary>
    public const int MaximumLength = 107;

    /// <summary>
    /// Gets or sets how long a loopback connection is waited for its first bytes.
    /// </summary>
    public static TimeSpan Wait { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Reads the line of a new connection when it comes from this computer and starts with it.
    /// </summary>
    /// <param name="socket">The accepted socket.</param>
    /// <returns>The address of the player; <c>null</c> without a line.</returns>
    public static async Task<IPEndPoint?> TryReadAsync(Socket socket)
    {
        if (socket.RemoteEndPoint is not IPEndPoint remote || !IPAddress.IsLoopback(remote.Address.IsIPv4MappedToIPv6 ? remote.Address.MapToIPv4() : remote.Address))
        {
            return null;
        }

        try
        {
            var wait = (int)Math.Clamp(Wait.TotalMicroseconds, 0, int.MaxValue);
            if (!await Task.Run(() => socket.Poll(wait, SelectMode.SelectRead)).ConfigureAwait(false) || socket.Available == 0)
            {
                return null;
            }

            var buffer = new byte[MaximumLength];
            var peeked = socket.Receive(buffer, SocketFlags.Peek);
            if (peeked < 6 || Encoding.ASCII.GetString(buffer, 0, 6) != "PROXY ")
            {
                return null;
            }

            var text = Encoding.ASCII.GetString(buffer, 0, peeked);
            var end = text.IndexOf("\r\n", StringComparison.Ordinal);
            if (end < 0)
            {
                return null;
            }

            // Consumes the line; the game's packets follow it.
            var line = new byte[end + 2];
            var read = 0;
            while (read < line.Length)
            {
                var count = socket.Receive(line, read, line.Length - read, SocketFlags.None);
                if (count <= 0)
                {
                    return null;
                }

                read += count;
            }

            return Parse(text[..end]);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the source of a line, e.g. "PROXY TCP4 203.0.113.7 127.0.0.1 51234 44405".
    /// </summary>
    /// <param name="line">The line without its end.</param>
    /// <returns>The source; <c>null</c> for "UNKNOWN" or a wrong line.</returns>
    public static IPEndPoint? Parse(string line)
    {
        var parts = line.Split(' ');
        if (parts.Length != 6 || parts[0] != "PROXY" || parts[1] is not ("TCP4" or "TCP6")
            || !IPAddress.TryParse(parts[2], out var address)
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port > 65535)
        {
            return null;
        }

        return new IPEndPoint(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address, port);
    }

    /// <summary>
    /// Writes the line for a relayed connection.
    /// </summary>
    /// <param name="source">The player's address.</param>
    /// <param name="destination">The server's address.</param>
    /// <returns>The line with its end.</returns>
    public static byte[] Line(IPEndPoint source, IPEndPoint destination)
    {
        var from = source.Address.IsIPv4MappedToIPv6 ? source.Address.MapToIPv4() : source.Address;
        var to = destination.Address.IsIPv4MappedToIPv6 ? destination.Address.MapToIPv4() : destination.Address;
        if (from.AddressFamily != to.AddressFamily)
        {
            to = from.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Loopback : IPAddress.IPv6Loopback;
        }

        var family = from.AddressFamily == AddressFamily.InterNetworkV6 ? "TCP6" : "TCP4";
        return Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"PROXY {family} {from} {to} {source.Port} {destination.Port}\r\n"));
    }
}
