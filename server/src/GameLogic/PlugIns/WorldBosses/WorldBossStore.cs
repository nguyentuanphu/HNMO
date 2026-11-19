// <copyright file="WorldBossStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// The damage of one player (or of a party) to a world boss.
/// </summary>
public sealed class WorldBossDamageEntry
{
    /// <summary>Gets or sets the character id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the party (the party master's name), if the player was in one.</summary>
    public string? Party { get; set; }

    /// <summary>Gets or sets the damage.</summary>
    public long Damage { get; set; }
}

/// <summary>
/// A world boss which is on a map now.
/// </summary>
public sealed class WorldBossAliveState
{
    /// <summary>Gets or sets the boss id.</summary>
    public string BossId { get; set; } = string.Empty;

    /// <summary>Gets or sets the game server which hosts it.</summary>
    public byte ServerId { get; set; }

    /// <summary>Gets or sets the identifier of this appearance (also of the kill).</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the x coordinate where it appeared.</summary>
    public byte X { get; set; }

    /// <summary>Gets or sets the y coordinate where it appeared.</summary>
    public byte Y { get; set; }

    /// <summary>Gets or sets when it appeared.</summary>
    public DateTime SpawnedAtUtc { get; set; }

    /// <summary>Gets or sets when it leaves.</summary>
    public DateTime DespawnAtUtc { get; set; }

    /// <summary>Gets or sets its maximum health.</summary>
    public long MaximumHealth { get; set; }

    /// <summary>Gets or sets the damage by character id.</summary>
    public Dictionary<string, WorldBossDamageEntry> Damage { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The schedule state of a world boss.
/// </summary>
public sealed class WorldBossState
{
    /// <summary>Gets or sets the next spawn time.</summary>
    public DateTime? NextSpawnUtc { get; set; }

    /// <summary>Gets or sets the spawn time which was announced before.</summary>
    public DateTime? AnnouncedUtc { get; set; }

    /// <summary>Gets or sets the schedule (as text) <see cref="NextSpawnUtc"/> was computed from, so an edit recomputes it.</summary>
    public string? ScheduleKey { get; set; }
}

/// <summary>
/// A line of the damage table of a kill.
/// </summary>
public sealed class WorldBossKillLine
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the party key, if any.</summary>
    public string? Party { get; set; }

    /// <summary>Gets or sets the damage.</summary>
    public long Damage { get; set; }

    /// <summary>Gets or sets the share of all damage, in percent.</summary>
    public double Share { get; set; }

    /// <summary>Gets or sets the boss-hunt points.</summary>
    public int Points { get; set; }

    /// <summary>Gets or sets the reward tiers, e.g. "top1", "participation", "lasthit".</summary>
    public List<string> Tiers { get; set; } = new();
}

/// <summary>
/// A kill (or an escape) of a world boss.
/// </summary>
public sealed class WorldBossKillRecord
{
    /// <summary>Gets or sets the identifier of the appearance.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the boss id.</summary>
    public string BossId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the game server.</summary>
    public byte ServerId { get; set; }

    /// <summary>Gets or sets when it appeared.</summary>
    public DateTime SpawnedAtUtc { get; set; }

    /// <summary>Gets or sets when it was killed or left.</summary>
    public DateTime EndedAtUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether it was killed (otherwise it left).</summary>
    public bool Killed { get; set; }

    /// <summary>Gets or sets the name of the character with the last hit.</summary>
    public string? LastHit { get; set; }

    /// <summary>Gets or sets all damage dealt.</summary>
    public long TotalDamage { get; set; }

