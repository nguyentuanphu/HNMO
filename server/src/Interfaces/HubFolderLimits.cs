// <copyright file="HubFolderLimits.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// The HNMO developer's switch for the licence's folder limit, as the hub's last answer to the weekly report or an update
/// check gave it (<c>limits: { "enforce": true|false }</c>; missing: <c>false</c>). It is kept in the folder's shared state
/// file (<c>save/developer-hub.json</c>) for at most <see cref="KeepDays"/> days: older, the limit is off, so a folder
/// whose hub can't be reached is never locked wrongly. Off by default (owner, 2026-11-28: HNMO is in testing, anyone may
/// run it freely).
/// </summary>
public sealed class HubFolderLimits
{
    /// <summary>The days the hub's last answer counts.</summary>
    public const int KeepDays = 14;

    /// <summary>The folder's shared state file in its save folder (the developer hub's state).</summary>
    public const string StateFileName = "developer-hub.json";

    /// <summary>
    /// The name <see cref="StateFileName"/> keeps the limits under (the developer hub state's <c>FolderLimits</c>); here,
    /// so the launcher, which writes the same file, needs no reference to the game logic.
    /// </summary>
    public const string StatePropertyName = "FolderLimits";

    /// <summary>Gets or sets a value indicating whether the hub asked for the folder limit.</summary>
    public bool Enforce { get; set; }

    /// <summary>Gets or sets when the hub gave it (UTC).</summary>
    public DateTime ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets what runs between reading the state file and writing it in <see cref="Remember"/>; for tests (another
    /// writer at the worst moment).
    /// </summary>
    internal static Action? AfterReadForTests { get; set; }

    /// <summary>Whether an answer of the hub asks for the folder limit.</summary>
    /// <param name="answer">The answer; <c>null</c>: none.</param>
    /// <returns><c>true</c> only for <c>limits.enforce == true</c>.</returns>
    public static bool EnforceOf(JsonNode? answer)
        => answer is JsonObject root && root["limits"] is JsonObject limits && limits["enforce"] is JsonValue value
           && value.GetValueKind() == JsonValueKind.True;

    /// <summary>Whether an answer of the hub asks for the folder limit.</summary>
    /// <param name="answer">The answer's root.</param>
    /// <returns><c>true</c> only for <c>limits.enforce == true</c>.</returns>
    public static bool EnforceOf(JsonElement answer)
        => answer.ValueKind == JsonValueKind.Object
           && answer.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Object
           && limits.TryGetProperty("enforce", out var enforce) && enforce.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Whether the folder limit holds now: the hub asked for it in an answer of at most <see cref="KeepDays"/> days ago
    /// (an answer from the future, after the clock went back, counts as none).
    /// </summary>
    /// <param name="limits">The kept limits; <c>null</c>: none.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, if the limit holds.</returns>
    public static bool IsActive(HubFolderLimits? limits, DateTime nowUtc)
        => limits is { Enforce: true } kept
           && nowUtc - kept.ReceivedUtc <= TimeSpan.FromDays(KeepDays)
           && kept.ReceivedUtc - nowUtc <= TimeSpan.FromDays(1);

    /// <summary>
    /// Keeps the hub's answer in a folder's state file, for a program which doesn't hold the state itself (the launcher's
    /// update check). Changes nothing else in the file: it reads and writes it under the state file's lock
    /// (<see cref="SharedStateFile"/>), which the server takes too, so a change the server makes meanwhile is kept. A file
    /// that can't be read or written is left as it is.
    /// </summary>
    /// <param name="stateFile">The state file (<see cref="StateFileName"/> in the save folder).</param>
    /// <param name="enforce">Whether the hub asked for the limit.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, if it was kept.</returns>
    public static bool Remember(string stateFile, bool enforce, DateTime nowUtc)
    {
        try
        {
            using var stateLock = SharedStateFile.Lock(stateFile);
            JsonObject root;
            try
            {
                root = File.Exists(stateFile) ? JsonNode.Parse(File.ReadAllText(stateFile)) as JsonObject ?? [] : [];
            }
            catch (JsonException)
            {
                root = [];
            }

            AfterReadForTests?.Invoke();
            root[StatePropertyName] = new JsonObject
            {
                [nameof(Enforce)] = enforce,
                [nameof(ReceivedUtc)] = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc),
            };
            SharedStateFile.Replace(stateFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
