// <copyright file="MarriagePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Marriage plug-in (OFF by default): proposals, Devias Church ceremonies, custom couple titles,
/// party hunting EXP/defense bonuses, spouse warp, divorce cooldown rules, and public couples registry.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Marriage", Description = "Proposals, Devias Church ceremonies, couple titles, party hunting bonus, and divorce rules (OFF by default).")]
[Guid("5F42A81D-8E13-4B76-A2E9-70B4F3D19248")]
public sealed class MarriagePlugIn :
    IPeriodicTaskPlugIn,
    IPlayerStateChangedPlugIn,
    ICharacterDeletedPlugIn,
    ISupportCustomConfiguration<MarriageConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>Buff effect number (200+ is server-side only, matching GuildsPlugIn).</summary>
    public const short BuffEffectNumber = 242;

    private static readonly TimeSpan BuffDuration = TimeSpan.FromMinutes(2);
    private static readonly MagicEffectDefinition BuffDefinition = new()
    {
        Number = BuffEffectNumber,
        Name = "HNMO couple hunting bonus",
        SubType = 242,
        InformObservers = false,
        StopByDeath = false,
        SendDuration = false,
    };

    private (MarriageConfiguration Config, MarriageEngine Engine)? _engine;
    private DateTime _lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public MarriageConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="MarriagePlugIn"/> for a game context, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static MarriagePlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<MarriagePlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MarriageConfiguration();

    /// <summary>Gets or creates the engine for the current configuration.</summary>
    /// <returns>The engine.</returns>
    public MarriageEngine GetEngine()
    {
        var cfg = this.Configuration ??= new MarriageConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new MarriageEngine(cfg, MarriageStore.Open(cfg.FileName), enabled: true);
        this._engine = (cfg, engine);
        return engine;
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
        var engine = this.GetEngine();
        await gameContext.ForEachPlayerAsync(async player => await this.RefreshCoupleBonusAsync(player, engine).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            await this.RefreshCoupleBonusAsync(player, this.GetEngine()).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
        }
    }

    /// <summary>Checks whether both spouses are currently hunting together in the same party and map.</summary>
    /// <param name="player">The player.</param>
    /// <param name="engine">The marriage engine.</param>
    /// <returns><c>true</c> if couple bonus is active right now.</returns>
    public bool IsCoupleBonusActive(Player player, MarriageEngine engine)
    {
        var myName = player.SelectedCharacter?.Name;
        if (string.IsNullOrWhiteSpace(myName) || player.Party is not { } party)
        {
            return false;
        }

        var spouseName = engine.GetSpouseName(myName);
        if (string.IsNullOrWhiteSpace(spouseName))
        {
            return false;
        }

        var spouse = party.PartyList.OfType<Player>().FirstOrDefault(p =>
            string.Equals(p.SelectedCharacter?.Name, spouseName, StringComparison.OrdinalIgnoreCase));
        return spouse is not null
            && spouse.CurrentMap is not null
            && player.CurrentMap is not null
            && spouse.CurrentMap.Definition.Number == player.CurrentMap.Definition.Number;
    }

    /// <summary>Sends the marriage state JSON to <paramref name="player"/> via <c>F5 37</c>.</summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether explicitly requested by client.</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>A task representing the send.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        var name = player.SelectedCharacter?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var engine = this.GetEngine();
        var bonusActive = this.IsCoupleBonusActive(player, engine);
        var json = MarriageStateWriter.WritePlayerState(engine, name, bonusActive, DateTime.UtcNow, actionResult, messageEn, messageVi);
        await player.InvokeViewPlugInAsync<IHnmoMarriageViewPlugIn>(p => p.ShowMarriageAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Sends a proposal to <paramref name="targetName"/>.</summary>
    /// <param name="player">The proposing player.</param>
    /// <param name="targetName">Target character name.</param>
    /// <returns>A task representing the operation.</returns>
    public async ValueTask ProposeAsync(Player player, string targetName)
    {
        var me = player.SelectedCharacter;
        if (me is null || player.Account is null)
        {
            return;
        }

        var target = await FindOnlinePlayerAsync(player.GameContext, targetName).ConfigureAwait(false);
        if (target?.SelectedCharacter is null)
        {
            await this.NotifyPlayerAsync(player, "TargetOffline", $"Player '{targetName}' is not online.", $"Người chơi '{targetName}' hiện không trực tuyến.").ConfigureAwait(false);
            return;
        }

        var engine = this.GetEngine();
        var outcome = engine.Propose(
            me.Name,
            player.Account.LoginName,
            player.Level,
            target.SelectedCharacter.Name,
            target.Level,
            DateTime.UtcNow);

        var (en, vi) = DescribeOutcome(outcome, engine.Configuration, target.SelectedCharacter.Name);
        await this.NotifyPlayerAsync(player, outcome.ToString(), en, vi).ConfigureAwait(false);

        if (outcome == MarriageActionResult.Success)
        {
            var inviteEn = $"{me.Name} proposed marriage to you! Stand near Devias Church (210, 23) and press Shift+M or type /marry accept.";
            var inviteVi = $"{me.Name} đã gửi lời cầu hôn tới bạn! Hãy đứng gần Nhà thờ Devias (210, 23) và nhấn Shift+M hoặc gõ /marry accept.";
            await target.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(IsVi(target) ? inviteVi : inviteEn, MessageType.GoldenCenter)).ConfigureAwait(false);
            await this.SendStateAsync(target, requested: false, actionResult: "ProposalReceived", messageEn: inviteEn, messageVi: inviteVi).ConfigureAwait(false);
        }
    }

    /// <summary>Accepts a pending proposal and completes the ceremony at the configured location.</summary>
    /// <param name="player">The accepting player.</param>
    /// <returns>A task representing the operation.</returns>
    public async ValueTask AcceptAsync(Player player)
    {
        var me = player.SelectedCharacter;
        if (me is null || player.Account is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var utcNow = DateTime.UtcNow;
        var pending = engine.GetIncomingProposal(me.Name, utcNow);
        if (pending is null)
        {
            await this.NotifyPlayerAsync(player, MarriageActionResult.NoPendingProposal.ToString(), "No pending marriage proposal.", "Không có lời cầu hôn nào đang chờ.").ConfigureAwait(false);
            return;
        }

        var proposer = await FindOnlinePlayerAsync(player.GameContext, pending.ProposerName).ConfigureAwait(false);
        if (proposer?.SelectedCharacter is null)
        {
            await this.NotifyPlayerAsync(player, "ProposerOffline", $"{pending.ProposerName} went offline before the ceremony.", $"{pending.ProposerName} đã thoát trước khi làm lễ.").ConfigureAwait(false);
            return;
        }

        var cost = Math.Max(0, engine.Configuration.CeremonyZenCost);
        if (cost > 0 && proposer.Money < cost)
        {
            await this.NotifyPlayerAsync(player, MarriageActionResult.NotEnoughZen.ToString(), $"{pending.ProposerName} needs {cost:N0} Zen for the ceremony.", $"{pending.ProposerName} cần {cost:N0} Zen cho lễ cưới.").ConfigureAwait(false);
            return;
        }

        var accepterMap = player.CurrentMap?.Definition.Number ?? -1;
        var proposerMap = proposer.CurrentMap?.Definition.Number ?? -1;
        var bothAtSpot = engine.IsAtCeremonySpot(accepterMap, player.Position.X, player.Position.Y)
            && engine.IsAtCeremonySpot(proposerMap, proposer.Position.X, proposer.Position.Y);

        var outcome = engine.AcceptCeremony(
            me.Name,
            player.Account.LoginName,
            player.Level,
            proposer.Level,
            bothAtSpot,
            utcNow,
            out var couple);

        if (outcome == MarriageActionResult.Success && couple is not null)
        {
            if (cost > 0)
            {
                proposer.TryRemoveMoney(cost);
            }

            await player.GameContext.ForEachPlayerAsync(async online =>
            {
                var msg = IsVi(online)
                    ? $"Chúc mừng lễ thành hôn của {couple.PartnerAName} và {couple.PartnerBName} tại Nhà thờ Devias!"
                    : $"Congratulations to {couple.PartnerAName} and {couple.PartnerBName} on their wedding ceremony!";
                await online.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, MessageType.GoldenCenter)).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await this.RefreshCoupleBonusAsync(player, engine).ConfigureAwait(false);
            await this.RefreshCoupleBonusAsync(proposer, engine).ConfigureAwait(false);
            await this.SendStateAsync(proposer, requested: false, actionResult: "Married", messageEn: $"You are now married to {me.Name}!", messageVi: $"Bạn đã kết hôn cùng {me.Name}!").ConfigureAwait(false);
        }

        var (en, vi) = DescribeOutcome(outcome, engine.Configuration, pending.ProposerName);
        await this.NotifyPlayerAsync(player, outcome.ToString(), en, vi).ConfigureAwait(false);
    }

    /// <summary>Declines an incoming marriage proposal.</summary>
    /// <param name="player">The declining player.</param>
    /// <returns>A task representing the operation.</returns>
    public async ValueTask DeclineAsync(Player player)
    {
        var name = player.SelectedCharacter?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var engine = this.GetEngine();
        if (!engine.DeclineProposal(name, out var proposerName))
        {
            await this.NotifyPlayerAsync(player, "NoPendingProposal", "No pending proposal to decline.", "Không có lời cầu hôn nào để từ chối.").ConfigureAwait(false);
            return;
        }

        await this.NotifyPlayerAsync(player, "Declined", $"Declined proposal from {proposerName}.", $"Đã từ chối lời cầu hôn của {proposerName}.").ConfigureAwait(false);
        if (proposerName is not null && await FindOnlinePlayerAsync(player.GameContext, proposerName).ConfigureAwait(false) is { } proposer)
        {
            var en = $"{name} declined your proposal.";
            var vi = $"{name} đã từ chối lời cầu hôn của bạn.";
            await proposer.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(IsVi(proposer) ? vi : en, MessageType.BlueNormal)).ConfigureAwait(false);
            await this.SendStateAsync(proposer, requested: false).ConfigureAwait(false);
        }
    }

    /// <summary>Changes the couple's custom title.</summary>
    /// <param name="player">The player.</param>
    /// <param name="title">The new couple title.</param>
    /// <returns>A task representing the operation.</returns>
    public async ValueTask SetTitleAsync(Player player, string title)
    {
        var name = player.SelectedCharacter?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var engine = this.GetEngine();
        var outcome = engine.SetCustomTitle(name, title);
        var (en, vi) = outcome == MarriageActionResult.Success
            ? ("Updated couple title!", "Đã cập nhật danh hiệu phu thê!")
            : DescribeOutcome(outcome, engine.Configuration, name);
        await this.NotifyPlayerAsync(player, outcome.ToString(), en, vi).ConfigureAwait(false);

        if (outcome == MarriageActionResult.Success
            && engine.GetSpouseName(name) is { } spouseName
            && await FindOnlinePlayerAsync(player.GameContext, spouseName).ConfigureAwait(false) is { } spouse)
        {
            await this.SendStateAsync(spouse, requested: false).ConfigureAwait(false);
        }
    }

    /// <summary>Divorces the caller's spouse according to server fee and cooldown rules.</summary>
    /// <param name="player">The player filing for divorce.</param>
    /// <returns>A task representing the operation.</returns>
    public async ValueTask DivorceAsync(Player player)
    {
        var name = player.SelectedCharacter?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var engine = this.GetEngine();
        var fee = Math.Max(0, engine.Configuration.DivorceZenFee);
        if (fee > 0 && player.Money < fee)
        {
            await this.NotifyPlayerAsync(player, MarriageActionResult.NotEnoughZen.ToString(), $"Need {fee:N0} Zen to divorce.", $"Cần {fee:N0} Zen để ly hôn.").ConfigureAwait(false);
            return;
        }

        var outcome = engine.Divorce(name, DateTime.UtcNow, out var exSpouseName);
        if (outcome == MarriageActionResult.Success)
        {
            if (fee > 0)
            {
                player.TryRemoveMoney(fee);
            }

            if (exSpouseName is not null && await FindOnlinePlayerAsync(player.GameContext, exSpouseName).ConfigureAwait(false) is { } ex)
            {
                var exEn = $"{name} dissolved your marriage.";
                var exVi = $"{name} đã hoàn tất thủ tục ly hôn.";
                await ex.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(IsVi(ex) ? exVi : exEn, MessageType.BlueNormal)).ConfigureAwait(false);
                await this.SendStateAsync(ex, requested: false).ConfigureAwait(false);
            }
        }

        var (en, vi) = outcome == MarriageActionResult.Success
            ? ($"You divorced {exSpouseName}. Cooldown before remarrying: {engine.Configuration.RemarryCooldownHours}h.", $"Đã ly hôn {exSpouseName}. Thời gian chờ tái hôn: {engine.Configuration.RemarryCooldownHours} giờ.")
            : DescribeOutcome(outcome, engine.Configuration, name);
        await this.NotifyPlayerAsync(player, outcome.ToString(), en, vi).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void CharacterDeleted(Player player, Character deletedCharacter)
    {
        // A new character with the same name must not inherit the marriage (audit 1.10).
        this.GetEngine().ForgetCharacter(deletedCharacter.Name);
    }

    /// <summary>
    /// Gets the level a player needs to warp to the map: the lowest level of the warps to it, with the reduction of the
    /// player's class (e.g. Magic Gladiator); <c>null</c> when no warp leads to the map.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="map">The map.</param>
    /// <returns>The level, or <c>null</c>.</returns>
    internal static int? GetSpouseWarpLevel(Player player, GameMapDefinition map)
    {
        var lowest = player.GameContext.Configuration.WarpList
            .Where(warp => warp.Gate?.Map == map)
            .Select(warp => (int?)warp.LevelRequirement)
            .Min();
        return lowest is { } level && player.SelectedCharacter is { } character
            ? character.GetEffectiveMoveLevelRequirement(level)
            : lowest;
    }

    /// <summary>Warps the player to their online spouse's current field map coordinates.</summary>
    /// <param name="player">The requesting player.</param>
    /// <returns>A task representing the warp.</returns>
    public async ValueTask TeleportToSpouseAsync(Player player)
    {
        var name = player.SelectedCharacter?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var engine = this.GetEngine();
        if (!engine.Configuration.AllowSpouseWarp)
        {
            await this.NotifyPlayerAsync(player, "WarpDisabled", "Spouse warp is disabled on this server.", "Dịch chuyển tới phu thê đang tắt trên máy chủ này.").ConfigureAwait(false);
            return;
        }

        var spouseName = engine.GetSpouseName(name);
        if (string.IsNullOrWhiteSpace(spouseName))
        {
            await this.NotifyPlayerAsync(player, MarriageActionResult.NotMarried.ToString(), "You are not married.", "Bạn chưa kết hôn.").ConfigureAwait(false);
            return;
        }

        if (player.CurrentMiniGame is not null)
        {
            await this.NotifyPlayerAsync(player, "InEvent", "Cannot warp out of an active event.", "Không thể dịch chuyển khi đang ở trong sự kiện.").ConfigureAwait(false);
            return;
        }

        var spouse = await FindOnlinePlayerAsync(player.GameContext, spouseName).ConfigureAwait(false);
        if (spouse?.CurrentMap is null || spouse.CurrentMiniGame is not null)
        {
            await this.NotifyPlayerAsync(player, "SpouseUnavailable", $"{spouseName} is offline or inside an event.", $"{spouseName} hiện đang thoát hoặc ở trong sự kiện.").ConfigureAwait(false);
            return;
        }

        if (HnmoWarpGuard.Refusal(player, spouse.CurrentMap.Definition) is { } refusal)
        {
            await this.NotifyPlayerAsync(player, "WarpRefused", refusal.En, refusal.Vi).ConfigureAwait(false);
            return;
        }

        // The spouse's map needs the level of its warp, like a normal warp (owner, 21 Nov 2026).
        if (GetSpouseWarpLevel(player, spouse.CurrentMap.Definition) is { } requiredLevel
            && (player.Attributes?[Stats.Level] ?? 0) < requiredLevel)
        {
            await this.NotifyPlayerAsync(player, "WarpLevel", $"You need level {requiredLevel} to warp to that map.", $"Cần cấp {requiredLevel} để dịch chuyển tới bản đồ đó.").ConfigureAwait(false);
            return;
        }

        var gate = new ExitGate
        {
            Map = spouse.CurrentMap.Definition,
            X1 = spouse.Position.X,
            X2 = spouse.Position.X,
            Y1 = spouse.Position.Y,
            Y2 = spouse.Position.Y,
        };
        await player.WarpToAsync(gate).ConfigureAwait(false);
        await this.NotifyPlayerAsync(player, "Warped", $"Warped to {spouseName}!", $"Đã dịch chuyển tới bên cạnh {spouseName}!").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ForceStart()
    {
    }

    private static bool IsVi(Player player) =>
        string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);

    private static async ValueTask<Player?> FindOnlinePlayerAsync(IGameContext context, string characterName)
    {
        Player? found = null;
        await context.ForEachPlayerAsync(p =>
        {
            if (found is null && string.Equals(p.SelectedCharacter?.Name, characterName, StringComparison.OrdinalIgnoreCase))
            {
                found = p;
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);
        return found;
    }

    private static (string En, string Vi) DescribeOutcome(MarriageActionResult result, MarriageConfiguration cfg, string targetName) => result switch
    {
        MarriageActionResult.Success => ($"Marriage action with {targetName} succeeded!", $"Thao tác kết hôn với {targetName} thành công!"),
        MarriageActionResult.InvalidTarget => ("Pick a valid online character other than yourself.", "Hãy chọn tên nhân vật hợp lệ khác với bạn."),
        MarriageActionResult.AlreadyMarried => ("One of you is already married.", "Một trong hai người đã kết hôn rồi."),
        MarriageActionResult.NotMarried => ("You are not married.", "Bạn chưa kết hôn."),
        MarriageActionResult.LevelTooLow => ($"Both partners must be at least level {cfg.MinimumLevel}.", $"Cả hai phải đạt tối thiểu cấp {cfg.MinimumLevel}."),
        MarriageActionResult.NotAtCeremonyLocation => ($"Both must stand at the ceremony hall (Map #{cfg.CeremonyMapNumber} around {cfg.CeremonyX}, {cfg.CeremonyY}).", $"Cả hai phải đứng tại lễ đường (Bản đồ #{cfg.CeremonyMapNumber} gần tọa độ {cfg.CeremonyX}, {cfg.CeremonyY})."),
        MarriageActionResult.NotEnoughZen => ($"Not enough Zen (requires {cfg.CeremonyZenCost:N0} Zen).", $"Không đủ Zen (cần {cfg.CeremonyZenCost:N0} Zen)."),
        MarriageActionResult.CooldownActive => ("One partner is still on remarriage cooldown.", "Một trong hai người vẫn đang trong thời gian chờ tái hôn."),
        MarriageActionResult.ProposalTooSoon => ("Wait a little before proposing again.", "Hãy chờ một chút trước khi cầu hôn lại."),
        MarriageActionResult.TargetHasProposal => ("Someone else's proposal is waiting for that character's answer.", "Nhân vật đó đang có một lời cầu hôn khác chờ trả lời."),
        MarriageActionResult.NoPendingProposal => ("No matching pending marriage proposal.", "Không tìm thấy lời cầu hôn đang chờ."),
        MarriageActionResult.InvalidTitle => ("Custom couple title is empty/too long or disabled.", "Danh hiệu phu thê không hợp lệ hoặc đang tắt."),
        _ => ("Marriage feature is currently disabled.", "Tính năng Kết hôn hiện đang tắt."),
    };

    private async ValueTask RefreshCoupleBonusAsync(Player player, MarriageEngine engine)
    {
        if (player.SelectedCharacter is null || player.MagicEffectList is null)
        {
            return;
        }

        if (!this.IsCoupleBonusActive(player, engine))
        {
            return;
        }

        var cfg = engine.Configuration;
        var elements = new List<MagicEffect.ElementWithTarget>();
        if (cfg.CoupleExpBonusPercent > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement((float)(cfg.CoupleExpBonusPercent / 100.0), AggregateType.AddRaw), Stats.BonusExperienceRate));
        }

        if (cfg.CoupleDefenseBonus > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement(cfg.CoupleDefenseBonus, AggregateType.AddFinal), Stats.DefenseFinal));
        }

        if (elements.Count == 0)
        {
            return;
        }

        await player.MagicEffectList.AddEffectAsync(new MagicEffect(BuffDuration, BuffDefinition, elements.ToArray())).ConfigureAwait(false);
    }

    private async ValueTask NotifyPlayerAsync(Player player, string code, string en, string vi)
    {
        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(IsVi(player) ? vi : en, MessageType.BlueNormal)).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, actionResult: code, messageEn: en, messageVi: vi).ConfigureAwait(false);
    }
}
