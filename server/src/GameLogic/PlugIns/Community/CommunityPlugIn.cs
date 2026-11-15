// <copyright file="CommunityPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.GameLogic.AccountLinking;
using HNMO.GameLogic.CastleSiege;
using HNMO.GameLogic.Discord;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// HNMO's community: one-time coins for linking the account to Discord (with the Discord bot's /link) and for joining
/// the Zalo group (a claim with a code which the admin approves in the admin panel; Zalo has no open bot API for
/// groups), community events (a code word entered once per account while the event runs, or a list of winning
/// characters; coins, zen and items, each paid once), and automatic posts to Discord and the website's recent
/// highlights (rare drops, world boss kills, guild level-ups and ladder winners, castle siege winners). The web game
/// shows it in the community window (K); the website in its Community section. The state is kept in a JSON file in the
/// save folder.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CommunityPlugIn_Name), Description = nameof(PlugInResources.CommunityPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8C2F4A71-6D3E-4B95-A1C8-5E7D2F9B0A36")]
public class CommunityPlugIn : IPeriodicTaskPlugIn, IMonsterItemDroppedPlugIn, ICastleSiegeStateChangedPlugIn, IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<CommunityConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>The name in the coins history's "granted by" column.</summary>
    public const string GrantedBy = "HNMO community";

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();

    private DateTime _nextTickUtc = DateTime.UtcNow + TimeSpan.FromSeconds(15);
    private int _running;
    private (CommunityConfiguration Configuration, CommunityEngine Engine)? _engine;

    /// <inheritdoc />
    public CommunityConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static CommunityPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<CommunityPlugIn>().FirstOrDefault();

    /// <summary>
    /// Gets a value indicating whether the community posts the rare drops to Discord, so the game events publisher
    /// leaves its own drop posts out (no post twice).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns><c>true</c>, if the community posts them.</returns>
    public static bool PostsDropsToDiscord(IGameContext gameContext) =>
        Get(gameContext)?.GetEngine().Configuration.RareDrop is { Enabled: true, ToDiscord: true };

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CommunityConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public CommunityEngine GetEngine()
    {
        var configuration = this.Configuration ??= new CommunityConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new CommunityEngine(configuration, CommunityStore.Open(configuration.FileName));
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>
    /// Sends the state to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="stateEvent">What happened.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, CommunityStateEvent? stateEvent = null)
    {
        if (!TryGetKeys(player, out var login, out var character))
        {
            return;
        }

        var linked = await DiscordUserOfAsync(player).ConfigureAwait(false) is not null;
        var facts = new CommunityPlayerFacts(login, character, DiscordIntegrationFeaturePlugIn.GetConfiguration(player.GameContext) is not null, linked);
        var json = CommunityStateWriter.WriteState(this.GetEngine(), facts, DateTime.UtcNow, stateEvent, ItemNames(player.GameContext));
        await player.InvokeViewPlugInAsync<IHnmoCommunityViewPlugIn>(p => p.ShowCommunityAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the Discord coins when the account is linked (the window's button; also checked when entering the world).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="tell">Whether the result is sent to the client (else only a payment is).</param>
    /// <returns>The task.</returns>
    public async ValueTask ClaimDiscordAsync(Player player, bool tell = true)
    {
        if (!TryGetKeys(player, out var login, out _))
        {
            return;
        }

        var userId = await DiscordUserOfAsync(player).ConfigureAwait(false);
        var (result, grant) = this.GetEngine().ClaimDiscord(login, userId, DateTime.UtcNow);
        if (grant is not null)
        {
            await MissionCoinGranter.GrantAsync(player.GameContext, grant, GrantedBy).ConfigureAwait(false);
            player.Logger.LogInformation("Community: {Player} got {Coins} coins for linking Discord.", player, grant.Amount);
            this.Save();
        }

        if (tell || grant is not null)
        {
            await this.SendStateAsync(player, tell, new CommunityStateEvent("discord", result.ToString(), grant?.Amount ?? 0)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Makes a Zalo claim (the admin approves it in the admin panel).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="zaloName">The player's name in Zalo, if given.</param>
    /// <returns>The task.</returns>
    public async ValueTask RequestZaloAsync(Player player, string? zaloName)
    {
        if (!TryGetKeys(player, out var login, out var character))
        {
            return;
        }

        var (result, code) = this.GetEngine().RequestZalo(login, character, zaloName, DateTime.UtcNow);
        this.Save();
        if (result == CommunityClaimResult.Ok)
        {
            player.Logger.LogInformation("Community: {Player} claims the Zalo coins with the code {Code}.", player, code);
        }

        await this.SendStateAsync(player, true, new CommunityStateEvent("zalo", result.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    /// Enters a community event's code.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="code">The code.</param>
    /// <returns>The task.</returns>
    public async ValueTask EnterCodeAsync(Player player, string? code)
    {
        if (!TryGetKeys(player, out var login, out var character))
        {
            return;
        }

        var engine = this.GetEngine();
        var (result, definition) = engine.EnterCode(login, character, code, DateTime.UtcNow);
        this.Save();
        var coins = 0;
        if (result == CommunityCodeResult.Ok)
        {
            player.Logger.LogInformation("Community: {Player} entered the code of the event {Event}.", player, definition?.Id);
            coins = await this.PayCoinsAsync(player.GameContext).ConfigureAwait(false) > 0 ? definition?.Reward.Coins ?? 0 : 0;
            await this.DeliverAsync(player).ConfigureAwait(false);
            this.Save();
        }

        await this.SendStateAsync(player, true, new CommunityStateEvent("code", result.ToString(), coins, definition?.Id)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the waiting zen and items which fit (the window's button).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask ClaimAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        this.Save();
        await this.SendStateAsync(player, true, new CommunityStateEvent("claim", left > 0 ? "BagFull" : given > 0 ? "Ok" : "Nothing")).ConfigureAwait(false);
    }

    /// <summary>
    /// Posts something worth telling: into the website's highlights and, when that's on, to Discord.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="valuesEn">The English values of the placeholders.</param>
    /// <param name="valuesVi">The Vietnamese values; the English ones when <c>null</c>.</param>
    /// <returns>The task.</returns>
    public async ValueTask PostAsync(IGameContext gameContext, CommunityPostKind kind, IReadOnlyDictionary<string, string> valuesEn, IReadOnlyDictionary<string, string>? valuesVi = null)
    {
        var engine = this.GetEngine();
        if (engine.Post(kind, valuesEn, valuesVi, DateTime.UtcNow) is not { } post)
        {
            return;
        }

        this.Save();
        if (engine.Configuration.Post(kind).ToDiscord && gameContext is IGameServerContext serverContext && post.TextEn.Length > 0)
        {
            try
            {
                await serverContext.EventPublisher.GameEventAsync(new CommunityHighlightEvent(serverContext.Id, post.AtUtc, kind.ToString(), post.TextEn, post.TextVi)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<CommunityPlugIn>().LogWarning(ex, "Community: the post {Kind} wasn't published.", kind);
            }
        }
    }

    /// <summary>
    /// Posts a world boss kill (called by the world bosses).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="record">The kill.</param>
    /// <returns>The task.</returns>
    public ValueTask WorldBossKilledAsync(IGameContext gameContext, WorldBossKillRecord record)
    {
        if (!record.Killed)
        {
            return ValueTask.CompletedTask;
        }

        var top = record.Lines.FirstOrDefault()?.Name ?? record.LastHit ?? "-";
        var map = MapName(gameContext, record.MapNumber);
        return this.PostAsync(
            gameContext,
            CommunityPostKind.WorldBossKill,
            new Dictionary<string, string> { ["boss"] = record.NameEn, ["player"] = top, ["map"] = map.En },
            new Dictionary<string, string> { ["boss"] = string.IsNullOrWhiteSpace(record.NameVi) ? record.NameEn : record.NameVi, ["player"] = top, ["map"] = map.Vi });
    }

    /// <summary>
    /// Posts a guild level-up (called by the guilds).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="guildName">The guild.</param>
    /// <param name="level">The new level.</param>
    /// <returns>The task.</returns>
    public ValueTask GuildLevelUpAsync(IGameContext gameContext, string guildName, int level) =>
        this.PostAsync(gameContext, CommunityPostKind.GuildLevelUp, new Dictionary<string, string> { ["guild"] = guildName, ["level"] = level.ToString(CultureInfo.InvariantCulture) });

    /// <summary>
    /// Posts a top race's start (called by the top races).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="titleEn">The English title.</param>
    /// <param name="titleVi">The Vietnamese title.</param>
    /// <param name="endEn">The end, in English.</param>
    /// <param name="endVi">The end, in Vietnamese.</param>
    /// <returns>The task.</returns>
    public ValueTask TopRaceStartedAsync(IGameContext gameContext, string titleEn, string titleVi, string endEn, string endVi) =>
        this.PostAsync(
            gameContext,
            CommunityPostKind.TopRaceStarted,
            new Dictionary<string, string> { ["race"] = titleEn, ["end"] = endEn },
            new Dictionary<string, string> { ["race"] = titleVi, ["end"] = endVi });

    /// <summary>
    /// Posts a top race winner (called by the top races, for the top ranks of each table).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="titleEn">The English title.</param>
    /// <param name="titleVi">The Vietnamese title.</param>
    /// <param name="name">The winner's name.</param>
    /// <param name="rank">The rank.</param>
    /// <param name="valueEn">The value, in English.</param>
    /// <param name="valueVi">The value, in Vietnamese.</param>
    /// <returns>The task.</returns>
    public ValueTask TopRaceWinnerAsync(IGameContext gameContext, string titleEn, string titleVi, string name, int rank, string valueEn, string valueVi)
    {
        var r = rank.ToString(CultureInfo.InvariantCulture);
        return this.PostAsync(
            gameContext,
            CommunityPostKind.TopRaceWinner,
            new Dictionary<string, string> { ["race"] = titleEn, ["player"] = name, ["rank"] = r, ["value"] = valueEn },
            new Dictionary<string, string> { ["race"] = titleVi, ["player"] = name, ["rank"] = r, ["value"] = valueVi });
    }

    /// <summary>
    /// Posts a weekly guild ladder winner (called by the guilds, for the top ranks).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="guildName">The guild.</param>
    /// <param name="rank">The rank.</param>
    /// <param name="points">The ladder points.</param>
    /// <returns>The task.</returns>
    public ValueTask GuildLadderWinnerAsync(IGameContext gameContext, string guildName, int rank, long points)
    {
        if (rank > this.GetEngine().Configuration.LadderRanksPosted)
        {
            return ValueTask.CompletedTask;
        }

        return this.PostAsync(gameContext, CommunityPostKind.GuildLadderWinner, new Dictionary<string, string>
        {
            ["guild"] = guildName,
            ["rank"] = rank.ToString(CultureInfo.InvariantCulture),
            ["points"] = points.ToString("N0", CultureInfo.InvariantCulture),
        });
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        var serverId = ServerIdOf(gameContext);
        Contexts[serverId] = gameContext;
        if (now < this._nextTickUtc || Interlocked.Exchange(ref this._running, 1) == 1)
        {
            return;
        }

        try
        {
            this._nextTickUtc = now + TickInterval;
            if (serverId == Contexts.Keys.Min())
            {
                await this.PayZaloAsync(gameContext, now).ConfigureAwait(false);
                await this.RecordWinnersAsync(gameContext, now).ConfigureAwait(false);
                await this.PayCoinsAsync(gameContext).ConfigureAwait(false);
            }

            foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
            {
                if (IsCounted(player) && this.GetEngine().Owed(player.SelectedCharacter!.Name).Count > 0)
                {
                    await this.DeliverAndTellAsync(player).ConfigureAwait(false);
                }
            }

            this.Save();
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<CommunityPlugIn>().LogError(ex, "Community: the tick failed.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextTickUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public async ValueTask MonsterItemDroppedAsync(AttackableNpcBase monster, Player killer, DroppedItem droppedItem)
    {
        // This is called for every dropped item, so the cheap checks come first.
        var engine = this.GetEngine();
        if (!engine.Configuration.RareDrop.Enabled || !IsCounted(killer) || droppedItem.Item.Definition is not { } definition)
        {
            return;
        }

        var item = droppedItem.Item;
        var map = monster.CurrentMap.Definition;
        if (!engine.IsRareDrop(definition.Group, definition.Number, item.Level, item.IsExcellent(), item.IsAncient(), map.Number)
            || !engine.TryTakeDropSlot(killer.Name, DateTime.UtcNow))
        {
            return;
        }

        var name = definition.Name.ValueInNeutralLanguage;
        var vi = CultureInfo.GetCultureInfo("vi");
        var itemEn = (item.IsExcellent() ? "Excellent " : string.Empty) + (item.IsAncient() ? "Ancient " : string.Empty) + name + (item.Level > 0 ? $" +{item.Level}" : string.Empty);
        var itemVi = name + (item.Level > 0 ? $" +{item.Level}" : string.Empty) + (item.IsExcellent() ? " (hoàn hảo)" : string.Empty) + (item.IsAncient() ? " (thần)" : string.Empty);
        await this.PostAsync(
            killer.GameContext,
            CommunityPostKind.RareDrop,
            new Dictionary<string, string> { ["player"] = killer.Name, ["item"] = itemEn, ["monster"] = monster.Definition.Designation.ValueInNeutralLanguage, ["map"] = map.Name.ValueInNeutralLanguage },
            new Dictionary<string, string> { ["player"] = killer.Name, ["item"] = itemVi, ["monster"] = monster.Definition.Designation.GetTranslation(vi) ?? monster.Definition.Designation.ValueInNeutralLanguage, ["map"] = map.Name.GetTranslation(vi) ?? map.Name.ValueInNeutralLanguage }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CastleSiegeStateChangedAsync(IGameContext gameContext, CastleSiegeContext castleSiege, CastleSiegeState previousState)
    {
        if (previousState != CastleSiegeState.Start || castleSiege.SiegeData?.OwnerGuildId is not { } guildId)
        {
            return;
        }

        string? name = null;
        try
        {
            using var context = gameContext.PersistenceContextProvider.CreateNewGuildContext();
            var names = await context.GetPersistentGuildNamesAsync([guildId]).ConfigureAwait(false);
            names.TryGetValue(guildId, out name);
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<CommunityPlugIn>().LogWarning(ex, "Community: the name of the castle siege winner wasn't read.");
        }

        if (!string.IsNullOrEmpty(name))
        {
            await this.PostAsync(gameContext, CommunityPostKind.CastleSiegeWinner, new Dictionary<string, string> { ["guild"] = name }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld || previousState != PlayerState.CharacterSelection || !IsCounted(player))
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Linked with the Discord bot while away: the coins come now.
                await this.ClaimDiscordAsync(player, false).ConfigureAwait(false);
                await this.DeliverAndTellAsync(player).ConfigureAwait(false);
                this.Save();
            }
            catch (Exception ex)
            {
                player.Logger.LogDebug(ex, "Community: entering the world of {Player} wasn't handled.", player);
            }
        });
        return ValueTask.CompletedTask;
    }

    private static byte ServerIdOf(IGameContext context) => (context as IGameServerContext)?.Id ?? 0;

    private static bool IsCounted(Player player) =>
        player is not OfflinePlayer && player.Account is { IsBot: false } && !player.IsTemplatePlayer && player.SelectedCharacter is not null;

    private static bool TryGetKeys(Player player, out string login, out string character)
    {
        login = player.Account?.LoginName ?? string.Empty;
        character = player.SelectedCharacter?.Name ?? string.Empty;
        return IsCounted(player) && login.Length > 0 && character.Length > 0;
    }

    private static (string En, string Vi) MapName(IGameContext gameContext, short number)
    {
        var map = gameContext.Configuration.Maps.FirstOrDefault(m => m.Number == number);
        var en = map?.Name.ValueInNeutralLanguage ?? number.ToString(CultureInfo.InvariantCulture);
        return (en, map?.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? en);
    }

    private static Func<short, short, string?> ItemNames(IGameContext gameContext) =>
        (group, number) => gameContext.Configuration.Items.FirstOrDefault(i => i.Group == group && i.Number == number)?.Name.ValueInNeutralLanguage;

    private static async ValueTask<string?> DiscordUserOfAsync(Player player)
    {
        if (player.Account is not { } account)
        {
            return null;
        }

        try
        {
            var service = new AccountLinkService(() => player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration));
            return (await service.GetLinkAsync(account.GetId(), AccountLinkService.DiscordProvider).ConfigureAwait(false))?.ExternalUserId;
        }
        catch (Exception ex)
        {
            player.Logger.LogDebug(ex, "Community: the Discord link of {Player} wasn't read.", player);
            return null;
        }
    }

    private static async ValueTask<Player?> FindPlayerAsync(string character)
    {
        foreach (var context in Contexts.Values.Distinct())
        {
            var player = (await context.GetPlayersAsync().ConfigureAwait(false))
                .FirstOrDefault(p => IsCounted(p) && string.Equals(p.SelectedCharacter!.Name, character, StringComparison.OrdinalIgnoreCase));
            if (player is not null)
            {
                return player;
            }
        }

        return null;
    }

    private async ValueTask PayZaloAsync(IGameContext gameContext, DateTime now)
    {
        var engine = this.GetEngine();
        foreach (var grant in engine.ZaloToPay())
        {
            // The reference makes a second payment of a retry a no-op.
            await MissionCoinGranter.GrantAsync(gameContext, grant, GrantedBy).ConfigureAwait(false);
            engine.MarkZaloPaid(grant.LoginName, now);
            gameContext.LoggerFactory.CreateLogger<CommunityPlugIn>().LogInformation("Community: {Login} got {Coins} coins for joining the Zalo group.", grant.LoginName, grant.Amount);
            if (await this.FindByLoginAsync(grant.LoginName).ConfigureAwait(false) is { } player)
            {
                await this.SendStateAsync(player, false, new CommunityStateEvent("zalo", "Paid", grant.Amount)).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask RecordWinnersAsync(IGameContext gameContext, DateTime now)
    {
        var engine = this.GetEngine();
        var logger = gameContext.LoggerFactory.CreateLogger<CommunityPlugIn>();
        foreach (var (definition, character) in engine.WinnersToRecord(now))
        {
            string? login = null;
            string name = character;
            try
            {
                using var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
                if (await context.GetAccountByCharacterNameAsync(character).ConfigureAwait(false) is { } account)
                {
                    login = account.LoginName;
                    name = account.Characters.FirstOrDefault(c => string.Equals(c.Name, character, StringComparison.OrdinalIgnoreCase))?.Name ?? character;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Community: the account of the winner {Character} wasn't read.", character);
                continue;
            }

            if (login is null)
            {
                // An unknown name: it's checked again on the next tick (the admin can still fix it).
                continue;
            }

            if (engine.RecordWinner(definition, name, login, now))
            {
                logger.LogInformation("Community: {Character} is a winner of the event {Event}.", name, definition.Id);
            }
        }
    }

    private async ValueTask<int> PayCoinsAsync(IGameContext gameContext)
    {
        var engine = this.GetEngine();
        var paid = 0;
        foreach (var (entry, grant) in engine.CoinsToPay())
        {
            if (await MissionCoinGranter.GrantAsync(gameContext, grant, GrantedBy).ConfigureAwait(false))
            {
                paid++;
            }

            engine.MarkCoinsPaid(entry);
            if (entry.Winner && await FindPlayerAsync(entry.Character).ConfigureAwait(false) is { } player)
            {
                await this.SendStateAsync(player, false, new CommunityStateEvent("winner", "Ok", grant.Amount, entry.EventId)).ConfigureAwait(false);
            }
        }

        return paid;
    }

    private async ValueTask<Player?> FindByLoginAsync(string login)
    {
        foreach (var context in Contexts.Values.Distinct())
        {
            var player = (await context.GetPlayersAsync().ConfigureAwait(false))
                .FirstOrDefault(p => IsCounted(p) && string.Equals(p.Account!.LoginName, login, StringComparison.OrdinalIgnoreCase));
            if (player is not null)
            {
                return player;
            }
        }

        return null;
    }

    /// <summary>
    /// Gives the waiting zen and items of a character when they fit.
    /// </summary>
    private async ValueTask<(int Given, int Left)> DeliverAsync(Player player)
    {
        if (!TryGetKeys(player, out _, out var character))
        {
            return (0, 0);
        }

        var engine = this.GetEngine();
        var keep = new List<CommunityOwedReward>();
        var given = 0;
        foreach (var reward in engine.TakeOwed(character))
        {
            try
            {
                if (await GuildsPlugIn.GiveGoodsAsync(player, reward.Reward).ConfigureAwait(false))
                {
                    given++;
                    player.Logger.LogInformation("Community: {Player} got the reward {Reference}.", player, reward.Reference);
                }
                else
                {
                    keep.Add(reward);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Community: the reward {Reference} of {Player} wasn't given; it waits.", reward.Reference, player);
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(character, keep);
        return (given, keep.Count);
    }

    private async ValueTask DeliverAndTellAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        if (given > 0 || left > 0)
        {
            await this.SendStateAsync(player, false, new CommunityStateEvent("goods", left > 0 ? "BagFull" : "Ok", given)).ConfigureAwait(false);
        }
    }

    private void Save()
    {
        var store = this.GetEngine().Store;
        lock (store.Lock)
        {
            store.SaveIfChanged();
        }
    }
}
