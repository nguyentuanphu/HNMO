// <copyright file="LuckyNumberStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Daily Lucky Number state to plain-ASCII JSON for packet <c>F5 35</c> and website APIs.
/// </summary>
public static class LuckyNumberStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Writes public state (no personal picks/owed items).</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="itemName">Optional jewel item name resolver.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(LuckyNumberEngine engine, DateTime utcNow, Func<short, short, string?>? itemName = null) =>
        WriteInternal(engine, loginName: null, utcNow, itemName, actionResult: null, messageEn: null, messageVi: null);

    /// <summary>Writes account-specific state including today's picks and unclaimed owed jewels.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="loginName">Account login name.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="itemName">Optional jewel item name resolver.</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WriteAccountState(
        LuckyNumberEngine engine,
        string loginName,
        DateTime utcNow,
        Func<short, short, string?>? itemName = null,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null) =>
        WriteInternal(engine, loginName, utcNow, itemName, actionResult, messageEn, messageVi);

    private static string WriteInternal(
        LuckyNumberEngine engine,
        string? loginName,
        DateTime utcNow,
        Func<short, short, string?>? itemName,
        string? actionResult,
        string? messageEn,
        string? messageVi)
    {
        var cfg = engine.Configuration;
        var (roundKey, nextDrawUtc) = engine.ResolveActiveRound(utcNow);
        var (exactPct, lastDigitPct) = engine.ComputeOddsPercent();

        var snapshot = engine.Store.Read(data =>
        {
            var currentRound = data.Rounds.FirstOrDefault(r => r.DateKey == roundKey);
            var myPicks = string.IsNullOrWhiteSpace(loginName) || currentRound is null
                ? []
                : currentRound.Picks
                    .Where(p => string.Equals(p.LoginName, loginName, StringComparison.OrdinalIgnoreCase))
                    .Select(p => new { number = p.Number, source = p.Source, at = ToMs(p.PickedAtUtc) })
                    .ToList();

            var owed = string.IsNullOrWhiteSpace(loginName) || !data.OwedByAccount.TryGetValue(loginName, out var owedList)
                ? []
                : owedList.Select(o => new
                {
                    id = o.Id,
                    dateKey = o.DateKey,
                    tier = o.Tier,
                    group = o.Group,
                    number = o.Number,
                    count = o.Count,
                    name = itemName?.Invoke((short)o.Group, (short)o.Number) ?? $"Item {o.Group}:{o.Number}",
                }).ToList();

            var recentDraws = data.Rounds
                .Where(r => r.DrawnNumber.HasValue)
                .OrderByDescending(r => r.DateKey, StringComparer.Ordinal)
                .Take(10)
                .Select(r => new
                {
                    dateKey = r.DateKey,
                    winningNumber = r.DrawnNumber!.Value,
                    drawnAt = r.DrawnAtUtc is { } dt ? ToMs(dt) : 0L,
                    pickCount = r.Picks.Count,
                    winners = r.Winners.Select(w => new
                    {
                        name = w.CharacterName,
                        number = w.Number,
                        tier = w.Tier,
                        coins = w.Coins,
                        coinType = w.CoinType.ToString(),
                        jewelCount = w.JewelCount,
                    }).ToList(),
                }).ToList();

            return new { totalPicksToday = currentRound?.Picks.Count ?? 0, myPicks, owed, recentDraws };
        });

        var exactJewelName = cfg.ExactMatchJewelCount > 0
            ? itemName?.Invoke((short)cfg.ExactMatchJewelGroup, (short)cfg.ExactMatchJewelNumber) ?? "Jewel of Bless"
            : string.Empty;
        var lastJewelName = cfg.EnableLastDigitPrize && cfg.LastDigitJewelCount > 0
            ? itemName?.Invoke((short)cfg.LastDigitJewelGroup, (short)cfg.LastDigitJewelNumber) ?? "Jewel of Soul"
            : string.Empty;

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            allowWebsite = cfg.AllowWebsite,
            now = ToMs(utcNow),
            timeZone = string.IsNullOrWhiteSpace(cfg.TimeZoneId) ? "Australia/Sydney" : cfg.TimeZoneId,
            drawTime = cfg.DrawTime,
            roundKey,
            nextDrawAt = ToMs(nextDrawUtc),
            minNumber = Math.Min(cfg.MinNumber, cfg.MaxNumber),
            maxNumber = Math.Max(cfg.MinNumber, cfg.MaxNumber),
            maxPicksPerDay = Math.Max(1, cfg.MaxPicksPerAccountPerDay),
            entryCostZen = cfg.EntryCostZen,
            odds = new { exactPercent = exactPct, lastDigitPercent = lastDigitPct },
            prizes = new
            {
                exact = new
                {
                    coins = cfg.ExactMatchCoins,
                    coinType = cfg.ExactMatchCoinType.ToString(),
                    jewelCount = cfg.ExactMatchJewelCount,
                    jewelName = exactJewelName,
                },
                lastDigit = new
                {
                    enabled = cfg.EnableLastDigitPrize,
                    coins = cfg.LastDigitCoins,
                    coinType = cfg.LastDigitCoinType.ToString(),
                    jewelCount = cfg.LastDigitJewelCount,
                    jewelName = lastJewelName,
                },
            },
            snapshot.totalPicksToday,
            snapshot.myPicks,
            snapshot.owed,
            snapshot.recentDraws,
            @event = actionResult is null ? null : new { result = actionResult, textEn = messageEn, textVi = messageVi },
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static long ToMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u");
                sb.Append(((int)c).ToString("x4"));
            }
        }

        return sb.ToString();
    }
}
