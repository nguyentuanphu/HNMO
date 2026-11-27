// <copyright file="ArenaScheduleNoticesPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Properties;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// On the home servers (owner's choice, 2026-11-21): the Arena's battle royale and guild war are announced in the game ten
/// minutes before and at the start, from the schedule the Arena publishes in the shared database. The same schedule
/// puts them in the events timetable (H) and so in the web push alerts.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ArenaScheduleNoticesPlugIn_Name), Description = nameof(PlugInResources.ArenaScheduleNoticesPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C5A1E923-4F7B-4D86-9E2A-61B3D0F8A7C4")]
public sealed class ArenaScheduleNoticesPlugIn : IPeriodicTaskPlugIn
{
    /// <summary>How long before a start the first notice goes out.</summary>
    public static readonly TimeSpan NoticeBefore = TimeSpan.FromMinutes(10);

    private static readonly object CacheGate = new();
    private static IReadOnlyList<ArenaScheduleEntry> _cache = [];
    private static DateTime _cacheReadUtc = DateTime.MinValue;

    private readonly ConcurrentDictionary<string, byte> _sent = new();
    private DateTime _nextUtc;

    /// <summary>The Arena's scheduled battles (read from the shared database at most every 30 seconds per program).</summary>
    /// <returns>The battles; none when the Arena doesn't run or there's no shared database.</returns>
    public static IReadOnlyList<ArenaScheduleEntry> Schedules()
    {
        lock (CacheGate)
        {
            if (DateTime.UtcNow - _cacheReadUtc < TimeSpan.FromSeconds(30))
            {
                return _cache;
            }

            _cacheReadUtc = DateTime.UtcNow;
            try
            {
                _cache = ArenaShared.Current?.Schedules() ?? [];
            }
            catch (Exception)
            {
                // The database can't be reached now: nothing to show; the next read tries again.
                _cache = [];
            }

            return _cache;
        }
    }

    /// <summary>The notice for a battle, ten minutes before (<paramref name="starting"/> false) or at the start.</summary>
    /// <param name="entry">The battle.</param>
    /// <param name="starting">Whether it starts now.</param>
    /// <returns>The EN and VI text.</returns>
    public static (string En, string Vi) NoticeOf(ArenaScheduleEntry entry, bool starting)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var at = entry.NextStartUtc is { } start
            ? TimeZoneInfo.ConvertTimeFromUtc(start, ArenaRanking.SydneyTime).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "?";
        return (entry.Kind, starting) switch
        {
            ("royale", false) => ($"[Arena] Battle royale at {at} (Sydney) on Lorencia: the sign-up opens 5 minutes before. Go to the Arena with Alt+A or the Arena Herald.", $"[Đấu trường] Đấu sinh tồn lúc {at} (giờ Sydney) ở Lorencia: đăng ký mở trước 5 phút. Tới Đấu trường bằng Alt+A hoặc Sứ giả Đấu trường."),
            ("royale", true) => ("[Arena] The battle royale starts now.", "[Đấu trường] Đấu sinh tồn bắt đầu."),
            ("guildwar", false) => ($"[Arena] Guild war at {at} (Sydney) on the Valley of Loren: guild members, sign up in the Arena before the sign-up closes (10 minutes before). Alt+A or the Arena Herald takes you there.", $"[Đấu trường] Bang chiến lúc {at} (giờ Sydney) ở Thung lũng Loren: thành viên bang hãy đăng ký trong Đấu trường trước khi đóng (10 phút trước giờ). Alt+A hoặc Sứ giả Đấu trường đưa bạn tới."),
            _ => ("[Arena] The guild war starts now.", "[Đấu trường] Bang chiến bắt đầu."),
        };
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (ArenaRules.Active || now < this._nextUtc)
        {
            return;
        }

        this._nextUtc = now.AddSeconds(20);
        foreach (var entry in Schedules())
        {
            if (entry.NextStartUtc is not { } start)
            {
                continue;
            }

            var starting = now >= start && now < start + TimeSpan.FromMinutes(2);
            var soon = now >= start - NoticeBefore && now < start;
            if ((!starting && !soon) || !this._sent.TryAdd($"{entry.Kind}|{start:o}|{starting}", 0))
            {
                continue;
            }

            var (en, vi) = NoticeOf(entry, starting);
            foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
            {
                await player.ShowBlueMessageAsync(string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase) ? vi : en).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextUtc = DateTime.MinValue;
}
