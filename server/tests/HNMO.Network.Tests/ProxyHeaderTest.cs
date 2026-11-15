// <copyright file="ProxyHeaderTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Network.Tests;

using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Tests for <see cref="ProxyHeader"/>: the web bridge's "PROXY" line which tells the game server the player's address.
/// </summary>
[TestFixture]
public class ProxyHeaderTest
{
    /// <summary>
    /// The line is written and read back; wrong lines give nothing.
    /// </summary>
    [Test]
    public void LineIsWrittenAndRead()
    {
        var line = Encoding.ASCII.GetString(ProxyHeader.Line(new IPEndPoint(IPAddress.Parse("::ffff:203.0.113.7"), 51234), new IPEndPoint(IPAddress.Loopback, 55901)));
        Assert.That(line, Is.EqualTo("PROXY TCP4 203.0.113.7 127.0.0.1 51234 55901\r\n"));
        Assert.That(ProxyHeader.Parse(line.TrimEnd()), Is.EqualTo(new IPEndPoint(IPAddress.Parse("203.0.113.7"), 51234)));
        Assert.That(ProxyHeader.Parse("PROXY TCP6 2001:db8::1 ::1 4000 55901"), Is.EqualTo(new IPEndPoint(IPAddress.Parse("2001:db8::1"), 4000)));
        Assert.That(ProxyHeader.Parse("PROXY UNKNOWN"), Is.Null);
        Assert.That(ProxyHeader.Parse("PROXY TCP4 nope 127.0.0.1 1 2"), Is.Null);
    }

    /// <summary>
    /// From this computer the line is consumed and the game's bytes follow untouched; without it nothing is read.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LoopbackLineIsConsumedAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using (var client = new TcpClient())
        {
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("PROXY TCP4 198.51.100.9 127.0.0.1 4242 1\r\n").Concat(new byte[] { 0xC1, 0x04, 0x00, 0x01 }).ToArray()).ConfigureAwait(false);
            using var server = await listener.AcceptSocketAsync().ConfigureAwait(false);
            Assert.That(await ProxyHeader.TryReadAsync(server).ConfigureAwait(false), Is.EqualTo(new IPEndPoint(IPAddress.Parse("198.51.100.9"), 4242)));
            var rest = new byte[4];
            Assert.That(server.Receive(rest), Is.EqualTo(4));
            Assert.That(rest, Is.EqualTo(new byte[] { 0xC1, 0x04, 0x00, 0x01 }));
        }

        using (var client = new TcpClient())
        {
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            await client.GetStream().WriteAsync(new byte[] { 0xC1, 0x04, 0x00, 0x01 }).ConfigureAwait(false);
            using var server = await listener.AcceptSocketAsync().ConfigureAwait(false);
            await Task.Delay(50).ConfigureAwait(false);
            Assert.That(await ProxyHeader.TryReadAsync(server).ConfigureAwait(false), Is.Null);
            Assert.That(server.Available, Is.EqualTo(4), "the game's bytes stay unread");
        }
    }
}
