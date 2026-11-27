// <copyright file="IdleConnectionTimeoutTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Network;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.Persistence;

/// <summary>
/// Tests for shorter idle connection detection and socket timeouts (no silent multi-minute hangs).
/// </summary>
[TestFixture]
public class IdleConnectionTimeoutTests
{
    /// <summary>
    /// Verifies that the default idle and lost-connection timeouts are shortened below one minute.
    /// </summary>
    [Test]
    public void DefaultsAreShortenedBelowOneMinute()
    {
        var hostSettings = new HostSettings();
        var lostConnectionConfig = new LostConnectionDetectionConfiguration();

        Assert.That(hostSettings.ConnectionIdleTimeoutSeconds, Is.EqualTo(30));
        Assert.That(lostConnectionConfig.Timeout, Is.EqualTo(TimeSpan.FromSeconds(45)));
    }

    /// <summary>
    /// Verifies that <see cref="Listener"/> fires <see cref="IConnection"/> idle probe once and closes a silent socket promptly.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SilentListenerClientIsProbedAndDisconnectedPromptlyAsync()
    {
        var timeout = TimeSpan.FromMilliseconds(350);
        var acceptedTcs = new TaskCompletionSource<IConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeCount = 0;

        var (listener, port) = StartListenerOnFreePort(timeout, l => l.ClientAccepted += args =>
        {
            var connection = args.AcceptedConnection;
            if (connection is Connection tcpConnection)
            {
                tcpConnection.IdleProbeRequested += () =>
                {
                    Interlocked.Increment(ref probeCount);
                    return tcpConnection.SendPingRequestAsync();
                };
            }

            connection.Disconnected += () => { disconnectedTcs.TrySetResult(); return ValueTask.CompletedTask; };
            acceptedTcs.TrySetResult(connection);
            return ValueTask.CompletedTask;
        });
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            using var connection = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            _ = connection.BeginReceiveAsync();

            var sw = Stopwatch.StartNew();
            var stream = client.GetStream();
            var buffer = new byte[16];
            var receivedBytes = new List<byte>();

            using var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!waitCts.IsCancellationRequested)
            {
                try
                {
                    var read = await stream.ReadAsync(buffer, waitCts.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    for (var i = 0; i < read; i++)
                    {
                        receivedBytes.Add(buffer[i]);
                    }
                }
                catch (IOException)
                {
                    break;
                }
            }

            await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            sw.Stop();

            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(4)));
            Assert.That(Volatile.Read(ref probeCount), Is.EqualTo(1));
            Assert.That(receivedBytes.ToArray(), Is.EqualTo(new[] { PingRequest.HeaderType, (byte)PingRequest.Length, PingRequest.Code }));
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Verifies that sending <see cref="PingResponse"/> packets resets the idle watchdog and keeps the socket open.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ActiveClientSendingPingResponseStaysConnectedUntilGoingSilentAsync()
    {
        var timeout = TimeSpan.FromMilliseconds(350);
        var acceptedTcs = new TaskCompletionSource<IConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var (listener, port) = StartListenerOnFreePort(timeout, l => l.ClientAccepted += args =>
        {
            var connection = args.AcceptedConnection;
            connection.Disconnected += () => { disconnectedTcs.TrySetResult(); return ValueTask.CompletedTask; };
            acceptedTcs.TrySetResult(connection);
            return ValueTask.CompletedTask;
        });
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            using var connection = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            _ = connection.BeginReceiveAsync();

            var stream = client.GetStream();
            var pingResponse = new[] { PingResponse.HeaderType, (byte)PingResponse.Length, PingResponse.Code };

            // Send 6 keep-alives 120ms apart (~720ms total), well past the 350ms idle timeout.
            for (var i = 0; i < 6; i++)
            {
                await stream.WriteAsync(pingResponse).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                await Task.Delay(120).ConfigureAwait(false);
                Assert.That(disconnectedTcs.Task.IsCompleted, Is.False, $"Connection disconnected early on iteration {i}.");
            }

            // Now stop sending and expect prompt disconnect.
            await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(4)).ConfigureAwait(false);
            Assert.That(connection.Connected, Is.False);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Verifies that <see cref="DefaultTcpGameServerListener"/> probes and closes a silent TCP client cleanly after idle timeout.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task GameServerListenerDisconnectsSilentTcpClientAfterIdleTimeoutAsync()
    {
        var persistence = LoginTestSetup.CreatePersistence(new Mock<IPlayerContext> { DefaultValue = DefaultValue.Mock }.Object);
        var loginServer = new Mock<ILoginServer>();
        await using var gameContext = LoginTestSetup.CreateGameServerContext(persistence, loginServer.Object);

        var resolver = new Mock<IIpAddressResolver>();
        resolver.Setup(r => r.ResolveIPv4Async()).ReturnsAsync(IPAddress.Loopback);
        var endpoint = new GameServerEndpoint
        {
            NetworkPort = 0,
            Client = new GameClientDefinition { Season = 6, Episode = 3, Language = ClientLanguage.English },
        };
        var serverInfo = new ServerInfo(0, "idle-test", 0, gameContext.ServerConfiguration.MaximumPlayers);

        const int maxAttempts = 5;
        DefaultTcpGameServerListener? listener = null;
        var port = 0;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            port = GetFreePort();
            endpoint.NetworkPort = port;
            listener = new DefaultTcpGameServerListener(
                endpoint,
                serverInfo,
                gameContext,
                new Mock<IGameServerStateObserver>().Object,
                resolver.Object,
                NullLoggerFactory.Instance,
                listenerPort: null,
                idleTimeout: TimeSpan.FromMilliseconds(400));
            try
            {
                await listener.StartAsync().ConfigureAwait(false);
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                listener.Stop();
                listener = null;
            }
        }

        Assert.That(listener, Is.Not.Null);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            var stream = client.GetStream();
            var buffer = new byte[16];
            var receivedBytes = new List<byte>();

            var sw = Stopwatch.StartNew();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!timeoutCts.IsCancellationRequested)
            {
                try
                {
                    var read = await stream.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    for (var i = 0; i < read; i++)
                    {
                        receivedBytes.Add(buffer[i]);
                    }
                }
                catch (IOException)
                {
                    break;
                }
            }

            sw.Stop();
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(4)));
            Assert.That(receivedBytes.ToArray(), Is.EqualTo(new[] { PingRequest.HeaderType, (byte)PingRequest.Length, PingRequest.Code }));
        }
        finally
        {
            listener!.Stop();
        }
    }

    /// <summary>
    /// Verifies that answering idle <see cref="PingRequest"/> probes with <see cref="PingResponse"/> keeps the connection alive past the idle timeout.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ClientAnsweringPingRequestStaysConnectedPastIdleTimeoutAsync()
    {
        var timeout = TimeSpan.FromMilliseconds(350);
        var acceptedTcs = new TaskCompletionSource<IConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probesSent = 0;

        var (listener, port) = StartListenerOnFreePort(timeout, l => l.ClientAccepted += args =>
        {
            var connection = args.AcceptedConnection;
            if (connection is Connection tcpConnection)
            {
                tcpConnection.IdleProbeRequested += () =>
                {
                    Interlocked.Increment(ref probesSent);
                    return tcpConnection.SendPingRequestAsync();
                };
            }

            connection.Disconnected += () => { disconnectedTcs.TrySetResult(); return ValueTask.CompletedTask; };
            acceptedTcs.TrySetResult(connection);
            return ValueTask.CompletedTask;
        });
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            using var connection = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            _ = connection.BeginReceiveAsync();

            var stream = client.GetStream();
            var expectedRequest = new[] { PingRequest.HeaderType, (byte)PingRequest.Length, PingRequest.Code };
            var pingResponse = new[] { PingResponse.HeaderType, (byte)PingResponse.Length, PingResponse.Code };
            var buffer = new byte[3];

            for (var cycle = 0; cycle < 2; cycle++)
            {
                var readOffset = 0;
                using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                while (readOffset < expectedRequest.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(readOffset, expectedRequest.Length - readOffset), readCts.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        Assert.Fail($"Stream closed unexpectedly while waiting for PingRequest cycle {cycle}.");
                    }

                    readOffset += read;
                }

                Assert.That(buffer, Is.EqualTo(expectedRequest));
                await stream.WriteAsync(pingResponse).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            await Task.Delay(120).ConfigureAwait(false);
            Assert.That(Volatile.Read(ref probesSent), Is.GreaterThanOrEqualTo(2));
            Assert.That(disconnectedTcs.Task.IsCompleted, Is.False);
            Assert.That(connection.Connected, Is.True);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static (Listener Listener, int Port) StartListenerOnFreePort(
        TimeSpan timeout,
        Action<Listener> configureListener)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var port = GetFreePort();
            var listener = new Listener(port, null, null, NullLoggerFactory.Instance, timeout);
            configureListener(listener);
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                listener.Stop();
            }
        }

        throw new InvalidOperationException($"Failed to bind listener after {maxAttempts} attempts.");
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
