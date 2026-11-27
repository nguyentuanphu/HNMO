// <copyright file="LoginAttemptGuard.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

using System.Net;
using System.Threading;

/// <summary>
/// Slows down password guessing: after <see cref="MaximumFailuresPerAccount"/> wrong passwords for an
/// account (or <see cref="MaximumFailuresPerAddress"/> from one address) within <see cref="Window"/>, logins
/// of that account (or from that address) are refused for <see cref="LockTime"/>. A successful login clears
/// the account's count. Addresses on this computer aren't counted per address: web players all come
/// through the web server on this computer, which limits per visitor address itself.
/// </summary>
public static class LoginAttemptGuard
{
    /// <summary>Wrong passwords per account before it's locked.</summary>
    public const int MaximumFailuresPerAccount = 5;

    /// <summary>Wrong passwords per address before it's locked.</summary>
    public const int MaximumFailuresPerAddress = 20;

    /// <summary>The time in which failures count.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>How long a lock lasts.</summary>
    public static readonly TimeSpan LockTime = TimeSpan.FromMinutes(10);

    private static readonly Dictionary<string, List<DateTime>> Failures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> Locks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Sync = new();

    /// <summary>Gets or sets the clock; for tests.</summary>
    internal static Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

    /// <summary>
    /// Determines whether logins of an account or from an address are locked right now.
    /// </summary>
    /// <param name="loginName">The account.</param>
    /// <param name="address">The address, if known.</param>
    /// <returns><c>true</c>, if the login is refused.</returns>
    public static bool IsLocked(string loginName, string? address)
    {
        var now = Now();
        lock (Sync)
        {
            return IsLocked(AccountKey(loginName), now) || (AddressKey(address) is { } key && IsLocked(key, now));
        }
    }

    /// <summary>
    /// Counts a wrong password.
    /// </summary>
    /// <param name="loginName">The account.</param>
    /// <param name="address">The address, if known.</param>
    /// <returns><c>true</c>, if this failure locked the account or the address.</returns>
    public static bool RecordFailure(string loginName, string? address)
    {
        var now = Now();
        lock (Sync)
        {
            var locked = Count(AccountKey(loginName), MaximumFailuresPerAccount, now);
            if (AddressKey(address) is { } key)
            {
                locked |= Count(key, MaximumFailuresPerAddress, now);
            }

            Prune(now);
            return locked;
        }
    }

    /// <summary>
    /// Clears an account's failures after a successful login.
    /// </summary>
    /// <param name="loginName">The account.</param>
    public static void RecordSuccess(string loginName)
    {
        lock (Sync)
        {
            Failures.Remove(AccountKey(loginName));
        }
    }

    /// <summary>Forgets everything; for tests.</summary>
    internal static void Reset()
    {
        lock (Sync)
        {
            Failures.Clear();
            Locks.Clear();
        }
    }

    private static string AccountKey(string loginName) => "account:" + loginName.Trim();

    private static string? AddressKey(string? address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var host = address.Contains(':') && IPAddress.TryParse(address, out _) ? address : address.Split(':')[0];
        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip) ? null : "address:" + host;
    }

    private static bool IsLocked(string key, DateTime now)
        => Locks.TryGetValue(key, out var until) && until > now;

    private static bool Count(string key, int maximum, DateTime now)
    {
        if (!Failures.TryGetValue(key, out var times))
        {
            Failures[key] = times = [];
        }

        times.RemoveAll(time => now - time > Window);
        times.Add(now);
        if (times.Count < maximum)
        {
            return false;
        }

        Locks[key] = now + LockTime;
        times.Clear();
        return true;
    }

    // Keeps the tables small: old failures and expired locks go.
    private static void Prune(DateTime now)
    {
        if (Failures.Count + Locks.Count < 10_000)
        {
            return;
        }

        foreach (var key in Failures.Where(entry => entry.Value.All(time => now - time > Window)).Select(entry => entry.Key).ToList())
        {
            Failures.Remove(key);
        }

        foreach (var key in Locks.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToList())
        {
            Locks.Remove(key);
        }
    }
}
