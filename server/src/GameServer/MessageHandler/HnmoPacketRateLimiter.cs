// <copyright file="HnmoPacketRateLimiter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.CompilerServices;
using HNMO.GameLogic;

/// <summary>
/// Limits how many of HNMO's own packets (group F5) one player may send: a bucket of <see cref="Burst"/> packets
/// that refills at <see cref="PerSecond"/> a second. Normal play stays far below it (a window sends one request
/// when it opens, an action one packet per click); a client sending them in a loop is cut off, because each one
/// reads or rewrites the shared state file.
/// </summary>
internal sealed class HnmoPacketRateLimiter
{
    /// <summary>How many packets a player may send at once.</summary>
    internal const int Burst = 40;

    /// <summary>How many packets a second refill the bucket.</summary>
    internal const double PerSecond = 20;

    private readonly ConditionalWeakTable<Player, Bucket> _buckets = new();

    private readonly Func<long> _now;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoPacketRateLimiter"/> class.
    /// </summary>
    /// <param name="now">The clock in milliseconds; tests pass their own.</param>
    internal HnmoPacketRateLimiter(Func<long>? now = null)
    {
        this._now = now ?? (static () => Environment.TickCount64);
    }

    /// <summary>Takes one packet from the player's bucket.</summary>
    /// <param name="player">The player who sent the packet.</param>
    /// <returns><c>true</c> if the packet may be handled; <c>false</c> if the player sends too many.</returns>
    internal bool TryTake(Player player)
    {
        var bucket = this._buckets.GetValue(player, _ => new Bucket(this._now()));
        lock (bucket)
        {
            var now = this._now();
            bucket.Tokens = Math.Min(Burst, bucket.Tokens + ((now - bucket.LastMilliseconds) * PerSecond / 1000.0));
            bucket.LastMilliseconds = now;
            if (bucket.Tokens < 1)
            {
                return false;
            }

            bucket.Tokens -= 1;
            return true;
        }
    }

    private sealed class Bucket(long now)
    {
        public double Tokens { get; set; } = Burst;

        public long LastMilliseconds { get; set; } = now;
    }
}
