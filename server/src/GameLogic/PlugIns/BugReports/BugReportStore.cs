// <copyright file="BugReportStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

using System.IO;
using System.Text.Json;

/// <summary>The bug reports in the state file, read and changed under the file's lock.</summary>
public sealed class BugReportStore
{
    private const string SectionKey = "bugReports";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private BugReportStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the store of a state file.</summary>
    /// <param name="filePath">The state file, or null/blank for a temporary one.</param>
    /// <returns>The store.</returns>
    public static BugReportStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-bugs-{Guid.NewGuid():N}.json"));
        return new BugReportStore(store);
    }

    /// <summary>Reads the data under the lock.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <returns>The result.</returns>
    public TResult Read<TResult>(Func<BugReportStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            return reader(this.ReadSanitizedData());
        }
    }

    /// <summary>Changes the data under the lock and saves it when changed.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="mutator">The change.</param>
    /// <returns>The result.</returns>
    public TResult Mutate<TResult>(Func<BugReportStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this.ReadSanitizedData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }

    private BugReportStoreData ReadSanitizedData()
    {
        var data = this._store.ReadSection<BugReportStoreData>(SectionKey, JsonOptions) ?? new BugReportStoreData();
        var changed = false;
        foreach (var group in data.AutoGroups.Where(g => g.ApprovalId == Guid.Empty))
        {
            group.ApprovalId = Guid.NewGuid();
            group.Kind = AutoErrorPayload.CleanKind(group.Kind);
            group.Fingerprint = AutoErrorPayload.CleanFingerprint(group.Kind, group.Fingerprint);
            group.Message = AutoErrorPayload.Label(group.Kind);
            group.Versions = group.Versions.Select(AutoErrorPayload.CleanVersion).Where(v => v.Length > 0).Distinct().ToList();
            foreach (var sample in group.Samples)
            {
                AutoErrorPayload.CleanSample(sample);
            }

            // Previous versions acknowledged the whole weekly response even when an auto-error was discarded.
            // Require fresh approval for legacy groups before anything is sent.
            group.QueuedUtc = null;
            group.ForwardedUtc = null;
            changed = true;
        }

        if (changed)
        {
            // A write copies the previous file to .bak, so the first write would leave the old text there; the second
            // makes the backup the cleaned file too.
            this._store.WriteSection(SectionKey, data, JsonOptions);
            this._store.WriteSection(SectionKey, data, JsonOptions);
        }

        return data;
    }
}
