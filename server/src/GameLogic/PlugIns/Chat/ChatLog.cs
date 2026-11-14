// <copyright file="ChatLog.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using HNMO.GameLogic.Views;

/// <summary>
/// A chat line in the <see cref="ChatLog"/>.
/// </summary>
/// <param name="Id">The running number of the line.</param>
/// <param name="TimeUtc">When it was sent (UTC).</param>
/// <param name="ServerId">The id of the game server.</param>
/// <param name="MapName">The name of the sender's map.</param>
/// <param name="Sender">The sender's character name.</param>
/// <param name="AccountId">The id of the sender's account.</param>
/// <param name="Kind">The kind of the message.</param>
/// <param name="Receiver">The receiver of a whisper.</param>
/// <param name="Text">The text.</param>
public sealed record ChatLogEntry(long Id, DateTime TimeUtc, int ServerId, string MapName, string Sender, Guid AccountId, ChatMessageType Kind, string? Receiver, string Text);

/// <summary>
/// Keeps the last chat lines of all game servers of this process in memory, for the admin panel's chat log.
/// </summary>
public sealed class ChatLog
{
    /// <summary>
    /// The default number of kept lines.
    /// </summary>
    public const int DefaultCapacity = 2000;

    private readonly object _lock = new();
    private readonly Queue<ChatLogEntry> _entries = new();
    private long _lastId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatLog"/> class.
    /// </summary>
    /// <param name="capacity">The number of kept lines.</param>
    public ChatLog(int capacity = DefaultCapacity)
    {
        this.Capacity = capacity;
    }

    /// <summary>
    /// Gets the chat log of this process.
    /// </summary>
    public static ChatLog Shared { get; } = new();

    /// <summary>
    /// Gets or sets the number of kept lines; the oldest lines go first.
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Gets the id of the newest line; it changes with every new line.
    /// </summary>
    public long LastId => System.Threading.Interlocked.Read(ref this._lastId);

    /// <summary>
    /// Adds a line.
    /// </summary>
    /// <param name="timeUtc">When it was sent.</param>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="mapName">The name of the sender's map.</param>
    /// <param name="sender">The sender's character name.</param>
    /// <param name="accountId">The id of the sender's account.</param>
    /// <param name="kind">The kind of the message.</param>
    /// <param name="receiver">The receiver of a whisper.</param>
    /// <param name="text">The text.</param>
    /// <returns>The added line.</returns>
    public ChatLogEntry Add(DateTime timeUtc, int serverId, string mapName, string sender, Guid accountId, ChatMessageType kind, string? receiver, string text)
    {
        lock (this._lock)
        {
            var entry = new ChatLogEntry(++this._lastId, timeUtc, serverId, mapName, sender, accountId, kind, receiver, text);
            this._entries.Enqueue(entry);
            while (this._entries.Count > Math.Max(1, this.Capacity))
            {
                this._entries.Dequeue();
            }

            return entry;
        }
    }

    /// <summary>
    /// Gets the lines, newest first, optionally only the ones whose sender, receiver or text contain a search text.
    /// </summary>
    /// <param name="search">The search text; empty for all lines.</param>
    /// <param name="maximum">The maximum number of returned lines.</param>
    /// <returns>The lines, newest first.</returns>
    public IReadOnlyList<ChatLogEntry> Find(string? search, int maximum = int.MaxValue)
    {
        ChatLogEntry[] entries;
        lock (this._lock)
        {
            entries = this._entries.ToArray();
        }

        var term = search?.Trim();
        return entries.Reverse()
            .Where(e => string.IsNullOrEmpty(term)
                || e.Sender.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (e.Receiver?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || e.Text.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(maximum)
            .ToList();
    }

    /// <summary>
    /// Removes all lines.
    /// </summary>
    public void Clear()
    {
        lock (this._lock)
        {
            this._entries.Clear();
        }
    }
}
