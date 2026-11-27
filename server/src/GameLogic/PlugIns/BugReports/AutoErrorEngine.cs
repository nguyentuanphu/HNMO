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

        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(login) || !Kinds.Contains(sample.Kind))
        {
            return (Outcome.Invalid, null);
        }

        var print = AutoErrorPayload.CleanFingerprint(sample.Kind, fingerprint);
        AutoErrorPayload.CleanSample(sample);
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), sydney).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (data.AutoDay != today)
        {
            data.AutoDay = today;
            data.AutoAccountCounts.Clear();
            data.AutoServerCount = 0;
        }

        var account = login.Trim().ToLowerInvariant();
        var sentToday = data.AutoAccountCounts.GetValueOrDefault(account);
        if (sentToday >= Math.Clamp(configuration.AutomaticDailyLimitPerAccount, 0, 100))
        {
            return (Outcome.AccountCap, null);
        }

        if (data.AutoServerCount >= Math.Clamp(configuration.AutomaticDailyLimitPerServer, 0, 10000))
        {
            return (Outcome.ServerCap, null);
        }

        data.AutoAccountCounts[account] = sentToday + 1;
        data.AutoServerCount++;

        sample.ReceivedUtc = nowUtc;
        var group = data.AutoGroups.FirstOrDefault(g => g.Fingerprint == print);
        var outcome = Outcome.Added;
        if (group is null)
        {
            if (data.AutoGroups.Count >= MaxGroups)
            {
                Trim(data);
            }

            group = new AutoErrorGroup
            {
                Id = $"E{data.NextAutoNumber++}",
                ApprovalId = Guid.NewGuid(),
                Fingerprint = print,
                Kind = sample.Kind,
                Message = sample.Message,
                FirstSeenUtc = nowUtc,
            };
            data.AutoGroups.Add(group);
            outcome = Outcome.NewGroup;
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

    /// <summary>Builds a privacy-filtered snapshot of the exact group approved by the host.</summary>
    /// <param name="group">The approved group.</param>
    /// <param name="season">The server's season.</param>
    /// <returns>The weekly report entry.</returns>
    public static JsonObject HubMessage(AutoErrorGroup group, string season)
    {
        var kind = AutoErrorPayload.CleanKind(group.Kind);
        return new JsonObject
        {
            ["kind"] = "auto-error",
            ["approval"] = group.ApprovalId.ToString("N"),
            ["season"] = season,
            ["errorKind"] = kind,
            ["fingerprint"] = AutoErrorPayload.CleanFingerprint(kind, group.Fingerprint),
            ["count"] = group.Count,
        };
    }

    private static void Trim(BugReportStoreData data)
    {
        var victim = data.AutoGroups.FirstOrDefault(g => g.Status != AutoErrorStatus.Open)
            ?? data.AutoGroups[0];
        data.AutoGroups.Remove(victim);
    }
}
