// <copyright file="HnmoPacketRateLimiterTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameServer.MessageHandler;

/// <summary>The per-player limit on HNMO's own packets.</summary>
[TestFixture]
public class HnmoPacketRateLimiterTest
{
    /// <summary>A burst is allowed, a loop is cut off, and the bucket refills with time.</summary>
    [Test]
    public async Task ABurstPassesALoopIsCutOffAndTimeRefillsAsync()
    {
        long now = 1_000;
        var limiter = new HnmoPacketRateLimiter(() => now);
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        var allowed = Enumerable.Range(0, 100).Count(_ => limiter.TryTake(player));
        Assert.That(allowed, Is.EqualTo(HnmoPacketRateLimiter.Burst), "a client in a loop gets the burst, then nothing");

        now += 1_000;
        allowed = Enumerable.Range(0, 100).Count(_ => limiter.TryTake(player));
        Assert.That(allowed, Is.EqualTo((int)HnmoPacketRateLimiter.PerSecond), "one second refills one second's worth");

        now += 60_000;
        allowed = Enumerable.Range(0, 100).Count(_ => limiter.TryTake(player));
        Assert.That(allowed, Is.EqualTo(HnmoPacketRateLimiter.Burst), "a long pause refills only up to the burst");
    }

    /// <summary>Normal play (a few packets a second) is never refused, and players don't share a bucket.</summary>
    [Test]
    public async Task NormalPlayIsNeverRefusedAndPlayersAreSeparateAsync()
    {
        long now = 0;
        var limiter = new HnmoPacketRateLimiter(() => now);
        var first = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var second = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        for (var i = 0; i < 600; i++)
        {
            now += 200;
            Assert.That(limiter.TryTake(first), Is.True, $"packet {i} at five a second");
        }

        while (limiter.TryTake(first))
        {
        }

        Assert.That(limiter.TryTake(second), Is.True, "another player's bucket is full");
    }
}
