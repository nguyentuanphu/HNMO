// <copyright file="AutoErrorOutbox.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.Interfaces;

/// <summary>
/// Shares only host-approved snapshots with the primary weekly sender. Each season retains its private report store.
/// </summary>
public static class AutoErrorOutbox
{
    private static readonly object Gate = new();
    private static readonly string[] Seasons = [HnmoSeasonInstance.SeasonSix, HnmoSeasonInstance.SeasonNine, HnmoSeasonInstance.Arena];

    /// <summary>An approved report and its source season.</summary>
    /// <param name="Id">The approval key.</param>
    /// <param name="Season">The source season.</param>
    /// <param name="Message">The approved snapshot.</param>
    public sealed record Entry(Guid Id, string Season, JsonObject Message);

    /// <summary>Atomically queues a snapshot of a group at the moment the host approves it.</summary>
    /// <param name="group">The group.</param>
    /// <param name="season">The current season.</param>
    /// <returns>Whether the group was queued.</returns>
    public static bool TryApprove(AutoErrorGroup group, string season) => TryApprove(group, season, ResolveRoot());

    /// <summary>Reads the approval and delivery timestamps for the Admin Panel.</summary>
    /// <param name="group">The group.</param>
    /// <param name="season">The current season.</param>
    public static void ReadStatus(AutoErrorGroup group, string season)
    {
        var (pending, sent) = Paths(ResolveRoot(), season, group.ApprovalId);
        var receipt = new FileInfo(sent);
        if (receipt.Exists)
        {
            group.QueuedUtc = receipt.CreationTimeUtc;
            group.ForwardedUtc = receipt.LastWriteTimeUtc;
        }
        else if (File.Exists(pending))
        {
            group.QueuedUtc = File.GetLastWriteTimeUtc(pending);
        }
    }

    /// <summary>Loads up to 50 approved snapshots, oldest first across the seasons on this host.</summary>
    /// <returns>Messages that have not been individually acknowledged.</returns>
    public static List<Entry> ReadPending() => ReadPending(ResolveRoot());

    /// <summary>Moves only explicitly acknowledged entries to receipts.</summary>
    /// <param name="batch">The entries included in the request.</param>
    /// <param name="accepted">The identifiers acknowledged by the hub.</param>
    public static void MarkAccepted(IReadOnlyList<Entry> batch, IReadOnlySet<Guid> accepted) =>
        MarkAccepted(batch, accepted, ResolveRoot());

    internal static bool TryApprove(AutoErrorGroup group, string season, string root)
    {
        if (group.ApprovalId == Guid.Empty)
        {
            return false;
        }

        var (pending, sent) = Paths(root, season, group.ApprovalId);
        var directory = Path.GetDirectoryName(pending)!;
        lock (Gate)
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(pending) || File.Exists(sent)
                || Directory.EnumerateFiles(directory, "*.pending").Take(AutoErrorEngine.MaxGroups).Count() >= AutoErrorEngine.MaxGroups)
            {
                return false;
            }

            var temp = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(file, AutoErrorEngine.HubMessage(group, season));
                    file.Flush(flushToDisk: true);
                }

                File.Move(temp, pending);
                return true;
            }
            finally
            {
                File.Delete(temp);
            }
        }
    }

    internal static List<Entry> ReadPending(string root)
    {
        var files = new List<(string Season, FileInfo File)>();
        foreach (var season in Seasons)
        {
            var directory = new DirectoryInfo(Path.Combine(root, season));
            if (directory.Exists)
            {
                files.AddRange(directory.EnumerateFiles("*.pending").Select(file => (season, file)));
            }
        }

        var result = new List<Entry>();
        foreach (var (season, file) in files.OrderBy(x => x.File.LastWriteTimeUtc).ThenBy(x => x.File.Name, StringComparer.Ordinal))
        {
            if (result.Count == 50)
            {
                break;
            }

            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "N", out var id) || file.Length > 1024)
            {
                continue;
            }

            try
            {
                if (JsonNode.Parse(File.ReadAllText(file.FullName)) is JsonObject message
                    && (string?)message["approval"] == id.ToString("N") && (string?)message["season"] == season)
                {
                    result.Add(new Entry(id, season, message));
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                // A damaged entry must not block other approved reports.
            }
        }

        return result;
    }

    internal static void MarkAccepted(IReadOnlyList<Entry> batch, IReadOnlySet<Guid> accepted, string root)
    {
        foreach (var entry in batch.Where(e => accepted.Contains(e.Id)))
        {
            var (pending, sent) = Paths(root, entry.Season, entry.Id);
            if (File.Exists(pending))
            {
                File.Move(pending, sent);
                File.SetLastWriteTimeUtc(sent, DateTime.UtcNow);
            }
        }
    }

    private static (string Pending, string Sent) Paths(string root, string season, Guid id)
    {
        if (!Seasons.Contains(season))
        {
            throw new ArgumentOutOfRangeException(nameof(season));
        }

        var path = Path.Combine(root, season, id.ToString("N"));
        return (path + ".pending", path + ".sent");
    }

    private static string ResolveRoot()
    {
        var path = HnmoStateStore.ResolveStateFilePath(HnmoStateStore.StateFileName)!;
        var directory = Path.GetDirectoryName(path)!;
        var shared = HnmoSeasonInstance.IsSecondary ? Path.GetDirectoryName(directory)! : directory;
        return Path.Combine(shared, "auto-error-outbox");
    }
}
