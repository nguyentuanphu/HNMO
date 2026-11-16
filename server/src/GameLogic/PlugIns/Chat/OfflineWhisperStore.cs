// <copyright file="OfflineWhisperStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// A whisper to a character which wasn't in the game.
/// </summary>
/// <param name="Sender">The name of the sending character.</param>
/// <param name="Receiver">The name of the receiving character.</param>
/// <param name="Text">The text.</param>
/// <param name="SentUtc">When it was sent (UTC).</param>
public sealed record OfflineWhisper(string Sender, string Receiver, string Text, DateTime SentUtc);

/// <summary>
/// The result of <see cref="OfflineWhisperStore.Add"/>.
/// </summary>
public enum OfflineWhisperStoreResult
{
    /// <summary>
    /// The whisper is kept until the receiver enters the game.
    /// </summary>
    Stored,

    /// <summary>
    /// The receiver has the maximum of waiting whispers already.
    /// </summary>
    ReceiverFull,
}

/// <summary>
/// Keeps the whispers to characters which aren't in the game, until they enter it: at most a number per receiver,
/// and not longer than a maximum age. With a file, the whispers outlast a restart of the server: they're kept in
/// a JSON file, so no database table is needed.
/// </summary>
public sealed class OfflineWhisperStore
{
    private const string FeatureQueue = "offlineWhispers.queue";
    private const string SectionKey = "offlineWhispers";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _lock = new();
    private readonly Dictionary<string, List<OfflineWhisper>> _whispers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<List<OfflineWhisper>> _sync = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineWhisperStore"/> class.
    /// </summary>
    /// <param name="filePath">The file which keeps the whispers over restarts; <c>null</c> keeps them in memory only.</param>
    public OfflineWhisperStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Load();
    }

    /// <summary>
    /// Gets or sets the maximum of waiting whispers per receiver.
    /// </summary>
    public int MaximumPerReceiver { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum age of a waiting whisper; older ones are dropped.
    /// </summary>
    public TimeSpan MaximumAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets the file which keeps the whispers, if any.
    /// </summary>
    public string? FilePath => this._stateStore?.FilePath;

    /// <summary>
    /// Gets the number of waiting whispers of a receiver.
    /// </summary>
    /// <param name="receiver">The receiver's character name.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The number of waiting whispers.</returns>
    public int Count(string receiver, DateTime nowUtc)
    {
        lock (this._lock)
        {
            return this._whispers.TryGetValue(receiver, out var list) ? list.Count(w => !this.IsExpired(w, nowUtc)) : 0;
        }
    }

    /// <summary>
    /// Keeps a whisper for its receiver.
    /// </summary>
    /// <param name="sender">The sender's character name.</param>
    /// <param name="receiver">The receiver's character name.</param>
    /// <param name="text">The text.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>Whether it was kept.</returns>
    public OfflineWhisperStoreResult Add(string sender, string receiver, string text, DateTime nowUtc)
    {
        lock (this._lock)
        {
            this.RemoveExpired(nowUtc);
            if (!this._whispers.TryGetValue(receiver, out var list))
            {
                list = new List<OfflineWhisper>();
                this._whispers[receiver] = list;
            }

            if (list.Count >= this.MaximumPerReceiver)
            {
                return OfflineWhisperStoreResult.ReceiverFull;
            }

            list.Add(new OfflineWhisper(sender, receiver, text, nowUtc));
            this.Save();
            return OfflineWhisperStoreResult.Stored;
        }
    }

    /// <summary>
    /// Takes the waiting whispers of a receiver, oldest first; they're removed from the store.
    /// </summary>
    /// <param name="receiver">The receiver's character name.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The whispers which aren't too old.</returns>
    public IReadOnlyList<OfflineWhisper> Take(string receiver, DateTime nowUtc)
    {
        lock (this._lock)
        {
            if (!this._whispers.Remove(receiver, out var list))
            {
                return [];
            }

            this.RemoveExpired(nowUtc);
            this.Save();
            return list.Where(w => !this.IsExpired(w, nowUtc)).OrderBy(w => w.SentUtc).ToList();
        }
    }

    private bool IsExpired(OfflineWhisper whisper, DateTime nowUtc) => nowUtc - whisper.SentUtc > this.MaximumAge;

    private void RemoveExpired(DateTime nowUtc)
    {
        foreach (var (receiver, list) in this._whispers.ToList())
        {
            list.RemoveAll(w => this.IsExpired(w, nowUtc));
            if (list.Count == 0)
            {
                this._whispers.Remove(receiver);
            }
        }
    }

    private void Load()
    {
        if (this._stateStore is null)
        {
            return;
        }

        var whispers = this._stateStore.ReadSection<List<OfflineWhisper>>(SectionKey, JsonOptions) ?? [];
        foreach (var w in whispers)
        {
            if (!this._whispers.TryGetValue(w.Receiver, out var list))
            {
                list = [];
                this._whispers[w.Receiver] = list;
            }

            list.Add(w);
        }

        if (this._sync.LoadOrMigrate(
            this._rowBackend,
            FeatureQueue,
            this._whispers,
            JsonOptions,
            (_, l) => l.Count > 0 ? l.Max(w => w.SentUtc) + this.MaximumAge : null))
        {
            this._stateStore.RemoveSection(SectionKey);
        }
    }

    private void Save()
    {
        if (this._stateStore is null)
        {
            return;
        }

        this._sync.SaveChanges(
            this._rowBackend,
            FeatureQueue,
            this._whispers,
            JsonOptions,
            (_, l) => l.Count > 0 ? l.Max(w => w.SentUtc) + this.MaximumAge : null);
    }
}
