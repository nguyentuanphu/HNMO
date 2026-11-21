// <copyright file="PartyOptionsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Party;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Party Options plug-in (OFF by default): adds <c>/partypass</c> password party join without invites
/// and Auto-Party hunting spot listings inside the existing <kbd>P</kbd> Party window (<c>F5 44</c>/<c>F5 45</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Party Options", Description = "Adds /partypass password join and Auto-Party hunting spot finder to the P Party window (OFF by default).")]
[Guid("8F5A1C32-6D04-4B9E-8A11-9D2C4E7F108A")]
public sealed class PartyOptionsPlugIn :
    IPlayerStateChangedPlugIn,
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<PartyOptionsConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private readonly ConcurrentDictionary<string, IGameContext> _leaderContexts = new(StringComparer.OrdinalIgnoreCase);
    private (PartyOptionsConfiguration Config, PartyOptionsEngine Engine)? _engine;
    private DateTime _lastPruneUtc = DateTime.MinValue;

    /// <inheritdoc />
    public PartyOptionsConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="PartyOptionsPlugIn"/> for <paramref name="gameContext"/>, or <c>null</c>.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static PartyOptionsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<PartyOptionsPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new PartyOptionsConfiguration();

    /// <summary>Gets or creates the engine.</summary>
    /// <returns>Engine.</returns>
    public PartyOptionsEngine GetEngine()
    {
        var cfg = this.Configuration ?? new PartyOptionsConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new PartyOptionsEngine(cfg, PartyOptionsStore.Open(cfg.FileName), enabled: this.Configuration is not null);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public void ForceStart() => this._lastPruneUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this.Configuration is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - this._lastPruneUtc < TimeSpan.FromSeconds(20))
        {
            return;
        }

        this._lastPruneUtc = now;
        await this.RefreshAdsFromLiveLeadersAsync(gameContext).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration is null)
        {
            return ValueTask.CompletedTask;
        }

        if (currentState == PlayerState.Finished && player.SelectedCharacter is { Name: { Length: > 0 } name })
        {
            this._leaderContexts.TryRemove(name, out _);
            this.GetEngine().RemoveAdvertisement(name);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Sends the Party Options panel state to <paramref name="player"/>.</summary>
    /// <param name="player">Player.</param>
    /// <param name="requested">Whether requested by UI.</param>
    /// <param name="notice">Optional feedback notice.</param>
    /// <returns>A task.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        (string Status, string MessageEn, string MessageVi)? notice = null)
    {
        var engine = this.GetEngine();
        var json = PartyOptionsStateWriter.WriteForPlayer(engine, player, notice);
        await player.InvokeViewPlugInAsync<IHnmoPartyOptionsViewPlugIn>(p => p.ShowPartyOptionsAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Handles <c>/partypass PASS</c> or <c>/partypass clear</c>.</summary>
    /// <param name="player">Player.</param>
    /// <param name="argument">Password or <c>clear</c>.</param>
    /// <returns>A task.</returns>
    public async ValueTask HandlePartyPassCommandAsync(Player player, string argument)
    {
        if (this.Configuration is null || player.SelectedCharacter is not { Name: { Length: > 0 } name })
        {
            return;
        }

        var engine = this.GetEngine();
        var arg = argument?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(arg))
        {
            await player.ShowBlueMessageAsync(IsVietnamese(player)
                ? "Cách dùng: /partypass MATKHAU (đặt mật khẩu nhóm hoặc tự vào nhóm có mật khẩu đó) · /partypass clear"
                : "Usage: /partypass PASSWORD (set your party password or join a matching party) · /partypass clear").ConfigureAwait(false);
            return;
        }

        if (arg.Equals("clear", StringComparison.OrdinalIgnoreCase) || arg.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            await this.StopAdvertisingAsync(player).ConfigureAwait(false);
            return;
        }

        // First try joining another open party matching this password if player is not already in a party.
        if (player.Party is null)
        {
            var level = (int)(player.Attributes?[Stats.Level] ?? 1);
            var findRes = engine.FindByPassword(arg, level, hasParty: false, out var match);
            if (findRes == PartyJoinCheckResult.Ok && match is not null)
            {
                await this.JoinLeaderPartyAsync(player, match.LeaderName, arg).ConfigureAwait(false);
                return;
            }
        }

        // Otherwise register/update this player's party password if solo or party leader.
        if (player.Party is not null && player.Party.PartyMaster?.Name != name)
        {
            await player.ShowBlueMessageAsync(IsVietnamese(player)
                ? "Chỉ trưởng nhóm mới có thể đặt mật khẩu cho nhóm."
                : "Only the party leader can set the party password.").ConfigureAwait(false);
            return;
        }

        var spot = player.CurrentMap?.Definition.Name.ValueInNeutralLanguage ?? "Hunting spot";
        await this.AdvertiseAsync(player, spot, arg, minLevel: 1, maxLevel: 0).ConfigureAwait(false);
    }

    /// <summary>Sets or updates an advertisement for <paramref name="player"/>'s party/spot.</summary>
    /// <param name="player">Player.</param>
    /// <param name="spot">Spot label.</param>
    /// <param name="password">Optional password.</param>
    /// <param name="minLevel">Minimum level.</param>
    /// <param name="maxLevel">Maximum level (0 = no cap).</param>
    /// <returns>A task.</returns>
    public async ValueTask AdvertiseAsync(Player player, string spot, string password, int minLevel, int maxLevel)
    {
        if (player.SelectedCharacter is not { Name: { Length: > 0 } name })
        {
            return;
        }

        if (player.Party is not null && player.Party.PartyMaster?.Name != name)
        {
            var en = "Only the party leader can advertise hunting spots or passwords.";
            var vi = "Chỉ trưởng nhóm mới có thể đăng tin tìm thành viên hoặc đặt mật khẩu.";
            await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        var engine = this.GetEngine();
        var mapName = player.CurrentMap?.Definition.Name.ValueInNeutralLanguage ?? "Lorencia";

        // Shown to every player who opens the party list: no markup or control characters, whatever the client does with it.
        var cleanSpot = new string((spot ?? string.Empty).Where(c => !char.IsControl(c) && c is not ('<' or '>' or '&' or '"' or '\'' or '`')).ToArray()).Trim();
        var spotName = cleanSpot.Length == 0 ? mapName : cleanSpot;
        var cleanPass = password?.Trim() ?? string.Empty;
        var ad = new PartyAdvertisement
        {
            LeaderName = name,
            MapName = mapName,
            SpotName = spotName.Length > 48 ? spotName[..48] : spotName,
            Password = cleanPass,
            MinLevel = Math.Max(1, minLevel),
            MaxLevel = Math.Max(0, maxLevel),
            MemberCount = player.Party?.PartyList.Count ?? 1,
            MaxSize = player.Party?.MaxPartySize ?? player.GameContext.Configuration.MaximumPartySize,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        this._leaderContexts[name] = player.GameContext;
        engine.SetAdvertisement(ad);

        var msgEn = string.IsNullOrEmpty(ad.Password)
            ? $"Advertising auto-party at {ad.SpotName} ({ad.MemberCount}/{ad.MaxSize})."
            : $"Party password set ({ad.SpotName}). Friends can type /partypass {ad.Password} to join.";
        var msgVi = string.IsNullOrEmpty(ad.Password)
            ? $"Đã đăng tìm nhóm tại {ad.SpotName} ({ad.MemberCount}/{ad.MaxSize})."
            : $"Đã đặt mật khẩu nhóm ({ad.SpotName}). Bạn bè gõ /partypass {ad.Password} để vào ngay.";

        await player.ShowBlueMessageAsync(IsVietnamese(player) ? msgVi : msgEn).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, ("ok", msgEn, msgVi)).ConfigureAwait(false);
    }

    /// <summary>Stops advertising <paramref name="player"/>'s spot/password.</summary>
    /// <param name="player">Player.</param>
    /// <returns>A task.</returns>
    public async ValueTask StopAdvertisingAsync(Player player)
    {
        if (player.SelectedCharacter is not { Name: { Length: > 0 } name })
        {
            return;
        }

        this._leaderContexts.TryRemove(name, out _);
        this.GetEngine().RemoveAdvertisement(name);
        var msgEn = "Stopped party spot / password advertisement.";
        var msgVi = "Đã tắt quảng bá bãi săn & mật khẩu nhóm.";
        await player.ShowBlueMessageAsync(IsVietnamese(player) ? msgVi : msgEn).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, ("ok", msgEn, msgVi)).ConfigureAwait(false);
    }

    /// <summary>Auto-joins an open spot party or passworded party without requiring an invite.</summary>
    /// <param name="joiner">Joining player.</param>
    /// <param name="leaderName">Target leader name.</param>
    /// <param name="password">Supplied password if any.</param>
    /// <returns>A task.</returns>
    public async ValueTask JoinLeaderPartyAsync(Player joiner, string leaderName, string? password = null)
    {
        if (joiner.SelectedCharacter is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var level = (int)(joiner.Attributes?[Stats.Level] ?? 1);
        var check = engine.PlanSpotJoin(leaderName, password, level, joiner.Party is not null, out var ad);
        if (check != PartyJoinCheckResult.Ok || ad is null)
        {
            var (en, vi) = DescribeFailure(check);
            await joiner.ShowBlueMessageAsync(IsVietnamese(joiner) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(joiner, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        var leader = await this.FindOnlineLeaderAsync(joiner.GameContext, ad.LeaderName).ConfigureAwait(false);
        if (leader is null || leader == joiner)
        {
            engine.RemoveAdvertisement(ad.LeaderName);
            var (en, vi) = DescribeFailure(PartyJoinCheckResult.NotFound);
            await joiner.ShowBlueMessageAsync(IsVietnamese(joiner) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(joiner, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        if (joiner.CurrentMiniGame?.Definition.AllowParty is false
            || await PartyRequestAction.IsPartyDeniedAsync(leader, joiner).ConfigureAwait(false))
        {
            var en = "Cannot join this party due to event, duel, or Guild/Gens rules.";
            var vi = "Không thể vào nhóm lúc này do giới hạn sự kiện, đối địch hoặc Gens.";
            await joiner.ShowBlueMessageAsync(IsVietnamese(joiner) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(joiner, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        if (leader.Party is { } existingParty)
        {
            if (!await existingParty.AddAsync(joiner).ConfigureAwait(false))
            {
                var (en, vi) = DescribeFailure(PartyJoinCheckResult.PartyFull);
                await joiner.ShowBlueMessageAsync(IsVietnamese(joiner) ? vi : en).ConfigureAwait(false);
                await this.SendStateAsync(joiner, requested: true, ("error", en, vi)).ConfigureAwait(false);
                return;
            }
        }
        else
        {
            var party = joiner.GameContext.PartyManager.CreateParty();
            await party.AddAsync(leader).ConfigureAwait(false);
            await party.AddAsync(joiner).ConfigureAwait(false);
        }

        ad.MemberCount = leader.Party?.PartyList.Count ?? ad.MemberCount + 1;
        ad.UpdatedAtUtc = DateTime.UtcNow;
        engine.SetAdvertisement(ad);

        await leader.ShowBlueMessageAsync(IsVietnamese(leader)
            ? $"{joiner.Name} đã tự động gia nhập nhóm của bạn ({ad.SpotName})."
            : $"{joiner.Name} joined your party automatically ({ad.SpotName}).").ConfigureAwait(false);

        var okEn = $"Joined {leader.Name}'s party ({ad.SpotName}).";
        var okVi = $"Đã vào nhóm của {leader.Name} ({ad.SpotName}).";
        await joiner.ShowBlueMessageAsync(IsVietnamese(joiner) ? okVi : okEn).ConfigureAwait(false);
        await this.SendStateAsync(joiner, requested: true, ("ok", okEn, okVi)).ConfigureAwait(false);
    }

    private static (string En, string Vi) DescribeFailure(PartyJoinCheckResult result) => result switch
    {
        PartyJoinCheckResult.Disabled => ("Party Options are currently disabled.", "Tính năng Tổ đội nhanh đang tắt."),
        PartyJoinCheckResult.AlreadyInParty => ("Leave your current party before joining another.", "Hãy rời nhóm hiện tại trước khi gia nhập nhóm mới."),
        PartyJoinCheckResult.NotFound => ("No matching open party found.", "Không tìm thấy nhóm phù hợp."),
        PartyJoinCheckResult.WrongPassword => ("Incorrect party password.", "Mật khẩu nhóm không đúng."),
        PartyJoinCheckResult.PartyFull => ("That party is already full.", "Nhóm đã đầy."),
        PartyJoinCheckResult.LevelOutOfRange => ("Your character level does not match this party's level filter.", "Cấp nhân vật không phù hợp điều kiện nhóm."),
        _ => ("Could not join party.", "Không thể vào nhóm."),
    };

    private static bool IsVietnamese(Player player) =>
        player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    private async ValueTask<Player?> FindOnlineLeaderAsync(IGameContext primaryContext, string leaderName)
    {
        if (this._leaderContexts.TryGetValue(leaderName, out var ctx))
        {
            var hit = (await ctx.GetPlayersAsync().ConfigureAwait(false))
                .FirstOrDefault(p => p.SelectedCharacter?.Name.Equals(leaderName, StringComparison.OrdinalIgnoreCase) == true);
            if (hit is not null)
            {
                return hit;
            }
        }

        return (await primaryContext.GetPlayersAsync().ConfigureAwait(false))
            .FirstOrDefault(p => p.SelectedCharacter?.Name.Equals(leaderName, StringComparison.OrdinalIgnoreCase) == true);
    }

    private async ValueTask RefreshAdsFromLiveLeadersAsync(IGameContext gameContext)
    {
        var engine = this.GetEngine();
        foreach (var ad in engine.ListAdvertisements())
        {
            var leader = await this.FindOnlineLeaderAsync(gameContext, ad.LeaderName).ConfigureAwait(false);
            if (leader is null || (leader.Party is not null && leader.Party.PartyMaster?.Name != ad.LeaderName))
            {
                engine.RemoveAdvertisement(ad.LeaderName);
                continue;
            }

            var liveCount = leader.Party?.PartyList.Count ?? 1;
            var liveMap = leader.CurrentMap?.Definition.Name.ValueInNeutralLanguage ?? ad.MapName;
            if (liveCount != ad.MemberCount || !string.Equals(liveMap, ad.MapName, StringComparison.Ordinal))
            {
                ad.MemberCount = liveCount;
                ad.MapName = liveMap;
                engine.SetAdvertisement(ad);
            }
        }
    }
}
