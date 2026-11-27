// <copyright file="AutoErrorEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

using System.Globalization;
using System.Text.Json.Nodes;

/// <summary>
/// The rules of the web client's automatic error reports, without the game: a cap per account and per server in a
/// Sydney day, grouping by fingerprint (count, accounts, first and last time, versions, the last 5 samples), the owner's
/// decisions, and what the HNMO developer gets of a group (never the login).
/// </summary>
public static class AutoErrorEngine
{
    /// <summary>The samples a group keeps.</summary>
    public const int MaxSamples = 5;

    /// <summary>The versions a group keeps.</summary>
    public const int MaxVersions = 10;

    /// <summary>The groups the store keeps (the oldest closed ones go first).</summary>
    public const int MaxGroups = 500;

    /// <summary>The longest fingerprint.</summary>
    public const int MaxFingerprintLength = 300;

    /// <summary>The kinds of errors the client sends.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["error", "rejection", "packet", "webgl", "disconnect"];

    /// <summary>Why a report was or wasn't kept.</summary>
    public enum Outcome
    {
        /// <summary>It started a new group.</summary>
        NewGroup,

        /// <summary>It was added to its group.</summary>
        Added,

        /// <summary>The automatic reports are off.</summary>
        Off,

        /// <summary>It had no fingerprint.</summary>
        Invalid,

        /// <summary>The account sent its automatic reports for today.</summary>
        AccountCap,

        /// <summary>The server took its automatic reports for today.</summary>
        ServerCap,
    }

