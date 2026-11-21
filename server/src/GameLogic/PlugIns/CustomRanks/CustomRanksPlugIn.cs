// <copyright file="CustomRanksPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Custom ranks and titles plug-in (OFF by default): evaluates tiers by level, master level,
/// resets, VIP tier, and completed achievements/missions, displaying active badges beside character names.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Custom Ranks & Titles", Description = "Awards custom titles by level, resets, VIP level, or achievements and shows them next to character names (OFF by default).")]
[Guid("B74F2D19-6A8C-4E05-9F31-1E8B4A7C2D53")]
public sealed class CustomRanksPlugIn :
    IPlayerStateChangedPlugIn,
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<CustomRankConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Dictionary<string, string> _lastResolved = new(StringComparer.OrdinalIgnoreCase);

    private (CustomRankConfiguration Config, CustomRankEngine Engine)? _engine;

    private DateTime _lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public CustomRankConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="CustomRanksPlugIn"/> for <paramref name="gameContext"/>, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static CustomRanksPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<CustomRanksPlugIn>().FirstOrDefault();

    /// <summary>Builds the character subject snapshot used to evaluate custom rank tiers.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The subject snapshot.</returns>
    public static CustomRankSubject BuildSubject(Player player)
    {
        var ch = player.SelectedCharacter;
        var name = ch?.Name ?? string.Empty;
        var level = player.Level;
        var masterLevel = (int)(player.Attributes?[Stats.MasterLevel] ?? 0f);
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0f);
        var fromAttr = (int)(player.Attributes?[Stats.IsVip] ?? 0f);
        var fromAcct = player.Account is { } acc
            ? (int)acc.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow)
            : 0;
        var vipLevel = Math.Max(fromAttr, fromAcct);

        var missionsClaimed = 0;
        if (ch is not null && MissionsPlugIn.Get(player.GameContext) is { } missions)
        {
            missionsClaimed = missions.GetEngine().GetAchievementCount(ch.Id.ToString("N"));
        }

        // The HNMO Arena's ranked rating, from the shared database (a copy refreshed every minute).
        var arenaRating = 0;
        if (player.Account?.LoginName is { Length: > 0 } login && Persistence.ArenaShared.Current is { } arena)
        {
            try
            {
                arenaRating = arena.GetRating(login) ?? 0;
            }
            catch (Exception ex)
            {
                player.Logger.LogDebug(ex, "Custom ranks: the arena rating couldn't be read.");
            }
        }

        return new CustomRankSubject(name, level, masterLevel, resets, vipLevel, missionsClaimed, arenaRating);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CustomRankConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._lastTickUtc = DateTime.MinValue;

    /// <summary>Gets or creates the engine paired with the active configuration.</summary>
    /// <returns>The custom rank engine.</returns>
    public CustomRankEngine GetEngine()
    {
        var cfg = this.Configuration ??= new CustomRankConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var store = CustomRankStore.Open(cfg.FileName);
        var engine = new CustomRankEngine(cfg, store);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            await this.BroadcastSnapshotAsync(player.GameContext).ConfigureAwait(false);
        }
        else if (previousState == PlayerState.EnteredWorld && currentState != PlayerState.EnteredWorld && player.SelectedCharacter is { } left)
        {
            lock (this._lastResolved)
            {
                this._lastResolved.Remove(left.Name);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var utcNow = DateTime.UtcNow;
        if (utcNow - this._lastTickUtc < TimeSpan.FromSeconds(15))
        {
            return;
        }

        this._lastTickUtc = utcNow;
        var cfg = this.Configuration ?? new CustomRankConfiguration();
        if (!cfg.Enabled)
        {
            return;
        }

        var engine = this.GetEngine();
        var changed = false;
        var online = await gameContext.GetPlayersAsync().ConfigureAwait(false);
        lock (this._lastResolved)
        {
            foreach (var p in online)
            {
                if (p.SelectedCharacter is null)
                {
                    continue;
                }

                var subject = BuildSubject(p);
                var activeId = engine.ResolveActiveTier(subject)?.Id ?? string.Empty;
                if (!this._lastResolved.TryGetValue(subject.CharacterName, out var prev)
                    || !string.Equals(prev, activeId, StringComparison.OrdinalIgnoreCase))
                {
                    this._lastResolved[subject.CharacterName] = activeId;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await this.BroadcastSnapshotAsync(gameContext).ConfigureAwait(false);
        }
    }

    /// <summary>Selects a rank tier (or <c>auto</c>) for <paramref name="player"/> and broadcasts updated state.</summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="rankId">Requested rank id or empty/<c>auto</c>.</param>
    /// <returns>Outcome tuple with localized messages.</returns>
    public async ValueTask<(bool Ok, string MessageEn, string MessageVi)> SelectRankAsync(Player player, string? rankId)
    {
        var cfg = this.Configuration ?? new CustomRankConfiguration();
        var engine = this.GetEngine();
        if (!cfg.Enabled)
        {
            return (false, "Custom ranks are currently disabled.", "Danh hiệu tùy chỉnh hiện đang tắt.");
        }

        var subject = BuildSubject(player);
        if (!engine.TrySelectRank(subject, rankId, out var errorEn, out var errorVi))
        {
            return (false, errorEn, errorVi);
        }

        var active = engine.ResolveActiveTier(subject);
        lock (this._lastResolved)
        {
            this._lastResolved[subject.CharacterName] = active?.Id ?? string.Empty;
        }

        await this.BroadcastSnapshotAsync(player.GameContext).ConfigureAwait(false);
        return active is null
            ? (true, "Active title cleared (no rank unlocked yet).", "Đã đặt lại danh hiệu tự động (chưa mở khóa cấp nào).")
            : (true, $"Active title set to {active.TitleEn}.", $"Đã chọn danh hiệu: {active.TitleVi}.");
    }

    /// <summary>Sends the custom rank snapshot directly to <paramref name="player"/>.</summary>
    /// <param name="player">Target player.</param>
    /// <param name="requested">Whether requested by a web packet.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested = true)
    {
        var online = await player.GameContext.GetPlayersAsync().ConfigureAwait(false);
        var json = this.BuildPayloadJson(player, online);
        await player.InvokeViewPlugInAsync<IHnmoCustomRanksViewPlugIn>(v => v.ShowCustomRanksStateAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Broadcasts updated active titles to all connected web clients that support the packet.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The task.</returns>
    public async ValueTask BroadcastSnapshotAsync(IGameContext gameContext)
    {
        var online = await gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var viewer in online)
        {
            if (viewer.SelectedCharacter is null)
            {
                continue;
            }

            var json = this.BuildPayloadJson(viewer, online);
            await viewer.InvokeViewPlugInAsync<IHnmoCustomRanksViewPlugIn>(v => v.ShowCustomRanksStateAsync(json, requested: false)).ConfigureAwait(false);
        }
    }

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u").Append(((int)c).ToString("x4"));
            }
        }

        return sb.ToString();
    }

    private string BuildPayloadJson(Player viewer, IList<Player> online)
    {
        var cfg = this.Configuration ?? new CustomRankConfiguration();
        var engine = this.GetEngine();
        var activePlayers = new List<object>();

        if (cfg.Enabled)
        {
            foreach (var p in online)
            {
                if (p.SelectedCharacter is null)
                {
                    continue;
                }

                var sub = BuildSubject(p);
                var tier = engine.ResolveActiveTier(sub);
                if (tier is null)
                {
                    continue;
                }

                activePlayers.Add(new
                {
                    name = sub.CharacterName,
                    rankId = tier.Id,
                    titleEn = tier.TitleEn,
                    titleVi = tier.TitleVi,
                    colorHex = tier.ColorHex,
                });
            }
        }

        object? me = null;
        if (viewer.SelectedCharacter is not null)
        {
            var mySub = BuildSubject(viewer);
            var store = CustomRankStore.Open(cfg.FileName);
            var record = store.GetCharacter(mySub.CharacterName);
            var unlocked = cfg.Enabled ? engine.GetUnlockedTiers(mySub) : [];
            var resolved = cfg.Enabled ? engine.ResolveActiveTier(mySub) : null;
            me = new
            {
                character = mySub.CharacterName,
                selectedRankId = record?.SelectedRankId ?? string.Empty,
                resolvedRankId = resolved?.Id ?? string.Empty,
                titleEn = resolved?.TitleEn ?? string.Empty,
                titleVi = resolved?.TitleVi ?? string.Empty,
                colorHex = resolved?.ColorHex ?? "#ffd166",
                unlockedIds = unlocked.Select(t => t.Id).ToList(),
                stats = new
                {
                    level = mySub.Level,
                    masterLevel = mySub.MasterLevel,
                    resets = mySub.Resets,
                    vipLevel = mySub.VipLevel,
                    missionsClaimed = mySub.MissionsClaimed,
                    achievementKeys = record?.AchievementKeys ?? [],
                },
            };
        }

        var tiers = (cfg.Tiers ?? []).Where(t => t.Enabled).OrderByDescending(t => t.Priority).Select(t => new
        {
            id = t.Id,
            priority = t.Priority,
            titleEn = t.TitleEn,
            titleVi = t.TitleVi,
            colorHex = t.ColorHex,
            minLevel = t.MinLevel,
            minMasterLevel = t.MinMasterLevel,
            minResets = t.MinResets,
            minVipLevel = t.MinVipLevel,
            minMissionsClaimed = t.MinMissionsClaimed,
            requiredAchievementKey = t.RequiredAchievementKey,
        }).ToList();

        var payload = new
        {
            v = 1,
            enabled = cfg.Enabled,
            showByNameplate = cfg.ShowByNameplate,
            showInChat = cfg.ShowInChat,
            me,
            tiers,
            players = activePlayers,
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
