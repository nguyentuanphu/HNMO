// <copyright file="FolderSeat.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// The licence's folder limit for this program: while the HNMO developer's hub has it switched on
/// (<see cref="DeveloperHub.FolderLimitEnforced"/>), at most the licence's <c>folders</c> (1 without a licence, the free
/// build too) HNMO folders hold a seat on this computer at once (<see cref="FolderSeats"/>). A folder without a seat lets
/// no player log in (<see cref="LicensedPlayerLimit"/> allows 0), the admin panel says why, and the log says so once. It
/// is checked every <see cref="FolderSeats.Heartbeat"/>: once a seat is free, or the hub switches the limit off, the
/// folder takes players again without a restart. While the limit is off nothing happens: no seat file, no warning.
/// </summary>
public static class FolderSeat
{
    private static readonly Lock Sync = new();
    private static FolderSeats? _seats;
    private static ILogger? _logger;
    private static Timer? _timer;
    private static FolderSeatStatus _status = FolderSeatStatus.Off;
    private static bool _directoryFailed;

    /// <summary>Gets the folder limit as this program saw it at its last heartbeat.</summary>
    public static FolderSeatStatus Status => Volatile.Read(ref _status);

    /// <summary>Gets a value indicating whether no player may log in to this folder now (the limit is on and no seat is free).</summary>
    public static bool IsBlocked => Status.IsBlocked;

    /// <summary>Sets this folder's seats; until then, and without a folder code, the limit never applies.</summary>
    /// <param name="seats">The seats of this computer for this folder.</param>
    /// <param name="logger">The log.</param>
    public static void Configure(FolderSeats? seats, ILogger? logger)
    {
        lock (Sync)
        {
            _seats = seats;
            _logger = logger;
        }
    }

    /// <summary>Checks now and then every <see cref="FolderSeats.Heartbeat"/>.</summary>
    public static void Start()
    {
        Evaluate(DateTime.UtcNow);
        _timer ??= new Timer(_ => Evaluate(DateTime.UtcNow), null, FolderSeats.Heartbeat, FolderSeats.Heartbeat);
    }

    /// <summary>Beats and checks the seat: whether the limit is on, and whether this folder holds a seat.</summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The status.</returns>
    public static FolderSeatStatus Evaluate(DateTime nowUtc)
    {
        lock (Sync)
        {
            var previous = _status;
            if (_seats is not { } seats || !DeveloperHub.FolderLimitEnforced(nowUtc))
            {
                if (previous.Enforced)
                {
                    _seats?.Release();
                    _logger?.LogInformation("HNMO licence: the HNMO developer switched the folder limit off; this folder runs freely.");
                }

                Volatile.Write(ref _status, FolderSeatStatus.Off);
                return FolderSeatStatus.Off;
            }

            var allowed = LicensedPlayerLimit.FoldersAllowed(nowUtc);
            FolderSeatStatus status;
            try
            {
                var result = seats.Refresh(allowed);
                status = new FolderSeatStatus(true, result.HasSeat, result.Allowed, seats.MachineId, result.OtherHolders);
                _directoryFailed = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Never lock wrongly: a seats directory which can't be written doesn't stop the folder.
                if (!_directoryFailed)
                {
                    _logger?.LogWarning(ex, "HNMO licence: the folder seats in {Directory} can't be written; this folder runs without a seat.", seats.Directory);
                }

                _directoryFailed = true;
                status = new FolderSeatStatus(true, true, allowed, seats.MachineId, []);
            }

            Volatile.Write(ref _status, status);
            Report(previous, status, seats);
            return status;
        }
    }

    /// <summary>Forgets the seats and the status; for tests.</summary>
    internal static void Reset()
    {
        lock (Sync)
        {
            _seats = null;
            _logger = null;
            _directoryFailed = false;
            Volatile.Write(ref _status, FolderSeatStatus.Off);
        }
    }

    private static void Report(FolderSeatStatus previous, FolderSeatStatus now, FolderSeats seats)
    {
        if (_logger is not { } logger || (previous.Enforced == now.Enforced && previous.HasSeat == now.HasSeat))
        {
            return;
        }

        if (now.HasSeat)
        {
            logger.LogInformation(
                "HNMO licence: the folder limit is on; this HNMO folder ({Folder}) holds a seat on this computer (id {MachineId}), {Allowed} folder(s) may run at once. Players can log in.",
                seats.FolderPath,
                now.MachineId,
                now.Allowed);
            return;
        }

        logger.LogWarning(
            "HNMO licence: this HNMO folder ({Folder}) has no seat on this computer (id {MachineId}): the licence lets {Allowed} HNMO folder(s) run at once and these already do: {Holders}. No player can log in here until one of them stops (checked every {Seconds} seconds) or a licence allows more folders.",
            seats.FolderPath,
            now.MachineId,
            now.Allowed,
            string.Join("; ", now.OtherHolders),
            (int)FolderSeats.Heartbeat.TotalSeconds);
    }
}
