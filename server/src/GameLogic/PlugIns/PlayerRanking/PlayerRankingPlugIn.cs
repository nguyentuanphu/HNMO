// <copyright file="PlayerRankingPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PlayerRanking;

using System.Runtime.InteropServices;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Character;
using HNMO.GameLogic.Views;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// In-game Player Ranking panel (<c>Shift+R</c> / <c>/rank</c>) and bulk Add Stats window (<c>Shift+A</c>) plug-in.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Player Ranking & Add Stats", Description = "Provides the in-game Player Ranking panel (Top Characters, Online, Guilds, Events) and bulk Add Stats distribution window.")]
[Guid("8B5C1F42-9E04-4A7D-B621-3C8F9D0E2A45")]
public sealed class PlayerRankingPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<PlayerRankingConfiguration>,
    ISupportDefaultCustomConfiguration
{
    private readonly IncreaseStatsAction _increaseStatsAction = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private CachedSnapshot? _cached;

    /// <inheritdoc />
    public PlayerRankingConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active <see cref="PlayerRankingPlugIn"/> from <paramref name="gameContext"/>, or <c>null</c> when disabled.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in instance, or <c>null</c>.</returns>
    public static PlayerRankingPlugIn? Get(IGameContext gameContext) =>
        gameContext.FeaturePlugIns.GetPlugIn<PlayerRankingPlugIn>();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new PlayerRankingConfiguration();

    /// <summary>
    /// Sends the current ranking and add-stats state to <paramref name="player"/>.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="requested">Whether the request originated from a web client packet.</param>
    public async ValueTask SendStateAsync(Player player, bool requested = true)
    {
        var cfg = this.Configuration ?? new PlayerRankingConfiguration();
        var topCount = Math.Clamp(cfg.TopCount, 5, 100);
        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

        if (!cfg.EnableRankings && !cfg.EnableBulkAddStats)
        {
            await player.ShowBlueMessageAsync(isVi
                ? "Bảng xếp hạng và Cộng điểm nhanh hiện đang tắt bởi quản trị viên."
                : "Rankings and bulk Add Stats are currently disabled by the administrator.").ConfigureAwait(false);
            return;
        }

        var snapshot = cfg.EnableRankings
            ? await this.GetOrRefreshSnapshotAsync(player.GameContext, cfg, topCount).ConfigureAwait(false)
            : new CachedSnapshot(DateTimeOffset.UtcNow, [], [], []);

        var onlinePlayers = await player.GameContext.GetPlayersAsync().ConfigureAwait(false);
        var onlineNames = new HashSet<string>(
            onlinePlayers.Where(p => p.SelectedCharacter is not null).Select(p => p.SelectedCharacter!.Name),
            StringComparer.OrdinalIgnoreCase);

        var characters = snapshot.Characters.Select((c, index) => new
        {
            rank = index + 1,
            name = c.Name,
            className = c.ClassName,
            level = c.Level,
            masterLevel = c.MasterLevel,
            resets = c.Resets,
            online = cfg.ShowOnlineStatus && onlineNames.Contains(c.Name),
        }).ToList();

        var online = cfg.ShowOnlineLeaderboard
            ? PlayerRankingEngine.BuildOnlineRanking(onlinePlayers, topCount)
            : [];

        var myName = player.SelectedCharacter?.Name ?? string.Empty;
        var myFound = characters.FirstOrDefault(c => c.name.Equals(myName, StringComparison.OrdinalIgnoreCase));
        var myRank = new
        {
            name = myName,
            rank = myFound?.rank ?? 0,
            level = (int)(player.Attributes?[Stats.Level] ?? player.Level),
            masterLevel = (int)(player.Attributes?[Stats.MasterLevel] ?? 0),
            resets = (int)(player.Attributes?[Stats.Resets] ?? 0),
            levelUpPoints = player.SelectedCharacter?.LevelUpPoints ?? 0,
        };

        var payload = new
        {
            enableRankings = cfg.EnableRankings,
            enableBulkAddStats = cfg.EnableBulkAddStats,
            showOnline = cfg.ShowOnlineLeaderboard,
            showGuilds = cfg.ShowGuildRankings,
            showEvents = cfg.ShowEventRankings,
            topCount,
            updatedAtUtc = snapshot.UpdatedAtUtc.ToString("O"),
            myRank,
            characters,
            online,
            guilds = snapshot.Guilds,
            events = snapshot.Events,
        };

        await player.InvokeViewPlugInAsync<IHnmoRankingViewPlugIn>(
            p => p.ShowRankingStateAsync(PlayerRankingEngine.SerializeAscii(payload), requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates and distributes multiple stat points from the Add Stats window in a single request.
    /// </summary>
    /// <param name="player">The player distributing stat points.</param>
    /// <param name="payload">JSON allocation payload (<c>str</c>, <c>agi</c>, <c>vit</c>, <c>ene</c>, <c>cmd</c>).</param>
    public async ValueTask HandleBulkAddStatsAsync(Player player, string payload)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        var cfg = this.Configuration ?? new PlayerRankingConfiguration();
        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

        if (!cfg.EnableBulkAddStats)
        {
            await player.ShowBlueMessageAsync(isVi
                ? "Cửa sổ cộng điểm nhanh hiện đang tắt."
                : "Bulk Add Stats is currently disabled.").ConfigureAwait(false);
            return;
        }

        if (player.CurrentMiniGame is not null)
        {
            await player.ShowLocalizedBlueMessageAsync(PlayerMessage.AddingMultiplePointsWhileMiniGameNotAllowed).ConfigureAwait(false);
            return;
        }

        var allowsCommand = character.CharacterClass?.GetStatAttribute(Stats.BaseLeadership)?.IncreasableByPlayer == true;
        var plan = PlayerRankingEngine.PlanBulkAddStats(payload, character.LevelUpPoints, allowsCommand);
        if (!plan.Valid)
        {
            var errMsg = plan.ErrorCode switch
            {
                "insufficient_points" => isVi
                    ? "Số điểm cộng vượt quá số điểm tiềm năng hiện có."
                    : "Requested points exceed your available Level-Up Points.",
                "no_command" => isVi
                    ? "Nhân vật của bạn không có chỉ số Mệnh Lệnh (Command)."
                    : "Your character class cannot add points to Command.",
                "negative" => isVi
                    ? "Số điểm cộng không được là số âm."
                    : "Stat point amounts cannot be negative.",
                _ => isVi
                    ? "Vui lòng nhập số điểm hợp lệ để cộng."
                    : "Please enter valid stat points to allocate.",
            };
            await player.ShowBlueMessageAsync(errMsg).ConfigureAwait(false);
            return;
        }

        var allocations = new (AttributeDefinition Attr, int Amount, string LabelEn, string LabelVi)[]
        {
            (Stats.BaseStrength, plan.Strength, "STR", "Sức Mạnh"),
            (Stats.BaseAgility, plan.Agility, "AGI", "Nhanh Nhẹn"),
            (Stats.BaseVitality, plan.Vitality, "VIT", "Thể Lực"),
            (Stats.BaseEnergy, plan.Energy, "ENE", "Năng Lượng"),
            (Stats.BaseLeadership, plan.Command, "CMD", "Mệnh Lệnh"),
        };

        var appliedParts = new List<string>();
        foreach (var (attr, amount, labelEn, labelVi) in allocations)
        {
            if (amount <= 0)
            {
                continue;
            }

            var remaining = amount;
            while (remaining > 0 && character.LevelUpPoints > 0)
            {
                var before = (int)(player.Attributes?[attr] ?? 0);
                var chunk = (ushort)Math.Min(remaining, ushort.MaxValue);
                await this._increaseStatsAction.IncreaseStatsAsync(player, attr, chunk).ConfigureAwait(false);
                var added = Math.Max(0, (int)(player.Attributes?[attr] ?? 0) - before);
                if (added == 0)
                {
                    break;
                }

                remaining -= added;
            }

            var actualAdded = amount - remaining;
            if (actualAdded > 0)
            {
                appliedParts.Add($"+{actualAdded:N0} {(isVi ? labelVi : labelEn)}");
            }
        }

        if (appliedParts.Count > 0)
        {
            var summary = string.Join(", ", appliedParts);
            await player.ShowBlueMessageAsync(isVi
                ? $"Đã cộng điểm thành công: {summary}. Còn lại: {character.LevelUpPoints:N0} điểm."
                : $"Added stats: {summary}. Remaining points: {character.LevelUpPoints:N0}.").ConfigureAwait(false);
        }

        await this.SendStateAsync(player, requested: true).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<GuildEntry>> LoadGuildRankingsAsync(IGameContext gameContext, int topCount)
    {
        try
        {
            using var guildCtx = gameContext.PersistenceContextProvider.CreateNewGuildContext();
            var all = await guildCtx.GetAsync<Guild>().ConfigureAwait(false);
            return all
                .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                .OrderByDescending(g => g.Score)
                .ThenByDescending(g => g.Members?.Count ?? 0)
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .Take(topCount)
                .Select((g, idx) => new GuildEntry(idx + 1, g.Name!, g.Score, g.Members?.Count ?? 0, g.Hostility?.Name ?? string.Empty))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static async ValueTask<IReadOnlyList<EventBoardEntry>> LoadEventRankingsAsync(IGameContext gameContext, IPlayerContext context, int perEventCount)
    {
        var boards = new List<EventBoardEntry>();
        try
        {
            var classNames = gameContext.Configuration.CharacterClasses
                .ToDictionary(c => c.GetId(), c => c.Name.ToString() ?? string.Empty);

            var groupedDefs = gameContext.Configuration.MiniGameDefinitions
                .Where(d => d.SaveRankingStatistics)
                .GroupBy(d => d.Type)
                .OrderBy(g => g.Key);

            foreach (var group in groupedDefs)
            {
                var ids = group.Select(d => d.GetId()).ToList();
                if (ids.Count == 0)
                {
                    continue;
                }

                var rows = await context.GetMiniGameLeaderboardAsync(ids, null, perEventCount).ConfigureAwait(false);
                var entries = rows
                    .Select((r, idx) => new EventPlayerEntry(
                        idx + 1,
                        r.CharacterName,
                        r.CharacterClassId is { } cid && classNames.TryGetValue(cid, out var cls) ? cls : string.Empty,
                        r.TotalScore,
                        r.GameCount))
                    .ToList();
                boards.Add(new EventBoardEntry(group.Key.ToString(), entries));
            }
        }
        catch
        {
            // Keep characters/guilds available even if mini-game leaderboard query isn't populated.
        }

        return boards;
    }

    private async Task<CachedSnapshot> GetOrRefreshSnapshotAsync(IGameContext gameContext, PlayerRankingConfiguration cfg, int topCount)
    {
        var cacheSpan = TimeSpan.FromSeconds(Math.Clamp(cfg.CacheSeconds, 0, 300));
        var now = DateTimeOffset.UtcNow;
        if (this._cached is { } existing && cacheSpan > TimeSpan.Zero && now - existing.UpdatedAtUtc < cacheSpan)
        {
            return existing;
        }

        await this._cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this._cached is { } fresh && cacheSpan > TimeSpan.Zero && DateTimeOffset.UtcNow - fresh.UpdatedAtUtc < cacheSpan)
            {
                return fresh;
            }

            using var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
            var charRows = await context.GetCharacterRankingAsync(Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, topCount).ConfigureAwait(false);
            var characters = charRows
                .Select(c => new CharacterEntry(c.Name, c.CharacterClassName, c.Level, c.MasterLevel, c.Resets))
                .ToList();

            var guilds = cfg.ShowGuildRankings
                ? await LoadGuildRankingsAsync(gameContext, topCount).ConfigureAwait(false)
                : [];

            var events = cfg.ShowEventRankings
                ? await LoadEventRankingsAsync(gameContext, context, Math.Min(10, topCount)).ConfigureAwait(false)
                : [];

            var snapshot = new CachedSnapshot(DateTimeOffset.UtcNow, characters, guilds, events);
            this._cached = snapshot;
            return snapshot;
        }
        finally
        {
            this._cacheLock.Release();
        }
    }

    private sealed record CachedSnapshot(
        DateTimeOffset UpdatedAtUtc,
        IReadOnlyList<CharacterEntry> Characters,
        IReadOnlyList<GuildEntry> Guilds,
        IReadOnlyList<EventBoardEntry> Events);

    private sealed record CharacterEntry(string Name, string ClassName, int Level, int MasterLevel, int Resets);

    private sealed record GuildEntry(int Rank, string Name, int Score, int MemberCount, string Alliance);

    private sealed record EventBoardEntry(string EventType, IReadOnlyList<EventPlayerEntry> Entries);

    private sealed record EventPlayerEntry(int Rank, string Name, string ClassName, long TotalScore, int GameCount);
}