    /// <summary>Gets or sets the damage table, best first.</summary>
    public List<WorldBossKillLine> Lines { get; set; } = new();
}

/// <summary>
/// A reward which waits to be given: its zen and items need the character in the game with room in the bag; its
/// coins are given at once (to the account).
/// </summary>
public sealed class WorldBossOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason (for the coins history and the logs).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name of the boss.</summary>
    public string BossEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name of the boss.</summary>
    public string BossVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the tier, e.g. "top1".</summary>
    public string Tier { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward.</summary>
    public MissionReward Reward { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the coins were given.</summary>
    public bool CoinsGiven { get; set; }

    /// <summary>Gets or sets when it was earned.</summary>
    public DateTime EarnedAtUtc { get; set; }
}

/// <summary>
/// The points of a character in a weekly ranking.
/// </summary>
public sealed class WeeklyRankingEntry
{
    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the points.</summary>
    public long Points { get; set; }

    /// <summary>Gets or sets when the points were last raised (an earlier time wins a tie).</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// A winner of a weekly ranking.
/// </summary>
public sealed class WeeklyWinner
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the character id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account (not shown on the website).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the points.</summary>
    public long Points { get; set; }

    /// <summary>Gets or sets the coins it paid.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }
}

/// <summary>
/// The results of a finished week.
/// </summary>
public sealed class WeeklyResult
{
    /// <summary>Gets or sets the week (its Monday).</summary>
    public string Week { get; set; } = string.Empty;

    /// <summary>Gets or sets the winners by category.</summary>
    public Dictionary<WeeklyCategory, List<WeeklyWinner>> Winners { get; set; } = new();

    /// <summary>Gets or sets the guild which won the castle siege that week, if any.</summary>
    public string? CastleSiegeGuild { get; set; }

    /// <summary>Gets or sets the persistent id of that guild.</summary>
    public Guid? CastleSiegeGuildId { get; set; }

    /// <summary>Gets or sets the members of that guild who were paid, with their coins.</summary>
    public List<WeeklyWinner> CastleSiegeMembers { get; set; } = new();
}

/// <summary>
/// An announcement of a world boss (shown on the website).
/// </summary>
public sealed class WorldBossAnnouncement
{
    /// <summary>Gets or sets when it was made.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the boss id.</summary>
    public string BossId { get; set; } = string.Empty;

    /// <summary>Gets or sets the phase: "soon", "spawn", "kill" or "escape".</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Gets or sets the English text.</summary>
    public string TextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text.</summary>
    public string TextVi { get; set; } = string.Empty;
}

/// <summary>
/// The saved state of the world bosses and the weekly rankings, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class WorldBossStoreData
{
    /// <summary>Gets or sets the schedule state by boss id.</summary>
    public Dictionary<string, WorldBossState> Bosses { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the bosses which are on a map now.</summary>
    public List<WorldBossAliveState> Alive { get; set; } = new();

    /// <summary>Gets or sets the recent kills and escapes, newest first.</summary>
    public List<WorldBossKillRecord> Kills { get; set; } = new();

    /// <summary>Gets or sets the recent announcements, newest first.</summary>
    public List<WorldBossAnnouncement> Announcements { get; set; } = new();

    /// <summary>Gets or sets the rewards which wait to be given, by character id.</summary>
    public Dictionary<string, List<WorldBossOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the current week (its Monday).</summary>
    public string WeekKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the points of the current week: by category, then by character id.</summary>
    public Dictionary<WeeklyCategory, Dictionary<string, WeeklyRankingEntry>> Week { get; set; } = new();

    /// <summary>Gets or sets the persistent id of the guild which won the castle siege in the current week.</summary>
    public Guid? CastleSiegeGuildId { get; set; }

    /// <summary>Gets or sets the name of that guild.</summary>
    public string? CastleSiegeGuildName { get; set; }

    /// <summary>Gets or sets the result of the last finished week.</summary>
    public WeeklyResult? LastWeek { get; set; }

    /// <summary>Gets or sets the finished weeks which wait to be paid.</summary>
    public List<WeeklyResult> PendingWeeks { get; set; } = new();

    /// <summary>Gets or sets the weeks which were paid.</summary>
    public List<string> PaidWeeks { get; set; } = new();
}

/// <summary>
/// Keeps the <see cref="WorldBossStoreData"/>; with a file, it outlasts a restart of the server. All game servers and
/// the admin panel of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class WorldBossStore
{
    private const string FeatureOwed = "worldBosses.owed";
    private const string SectionKey = "worldBosses";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, WorldBossStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<List<WorldBossOwedReward>> _owedSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldBossStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public WorldBossStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the file, if any.</summary>
    public string? FilePath => this._stateStore?.FilePath;

    /// <summary>Gets the data.</summary>
    public WorldBossStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static WorldBossStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new WorldBossStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the data as changed.</summary>
    public void Changed() => this._changed = true;

    /// <summary>
    /// Saves the data when it changed. The caller holds <see cref="Lock"/>.
    /// </summary>
    public void SaveIfChanged()
    {
        if (!this._changed || this._stateStore is null)
        {
            this._changed = false;
            return;
        }

        this._owedSync.SaveChanges(this._rowBackend, FeatureOwed, this.Data.Owed, JsonOptions, useDirtySetOnly: true);

        try
        {
            this._stateStore.WriteSection(SectionKey, this.BuildServerOnlyData(this.Data), JsonOptions);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private WorldBossStoreData BuildServerOnlyData(WorldBossStoreData src) => new()
    {
        Bosses = src.Bosses,
        Alive = src.Alive,
        Kills = src.Kills,
        Announcements = src.Announcements,
        WeekKey = src.WeekKey,
        Week = src.Week,
        CastleSiegeGuildId = src.CastleSiegeGuildId,
        CastleSiegeGuildName = src.CastleSiegeGuildName,
        LastWeek = src.LastWeek,
        PendingWeeks = src.PendingWeeks,
        PaidWeeks = src.PaidWeeks,
    };

    private WorldBossStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new WorldBossStoreData();
        }

        var data = this._stateStore.ReadSection<WorldBossStoreData>(SectionKey, JsonOptions) ?? new WorldBossStoreData();
        data.Bosses = new Dictionary<string, WorldBossState>(data.Bosses, StringComparer.Ordinal);
        data.Owed = new Dictionary<string, List<WorldBossOwedReward>>(data.Owed, StringComparer.Ordinal);
        foreach (var alive in data.Alive)
        {
            alive.Damage = new Dictionary<string, WorldBossDamageEntry>(alive.Damage, StringComparer.Ordinal);
        }

        if (this._owedSync.LoadOrMigrate(this._rowBackend, FeatureOwed, data.Owed, JsonOptions))
        {
            this._stateStore.WriteSection(SectionKey, this.BuildServerOnlyData(data), JsonOptions);
        }

        return data;
    }
}
