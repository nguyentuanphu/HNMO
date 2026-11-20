// <copyright file="LicensedPlayerLimit.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using System.Threading;

/// <summary>
/// How many players may be in the game at once in this whole process (all game servers together), as the
/// folder's license allows (set at startup; 10 without a license). Counted per account: a login of an
/// account that is already in (e.g. taking over its offline session) takes no extra place. Bots and template
/// accounts don't count. Part of the login itself, so it can't be switched off like a plug-in.
/// </summary>
public static class LicensedPlayerLimit
{
    /// <summary>The players at once without a license.</summary>
    public const int DefaultMaximum = 10;

    private static readonly Dictionary<string, Player> Admitted = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Sync = new();
    private static int _maximum = DefaultMaximum;

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
    /// Admits a login, unless the limit is reached. The place is freed when the player disconnects.
    /// </summary>
    /// <param name="player">The player which logs in.</param>
    /// <param name="loginName">The account's login name.</param>
    /// <returns><c>true</c>, if the player may log in.</returns>
    public static bool TryAdmit(Player player, string loginName)
    {
        lock (Sync)
        {
            // Over the limit, a week of grace lets more play while the admin panel asks for a licence (owner, 2026-11-20).
            if (!Admitted.ContainsKey(loginName) && Admitted.Count >= Maximum && !DeveloperHub.AllowOverLimit(DateTime.UtcNow))
            {
                return false;
            }

            Admitted[loginName] = player;
            DeveloperHub.NotePlayers(Admitted.Count);
        }

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

        return ValueTask.CompletedTask;
    }
}
