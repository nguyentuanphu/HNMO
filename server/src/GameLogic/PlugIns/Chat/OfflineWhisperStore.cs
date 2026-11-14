// <copyright file="OfflineWhisperStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.IO;
using System.Text.Json;

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
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _lock = new();
    private readonly Dictionary<string, List<OfflineWhisper>> _whispers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineWhisperStore"/> class.
    /// </summary>
    /// <param name="filePath">The file which keeps the whispers over restarts; <c>null</c> keeps them in memory only.</param>
    public OfflineWhisperStore(string? filePath = null)
    {
        this._filePath = filePath;
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
    public string? FilePath => this._filePath;

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
        if (this._filePath is null || !File.Exists(this._filePath))
        {
            return;
        }

        try
        {
            var whispers = JsonSerializer.Deserialize<List<OfflineWhisper>>(File.ReadAllText(this._filePath), JsonOptions) ?? [];
            foreach (var whisper in whispers.Where(w => !string.IsNullOrEmpty(w.Receiver)))
            {
                if (!this._whispers.TryGetValue(whisper.Receiver, out var list))
                {
                    list = new List<OfflineWhisper>();
                    this._whispers[whisper.Receiver] = list;
                }

                list.Add(whisper);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A broken file loses its whispers, but doesn't stop the server.
        }
    }

    private void Save()
    {
        if (this._filePath is null)
        {
            return;
        }

        try
        {
            if (Path.GetDirectoryName(this._filePath) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = this._filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this._whispers.Values.SelectMany(l => l).ToList(), JsonOptions));
            File.Move(temporary, this._filePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The whispers stay in memory; they're written again with the next change.
        }
    }
}