    /// <summary>Keeps a report in its group, unless a cap of the day is reached.</summary>
    /// <param name="data">The data.</param>
    /// <param name="login">The account (counted for the cap and the distinct accounts; never shown to the developer).</param>
    /// <param name="fingerprint">The fingerprint the client computed.</param>
    /// <param name="sample">The report.</param>
    /// <param name="configuration">The settings.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="sydney">The Sydney time zone.</param>
    /// <returns>What happened, and the group (when kept).</returns>
    public static (Outcome Outcome, AutoErrorGroup? Group) Submit(BugReportStoreData data, string login, string? fingerprint, AutoErrorSample sample, BugReportsConfiguration configuration, DateTime nowUtc, TimeZoneInfo sydney)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.AutomaticReports)
        {
            return (Outcome.Off, null);
        }

        var print = (fingerprint ?? string.Empty).Trim();
        if (print.Length == 0 || string.IsNullOrWhiteSpace(login))
        {
            return (Outcome.Invalid, null);
        }

        print = Cut(print, MaxFingerprintLength);
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), sydney).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (data.AutoDay != today)
        {
            data.AutoDay = today;
            data.AutoAccountCounts.Clear();
            data.AutoServerCount = 0;
        }

        var account = login.Trim().ToLowerInvariant();
        var sentToday = data.AutoAccountCounts.GetValueOrDefault(account);
        if (sentToday >= configuration.AutomaticDailyLimitPerAccount)
        {
            return (Outcome.AccountCap, null);
        }

        if (data.AutoServerCount >= configuration.AutomaticDailyLimitPerServer)
        {
            return (Outcome.ServerCap, null);
        }

        data.AutoAccountCounts[account] = sentToday + 1;
        data.AutoServerCount++;

        sample.ReceivedUtc = nowUtc;
        sample.Kind = Kinds.Contains(sample.Kind) ? sample.Kind : "error";
        sample.Message = Cut(sample.Message, 500);
        sample.Stack = Cut(sample.Stack, 1500);
        sample.Version = Cut(sample.Version, 80);
        sample.Browser = Cut(sample.Browser, 200);
        sample.Os = Cut(sample.Os, 80);
        sample.Device = Cut(sample.Device, 120);
        sample.Map = Cut(sample.Map, 60);
        sample.Gpu = Cut(sample.Gpu, 160);
        sample.Character = Cut(sample.Character, 20);

        var group = data.AutoGroups.FirstOrDefault(g => g.Fingerprint == print);
        var outcome = Outcome.Added;
        if (group is null)
        {
            group = new AutoErrorGroup
            {
                Id = $"E{data.NextAutoNumber++}",
                Fingerprint = print,
                Kind = sample.Kind,
                Message = sample.Message,
                FirstSeenUtc = nowUtc,
            };
            data.AutoGroups.Add(group);
            outcome = Outcome.NewGroup;
            Trim(data);
        }

        group.Count++;
        group.LastSeenUtc = nowUtc;
        if (!group.Accounts.Contains(account))
        {
            group.Accounts.Add(account);
        }

        if (sample.Version.Length > 0 && !group.Versions.Contains(sample.Version))
        {
            group.Versions.Add(sample.Version);
            if (group.Versions.Count > MaxVersions)
            {
                group.Versions.RemoveAt(0);
            }
        }

        if (group.Status == AutoErrorStatus.Fixed)
        {
            // It came back after the owner marked it fixed.
            group.Status = AutoErrorStatus.Open;
            group.Reopened = true;
        }

        if (group.Status != AutoErrorStatus.Ignored)
        {
            group.Samples.Add(sample);
            if (group.Samples.Count > MaxSamples)
            {
                group.Samples.RemoveAt(0);
            }
        }

        return (outcome, group);
    }

    /// <summary>Sets a group's status.</summary>
    /// <param name="data">The data.</param>
    /// <param name="id">The group.</param>
    /// <param name="status">The status.</param>
    /// <returns>The group, or <c>null</c>.</returns>
    public static AutoErrorGroup? SetStatus(BugReportStoreData data, string id, AutoErrorStatus status)
    {
        if (Find(data, id) is not { } group)
        {
            return null;
        }

        group.Status = status;
        if (status != AutoErrorStatus.Open)
        {
            group.Reopened = false;
        }

        return group;
    }

    /// <summary>The groups by how many reports they have, most first.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The groups.</returns>
    public static List<AutoErrorGroup> ByCount(BugReportStoreData data) =>
        data.AutoGroups.OrderByDescending(g => g.Count).ThenByDescending(g => g.LastSeenUtc).ToList();

    /// <summary>Finds a group by its id.</summary>
    /// <param name="data">The data.</param>
    /// <param name="id">The id.</param>
    /// <returns>The group.</returns>
    public static AutoErrorGroup? Find(BugReportStoreData data, string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : data.AutoGroups.FirstOrDefault(g => string.Equals(g.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>What the developer's hub gets of a group: no login and no account count by name; the character only when allowed.</summary>
    /// <param name="group">The group.</param>
    /// <param name="shareCharacter">Whether to name the characters of the samples.</param>
    /// <param name="sydneyText">Formats a time as Sydney time.</param>
    /// <returns>The message.</returns>
    public static JsonObject HubMessage(AutoErrorGroup group, bool shareCharacter, Func<DateTime, string> sydneyText)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(sydneyText);
        var samples = new JsonArray();
        foreach (var s in group.Samples)
        {
            samples.Add(new JsonObject
            {
                ["time"] = sydneyText(s.ReceivedUtc) + " Sydney",
                ["kind"] = s.Kind,
                ["message"] = s.Message,
                ["stack"] = s.Stack,
                ["version"] = s.Version,
                ["browser"] = s.Browser,
                ["os"] = s.Os,
                ["device"] = s.Device,
                ["map"] = s.Map,
                ["gpu"] = s.Gpu,
                ["character"] = shareCharacter ? s.Character : string.Empty,
            });
        }

        return new JsonObject
        {
            ["kind"] = "auto-error",
            ["id"] = group.Id,
            ["fingerprint"] = group.Fingerprint,
            ["errorKind"] = group.Kind,
            ["message"] = group.Message,
            ["count"] = group.Count,
            ["accounts"] = group.Accounts.Count,
            ["firstSeen"] = sydneyText(group.FirstSeenUtc) + " Sydney",
            ["lastSeen"] = sydneyText(group.LastSeenUtc) + " Sydney",
            ["versions"] = new JsonArray(group.Versions.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()),
            ["status"] = group.Status.ToString(),
            ["samples"] = samples,
        };
    }

    private static void Trim(BugReportStoreData data)
    {
        while (data.AutoGroups.Count > MaxGroups)
        {
            // The oldest group the owner closed goes first; else the oldest one.
            var victim = data.AutoGroups.FirstOrDefault(g => g.Status != AutoErrorStatus.Open && (g.QueuedUtc is null || g.ForwardedUtc is not null))
                ?? data.AutoGroups[0];
            data.AutoGroups.Remove(victim);
        }
    }

    private static string Cut(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max];
}
