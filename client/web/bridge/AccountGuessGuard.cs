using System.Collections.Concurrent;

/// <summary>
/// Password guessing on the website, per account (audit 2.8): the per-visitor rate limit stops one address, not many
/// addresses trying one account. After <see cref="MaximumFailures"/> refused passwords for an account within
/// <see cref="Window"/>, the website's account requests for it are refused for <see cref="LockTime"/>.
/// </summary>
internal sealed class AccountGuessGuard(TimeProvider clock)
{
    /// <summary>Refused passwords an account may get within <see cref="Window"/>.</summary>
    public const int MaximumFailures = 10;

    /// <summary>How long failures are counted.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>How long an account stays locked on the website.</summary>
    public static readonly TimeSpan LockTime = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The guard of this web server.</summary>
    public static AccountGuessGuard Shared { get; } = new(TimeProvider.System);

    /// <summary>Whether the website refuses requests for the account now.</summary>
    /// <param name="loginName">The login name.</param>
    /// <returns><c>true</c> while it's locked.</returns>
    public bool IsLocked(string loginName) =>
        this._entries.TryGetValue(loginName.Trim(), out var entry) && entry.LockedUntil > clock.GetUtcNow();

    /// <summary>Counts a refused password for the account.</summary>
    /// <param name="loginName">The login name.</param>
    /// <returns><c>true</c> if the account is locked now.</returns>
    public bool RecordFailure(string loginName)
    {
        var now = clock.GetUtcNow();
        var entry = this._entries.AddOrUpdate(
            loginName.Trim(),
            _ => new Entry(now, 1, DateTimeOffset.MinValue),
            (_, old) =>
            {
                var fresh = now - old.WindowStart > Window ? new Entry(now, 0, old.LockedUntil) : old;
                var failures = fresh.Failures + 1;
                return fresh with { Failures = failures, LockedUntil = failures >= MaximumFailures ? now + LockTime : fresh.LockedUntil };
            });
        return entry.LockedUntil > now;
    }

    /// <summary>Forgets the account's failures after a right password.</summary>
    /// <param name="loginName">The login name.</param>
    public void RecordSuccess(string loginName)
    {
        if (this._entries.TryGetValue(loginName.Trim(), out var entry) && entry.LockedUntil <= clock.GetUtcNow())
        {
            this._entries.TryRemove(loginName.Trim(), out _);
        }
    }

    private sealed record Entry(DateTimeOffset WindowStart, int Failures, DateTimeOffset LockedUntil);
}
