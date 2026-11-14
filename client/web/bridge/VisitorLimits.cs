// Limits per visitor address, against floods and simple denial of service: game connections at once,
// new game connections per minute, and packets per second on each connection (a token bucket). The
// host's own computer (loopback) isn't limited by address. Settings: "Limits" in appsettings.json.
using System.Collections.Concurrent;
using System.Net;

internal sealed class VisitorLimitOptions
{
    public int ConnectionsPerAddress { get; set; } = 6;

    public int NewConnectionsPerMinute { get; set; } = 30;

    public double PacketsPerSecond { get; set; } = 60;

    public double PacketBurst { get; set; } = 200;
}

internal sealed class VisitorLimits(VisitorLimitOptions options)
{
    private readonly ConcurrentDictionary<IPAddress, Visitor> _visitors = new();

    public VisitorLimitOptions Options => options;

    /// <summary>Takes a connection place for an address; null when it has too many (dispose to free it).</summary>
    public IDisposable? TryOpen(IPAddress? address)
    {
        if (address is null || IPAddress.IsLoopback(address))
        {
            return Nothing.Instance;
        }

        var visitor = this._visitors.GetOrAdd(address, _ => new Visitor());
        lock (visitor)
        {
            var now = DateTime.UtcNow;
            visitor.Opened.RemoveAll(time => now - time > TimeSpan.FromMinutes(1));
            if (visitor.Open >= options.ConnectionsPerAddress || visitor.Opened.Count >= options.NewConnectionsPerMinute)
            {
                return null;
            }

            visitor.Open++;
            visitor.Opened.Add(now);
        }

        return new Place(this, address, visitor);
    }

    /// <summary>A packet budget for one connection.</summary>
    public TokenBucket NewBucket() => new(options.PacketsPerSecond, options.PacketBurst);

    private void Close(IPAddress address, Visitor visitor)
    {
        lock (visitor)
        {
            visitor.Open--;
            if (visitor.Open <= 0 && visitor.Opened.Count == 0)
            {
                this._visitors.TryRemove(new KeyValuePair<IPAddress, Visitor>(address, visitor));
            }
        }
    }

    private sealed class Visitor
    {
        public int Open;
        public readonly List<DateTime> Opened = [];
    }

    private sealed class Place(VisitorLimits limits, IPAddress address, Visitor visitor) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this._disposed, 1) == 0)
            {
                limits.Close(address, visitor);
            }
        }
    }

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>Allows <c>rate</c> events per second on average, with bursts up to <c>burst</c>.</summary>
internal sealed class TokenBucket(double rate, double burst)
{
    private double _tokens = burst;
    private long _last = Environment.TickCount64;

    public bool TryTake()
    {
        var now = Environment.TickCount64;
        this._tokens = Math.Min(burst, this._tokens + ((now - this._last) / 1000.0 * rate));
        this._last = now;
        if (this._tokens < 1)
        {
            return false;
        }

        this._tokens--;
        return true;
    }
}
