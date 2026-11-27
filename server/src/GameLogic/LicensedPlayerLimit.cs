// <copyright file="LicensedPlayerLimit.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

using System.Text;
using System.Threading;
using HNMO.Integrity;
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
    private static byte[]? _licenceBytes;
    private static Func<IntegrityStatus>? _integrityStatusProvider;
    private static Func<byte[]?, DateOnly, PlayerLicense>? _licenseEvaluator;
    private static bool _hasVerificationContext;
    private static IReadOnlyCollection<string> _elsewhere = [];
    private static DateTime _elsewhereReadUtc = DateTime.MinValue;
    private static int _publishing;
    private static int _publishAgain;
    private static Timer? _heartbeat;

    /// <summary>
    /// Gets or sets the players allowed at once (at least 1), always bounded by independent re-verification
    /// of build integrity, signed licence, withdrawal state, and developer-hub reachability.
    /// </summary>
    public static int Maximum
    {
        get => ResolveEffectiveMaximum(DateTime.UtcNow);
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

        var effectiveMaximum = ReevaluateAndEnforce(DateTime.UtcNow);
        var elsewhere = Elsewhere();
        lock (Sync)
        {
            // Every program counts together; an account already in (here or elsewhere) takes no extra place.
            var inGame = Admitted.Keys.Union(elsewhere, StringComparer.OrdinalIgnoreCase).Count();
            var alreadyIn = Admitted.ContainsKey(loginName) || elsewhere.Contains(loginName, StringComparer.OrdinalIgnoreCase);

            // Over the limit, a week of grace lets more play while the admin panel asks for a licence (owner, 2026-11-20).
            if (!alreadyIn && inGame >= effectiveMaximum && !DeveloperHub.AllowOverLimit(DateTime.UtcNow))
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
    /// Registers the runtime build integrity check and optional custom licence evaluator used to re-derive
    /// the player cap independently at startup, login, and weekly ticks.
    /// </summary>
    /// <param name="integrityStatusProvider">Returns the current build <see cref="IntegrityStatus"/>.</param>
    /// <param name="licenseEvaluator">Optional custom licence evaluator (primarily for tests).</param>
    public static void ConfigureBuildIntegrity(
        Func<IntegrityStatus> integrityStatusProvider,
        Func<byte[]?, DateOnly, PlayerLicense>? licenseEvaluator = null)
    {
        _integrityStatusProvider = integrityStatusProvider ?? throw new ArgumentNullException(nameof(integrityStatusProvider));
        if (licenseEvaluator is not null)
        {
            _licenseEvaluator = licenseEvaluator;
        }

        _hasVerificationContext = true;
    }

    /// <summary>
    /// Stores raw signed licence bytes and applies the verified cap (or silently falls back to <see cref="DefaultMaximum"/>
    /// when binaries, signature, withdrawal state, or hub reachability fail validation).
    /// </summary>
    /// <param name="licenceBytes">The raw signed <c>license.hnmo</c> bytes, or <c>null</c>.</param>
    /// <param name="nowUtc">Optional UTC timestamp (for deterministic tests).</param>
    /// <returns>The derived effective player limit.</returns>
    public static int ApplyLicence(byte[]? licenceBytes, DateTime? nowUtc = null)
    {
        _licenceBytes = licenceBytes is { Length: > 0 } ? [.. licenceBytes] : null;
        _hasVerificationContext = true;

        var now = nowUtc ?? DateTime.UtcNow;
        var (derived, licensee) = EvaluateSource(now);
        Volatile.Write(ref _maximum, derived);
        Licensee = derived > DefaultMaximum ? licensee : null;
        return derived;
    }

    /// <summary>
    /// Independently re-derives the allowed player cap from build integrity, signed licence, withdrawal list,
    /// and hub unreachability past <see cref="DeveloperHub.GraceDays"/>, clamping any tampered in-memory value.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The effective player cap.</returns>
    public static int ReevaluateAndEnforce(DateTime nowUtc)
    {
        var (derived, licensee) = EvaluateSource(nowUtc);
        var current = Volatile.Read(ref _maximum);
        var effective = _hasVerificationContext || current > DefaultMaximum ? Math.Min(current, derived) : current;
        if (_hasVerificationContext || current > DefaultMaximum)
        {
            Volatile.Write(ref _maximum, effective);
            if (derived <= DefaultMaximum)
            {
                Licensee = null;
            }
            else if (Licensee is null)
            {
                Licensee = licensee;
            }
        }

        return effective;
    }

    /// <summary>
    /// Clears verification state and restores defaults; for tests.
    /// </summary>
    internal static void ResetVerification()
    {
        _licenceBytes = null;
        _integrityStatusProvider = null;
        _licenseEvaluator = null;
        _hasVerificationContext = false;
        Volatile.Write(ref _maximum, DefaultMaximum);
        Licensee = null;
    }

    private static int ResolveEffectiveMaximum(DateTime nowUtc)
    {
        var (derived, _) = EvaluateSource(nowUtc);
        var current = Volatile.Read(ref _maximum);
        return _hasVerificationContext || current > DefaultMaximum ? Math.Min(current, derived) : current;
    }

    private static (int Maximum, string? Licensee) EvaluateSource(DateTime nowUtc)
    {
        var integrity = _integrityStatusProvider?.Invoke() ?? IntegrityStatus.Ok;
        if (integrity is IntegrityStatus.SignatureInvalid or IntegrityStatus.ManifestInvalid or IntegrityStatus.FileMismatch)
        {
            return (DefaultMaximum, null);
        }

        if (DeveloperHub.IsHubUnreachablePastGrace(nowUtc))
        {
            return (DefaultMaximum, null);
        }

        var bytes = _licenceBytes ?? (DeveloperHub.LicenceFile is { Length: > 0 } text ? Encoding.UTF8.GetBytes(text) : null);
        if (bytes is null or { Length: 0 })
        {
            return (DefaultMaximum, null);
        }

        var today = DateOnly.FromDateTime(nowUtc);
        var license = (_licenseEvaluator ?? DefaultLicenseEvaluator)(bytes, today);
        if (!license.IsValid)
        {
            return (DefaultMaximum, null);
        }

        var licenceText = Encoding.UTF8.GetString(bytes);
        if (DeveloperHub.IsWithdrawn(licenceText))
        {
            return (DefaultMaximum, null);
        }

        return (Math.Max(1, license.MaximumPlayers), license.Licensee);
    }

    private static PlayerLicense DefaultLicenseEvaluator(byte[]? bytes, DateOnly today)
        => PlayerLicense.CheckWithEmbeddedKeys(
            bytes,
            DeveloperHub.MachineId.Length > 0 ? DeveloperHub.MachineId : PlayerLicense.MachineId,
            today,
            DeveloperHub.LegacyMachineId.Length > 0 ? DeveloperHub.LegacyMachineId : PlayerLicense.LegacyMachineId);

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
