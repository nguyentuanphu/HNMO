// <copyright file="LicensedPlayerLimit.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

using System.Threading;
using HNMO.Interfaces;
using HNMO.Persistence;

/// <summary>
/// How many players may be in the game at once in the whole HNMO folder, as the folder's license allows (set at startup;
/// 10 without a license). One limit for every program together (owner, 2026-11-21): Season 6.3, Season 9 and the Arena
/// share it through the shared database, where each program publishes the accounts holding a place
/// (<see cref="IArenaShared.PublishLicensed"/>). Counted per account: a login of an account that is already in, here or
/// in another program, takes no extra place. Bots and template accounts don't count. Part of the login itself, so it
/// can't be switched off like a plug-in.
/// </summary>
public static class LicensedPlayerLimit
{
    /// <summary>The players at once without a license.</summary>
    public const int DefaultMaximum = 10;

    private static readonly Dictionary<string, Player?> Admitted = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Sync = new();
    private static int _maximum = DefaultMaximum;
    private static IReadOnlyCollection<string> _elsewhere = [];
    private static DateTime _elsewhereReadUtc = DateTime.MinValue;
    private static int _publishing;
    private static int _publishAgain;
    private static Timer? _heartbeat;

    /// <summary>
    /// Gets or sets the players allowed at once (at least 1).
    /// </summary>
    public static int Maximum
    {
        get => Volatile.Read(ref _maximum);
        set => Volatile.Write(ref _maximum, Math.Max(1, value));
    }

    /// <summary>
    /// Gets or sets who the folder's license is for (<c>null</c> without a valid license); bug reports sent to the HNMO
    /// developer name it.
    /// </summary>
    public static string? Licensee { get; set; }

    /// <summary>
    /// Gets the number of accounts in the game now.
    /// </summary>
    public static int Count
    {
        get
        {
            lock (Sync)
            {
                return Admitted.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of accounts in the game now in every program of the folder (this one and those the others
    /// published, each account once).
    /// </summary>
    public static int TotalCount
    {
        get
        {
            var elsewhere = Elsewhere();
            lock (Sync)
            {
                return Admitted.Keys.Union(elsewhere, StringComparer.OrdinalIgnoreCase).Count();
            }
        }
    }

    /// <summary>
    /// Starts sharing the places with the other programs of the folder: publishes this program's accounts now and every
    /// 30 seconds (a program that stopped is forgotten after two minutes). Without a shared database, nothing happens.
    /// </summary>
    public static void StartSharing()
    {
        if (ArenaShared.Current is null)
        {
            return;
        }

        _heartbeat ??= new Timer(
            _ =>
            {
                Publish();
                DeveloperHub.NotePlayers(TotalCount);
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Admits a login, unless the limit is reached. The place is freed when the player disconnects.
    /// </summary>
    /// <param name="player">The player which logs in.</param>
    /// <param name="loginName">The account's login name.</param>
    /// <returns><c>true</c>, if the player may log in.</returns>
    public static bool TryAdmit(Player? player, string loginName)
    {
        if (player?.Account?.IsBot == true)
        {
            return true;
        }

        var elsewhere = Elsewhere();
        lock (Sync)
        {
            // Every program counts together; an account already in (here or elsewhere) takes no extra place.
            var inGame = Admitted.Keys.Union(elsewhere, StringComparer.OrdinalIgnoreCase).Count();
            var alreadyIn = Admitted.ContainsKey(loginName) || elsewhere.Contains(loginName, StringComparer.OrdinalIgnoreCase);

            // Over the limit, a week of grace lets more play while the admin panel asks for a licence (owner, 2026-11-20).
            if (!alreadyIn && inGame >= Maximum && !DeveloperHub.AllowOverLimit(DateTime.UtcNow))
            {
                return false;
            }

            Admitted[loginName] = player;
            DeveloperHub.NotePlayers(alreadyIn ? inGame : inGame + 1);
        }

        PublishSoon();

        if (player is not null)
        {
            player.PlayerDisconnected += OnDisconnectedAsync;
        }

        return true;
    }

    /// <summary>
    /// Frees all places; for tests.
    /// </summary>
    internal static void Reset()
    {
        lock (Sync)
        {
            Admitted.Clear();
            _elsewhereReadUtc = DateTime.MinValue;
            _elsewhere = [];
        }
    }

    /// <summary>The accounts holding a place in the other programs (read from the shared database at most every 3 seconds).</summary>
    private static IReadOnlyCollection<string> Elsewhere()
    {
        if (ArenaShared.Current is not { } shared)
        {
            return [];
        }

        if (DateTime.UtcNow - _elsewhereReadUtc < TimeSpan.FromSeconds(3))
        {
            return _elsewhere;
        }

        try
        {
            _elsewhere = shared.LicensedElsewhere(HnmoSeasonInstance.Current);
        }
        catch (Exception)
        {
            // The shared database can't be reached now: this program counts alone until it can.
            _elsewhere = [];
        }

        _elsewhereReadUtc = DateTime.UtcNow;
        return _elsewhere;
    }

    /// <summary>Publishes this program's accounts soon (after a login or a logout), one publication at a time.</summary>
    private static void PublishSoon()
    {
        if (ArenaShared.Current is null)
        {
            return;
        }

        Interlocked.Exchange(ref _publishAgain, 1);
        if (Interlocked.Exchange(ref _publishing, 1) == 0)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    while (Interlocked.Exchange(ref _publishAgain, 0) == 1)
                    {
                        Publish();
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _publishing, 0);
                }
            });
        }
    }

    private static void Publish()
    {
        if (ArenaShared.Current is not { } shared)
        {
            return;
        }

        List<string> logins;
        lock (Sync)
        {
            logins = Admitted.Keys.ToList();
        }

        try
        {
            shared.PublishLicensed(HnmoSeasonInstance.Current, logins);
        }
        catch (Exception)
        {
            // The shared database can't be reached now: the next heartbeat tries again.
        }
    }

    private static ValueTask OnDisconnectedAsync(Player player)
    {
        player.PlayerDisconnected -= OnDisconnectedAsync;
        lock (Sync)
        {
            foreach (var (name, admitted) in Admitted.ToList())
            {
                // Only the player which holds the place frees it (a newer login of the account keeps it).
                if (ReferenceEquals(admitted, player))
                {
                    Admitted.Remove(name);
                }
            }
        }

        PublishSoon();
        return ValueTask.CompletedTask;
    }
}
