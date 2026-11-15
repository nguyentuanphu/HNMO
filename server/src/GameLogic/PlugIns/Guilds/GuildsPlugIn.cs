// <copyright file="GuildsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CastleSiege;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// HNMO's guild levels: the members' play (monsters, events, world bosses, missions, time in the game, the castle
/// siege) gives their guild experience, with a daily cap per member; the guild's level gives its online members a small
/// buff (bonus experience, zen drops, defense); the weekly guild ladder (Monday 00:00 Sydney time) pays coins (and zen
/// or items) once to the members of the top guilds; the castle siege winner gets a ladder bonus. The web game shows it
/// with the guild window; the website shows the ladder. The state is kept in a JSON file in the save folder.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildsPlugIn_Name), Description = nameof(PlugInResources.GuildsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6B4E2D91-3A7C-4F18-9E05-C2D8B1A7F463")]
public class GuildsPlugIn : IPeriodicTaskPlugIn, IAttackableGotKilledPlugIn, IMiniGameEndedPlugIn, ICastleSiegeStateChangedPlugIn, IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<GuildsConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>The name in the coins history's "granted by" column.</summary>
    public const string GrantedBy = "HNMO guild ladder";

    /// <summary>The number of the guild buff's effect (200 and above aren't sent to the game clients).</summary>
    public const short BuffEffectNumber = 240;

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BuffDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GuildKeyLifetime = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();
    private static readonly ConcurrentDictionary<uint, (GuildKey Key, DateTime AtUtc)> GuildKeys = new();
    private static readonly MagicEffectDefinition BuffDefinition = new()
    {
        Number = BuffEffectNumber,
        Name = "HNMO guild buff",
        SubType = 240,
        InformObservers = false,
        StopByDeath = false,
        SendDuration = false,
    };

    private readonly ConcurrentDictionary<string, int> _onlineMinutes = new(StringComparer.Ordinal);
    private DateTime _nextTickUtc = DateTime.UtcNow + TimeSpan.FromSeconds(20);
    private int _running;
    private (GuildsConfiguration Configuration, GuildEngine Engine)? _engine;

    /// <inheritdoc />
    public GuildsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static GuildsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<GuildsPlugIn>().FirstOrDefault();

    /// <summary>
    /// Gets the power-ups of a level's buff: bonus experience (added to the experience rate), a zen drop multiplier and
    /// final defense.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <returns>The power-ups.</returns>
    public static MagicEffect.ElementWithTarget[] BuffElements(GuildLevelDefinition level)
    {
        var elements = new List<MagicEffect.ElementWithTarget>();
        if (level.ExperiencePercent > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement((float)(level.ExperiencePercent / 100.0), AggregateType.AddRaw), Stats.BonusExperienceRate));
        }

        if (level.ZenPercent > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement((float)(1 + (level.ZenPercent / 100.0)), AggregateType.Multiplicate), Stats.MoneyAmountRate));
        }

        if (level.Defense > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement(level.Defense, AggregateType.AddFinal), Stats.DefenseFinal));
        }

        return elements.ToArray();
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new GuildsConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public GuildEngine GetEngine()
    {
        var configuration = this.Configuration ??= new GuildsConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new GuildEngine(configuration, GuildStore.Open(configuration.FileName));
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
    public async ValueTask SendStateAsync(Player player, bool requested, GuildStateEvent? stateEvent = null)
    {
        if (!TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var key = await GuildOfAsync(player).ConfigureAwait(false);
        var json = GuildStateWriter.WriteState(this.GetEngine(), key, characterId, player.SelectedCharacter!.Name, DateTime.UtcNow, stateEvent);
        await player.InvokeViewPlugInAsync<IHnmoGuildsViewPlugIn>(p => p.ShowGuildsAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the waiting rewards which fit (the window's button).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask ClaimAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        this.Save();
        await this.SendStateAsync(player, true, new GuildStateEvent("claim", left > 0 ? "BagFull" : given > 0 ? "Ok" : "Nothing")).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the guild experience of something a player did (e.g. a mission), to the player's guild.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="source">The source.</param>
    /// <param name="times">How many times (percent for the world boss damage).</param>
    /// <returns>The task.</returns>
    public async ValueTask AddForPlayerAsync(Player player, GuildXpSource source, double times = 1)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var engine = this.GetEngine();
        var xp = engine.XpFor(source, times);
        if (xp <= 0 || await GuildOfAsync(player).ConfigureAwait(false) is not { } key)
        {
            return;
        }

        var result = engine.AddXp(key.GuildId, key.Name, characterId, player.SelectedCharacter!.Name, source, xp, DateTime.UtcNow);
        if (result.LeveledUp)
        {
            await this.LevelUpAsync(player.GameContext, key, result.NewLevel).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds the guild experience of a world boss kill: for each player with damage, the participation and the share of
    /// the damage, to their guild (also when they left the game since).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="record">The kill.</param>
    /// <returns>The task.</returns>
    public async ValueTask WorldBossKilledAsync(IGameContext gameContext, WorldBossKillRecord record)
    {
        if (!record.Killed)
        {
            return;
        }

        var engine = this.GetEngine();
        var logger = gameContext.LoggerFactory.CreateLogger<GuildsPlugIn>();
        foreach (var line in record.Lines.Where(l => l.Damage > 0))
        {
            var xp = engine.XpFor(GuildXpSource.WorldBossKill) + engine.XpFor(GuildXpSource.WorldBossDamage, line.Share);
            if (xp <= 0)
            {
                continue;
            }

            try
            {
                using var guilds = gameContext.PersistenceContextProvider.CreateNewGuildContext();
                if (await guilds.GetGuildMembershipAsync(line.Name).ConfigureAwait(false) is not { } membership)
                {
                    continue;
                }

                var names = await guilds.GetPersistentGuildNamesAsync([membership.GuildId]).ConfigureAwait(false);
                names.TryGetValue(membership.GuildId, out var guildName);
                var key = new GuildKey(membership.GuildId, guildName ?? string.Empty);
                var result = engine.AddXp(key.GuildId, guildName, membership.CharacterId.ToString(), line.Name, GuildXpSource.WorldBossKill, xp, DateTime.UtcNow);
                if (result.LeveledUp)
                {
                    await this.LevelUpAsync(gameContext, key, result.NewLevel).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Guilds: the world boss experience of {Name} wasn't added.", line.Name);
            }
        }

        this.Save();
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
            var engine = this.GetEngine();
            foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
            {
                try
                {
                    await this.TickPlayerAsync(player, engine, now).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    player.Logger.LogDebug(ex, "Guilds: the tick of {Player} failed.", player);
                }
            }

            if (serverId == Contexts.Keys.Min())
            {
                await this.PayWeeksAsync(gameContext, now).ConfigureAwait(false);
            }

            this.Save();
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<GuildsPlugIn>().LogError(ex, "Guilds: the tick failed.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextTickUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Monster { SummonedBy: null } monster || monster.Definition.ObjectKind != NpcObjectKind.Monster)
        {
            return;
        }

        var player = killer as Player ?? (killer as IPlayerSurrogate)?.Owner ?? (killer as Monster)?.SummonedBy;
        if (player is not null)
        {
            await this.AddForPlayerAsync(player, GuildXpSource.MonsterKill).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask MiniGameEndedAsync(MiniGameContext miniGame, Player? winner, IReadOnlyCollection<Player> finishers)
    {
        if (miniGame.Definition.Type is not (MiniGameType.BloodCastle or MiniGameType.DevilSquare or MiniGameType.ChaosCastle))
        {
            return;
        }

        foreach (var player in finishers)
        {
            await this.AddForPlayerAsync(player, GuildXpSource.EventFinish).ConfigureAwait(false);
        }

        if (winner is not null)
        {
            await this.AddForPlayerAsync(winner, GuildXpSource.EventWin).ConfigureAwait(false);
        }

        this.Save();
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
            gameContext.LoggerFactory.CreateLogger<GuildsPlugIn>().LogWarning(ex, "Guilds: the name of the castle siege winner wasn't read.");
        }

        var key = new GuildKey(guildId, name ?? string.Empty);
        var result = this.GetEngine().RecordCastleSiege(guildId, name, DateTime.UtcNow);
        this.Save();
        await this.ForEachMemberAsync(key, player => this.SendStateAsync(player, false, new GuildStateEvent("siege")).AsTask()).ConfigureAwait(false);
        if (result.LeveledUp)
        {
            await this.LevelUpAsync(gameContext, key, result.NewLevel).ConfigureAwait(false);
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
                var key = await GuildOfAsync(player).ConfigureAwait(false);
                await this.RefreshBuffAsync(player, key, DateTime.UtcNow).ConfigureAwait(false);
                await this.DeliverAndTellAsync(player).ConfigureAwait(false);
                this.Save();
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Guilds: the guild buff or the waiting rewards of {Player} weren't given.", player);
            }
        });
        return ValueTask.CompletedTask;
    }

    private static byte ServerIdOf(IGameContext context) => (context as IGameServerContext)?.Id ?? 0;

    private static bool IsCounted(Player player) =>
        player is not OfflinePlayer && player.Account is { IsBot: false } && !player.IsTemplatePlayer && player.SelectedCharacter is not null;

    private static bool TryGetKeys(Player player, out string characterId, out string login)
    {
        characterId = player.SelectedCharacter?.Id.ToString() ?? string.Empty;
        login = player.Account?.LoginName ?? string.Empty;
        return characterId.Length > 0 && login.Length > 0 && player.SelectedCharacter!.Id != Guid.Empty;
    }

    private static bool IsVietnamese(Player player) => player.Culture.TwoLetterISOLanguageName == "vi";

    /// <summary>
    /// Gets the persistent guild of a player (cached for a while per guild server id).
    /// </summary>
    private static async ValueTask<GuildKey?> GuildOfAsync(Player player)
    {
        if (player.GuildStatus is not { GuildId: > 0 } status || player.GameContext is not IGameServerContext serverContext)
        {
            return null;
        }

        if (GuildKeys.TryGetValue(status.GuildId, out var cached) && DateTime.UtcNow - cached.AtUtc < GuildKeyLifetime)
        {
            return cached.Key;
        }

        if (await serverContext.GuildServer.GetPersistentGuildIdAsync(status.GuildId).ConfigureAwait(false) is not { } guildId)
        {
            return null;
        }

        var name = await serverContext.GuildServer.GetPersistentGuildNameAsync(guildId).ConfigureAwait(false) ?? string.Empty;
        var key = new GuildKey(guildId, name);
        GuildKeys[status.GuildId] = (key, DateTime.UtcNow);
        return key;
    }

    private static bool SameBuff(MagicEffect existing, MagicEffect.ElementWithTarget[] wanted)
    {
        var current = existing.PowerUpElements.ToList();
        return current.Count == wanted.Length
            && current.Zip(wanted).All(p => p.First.Target == p.Second.Target
                && p.First.Element.AggregateType == p.Second.Element.AggregateType
                && Math.Abs(p.First.Element.Value - p.Second.Element.Value) < 0.0001f);
    }

    /// <summary>
    /// Gives the reward's zen and items to the character, all or nothing.
    /// </summary>
    internal static async ValueTask<bool> GiveGoodsAsync(Player player, MissionReward reward)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var added = new List<Item>();
        async ValueTask TakeBackAsync()
        {
            foreach (var back in added)
            {
                await inventory.RemoveItemAsync(back).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(back).ConfigureAwait(false);
            }
        }

        foreach (var entry in reward.Items.Where(i => i.Count > 0))
        {
            var definition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == entry.Group && d.Number == entry.Number);
            if (definition is null)
            {
                player.Logger.LogWarning("Guilds: the reward item {Group} {Number} doesn't exist; it's left out.", entry.Group, entry.Number);
                continue;
            }

            for (var i = 0; i < entry.Count; i++)
            {
                var item = player.PersistenceContext.CreateNew<Item>();
                item.Definition = definition;
                item.Level = Math.Min(entry.Level, definition.MaximumItemLevel);
                item.Durability = item.GetMaximumDurabilityOfOnePiece();
                if (!await inventory.AddItemAsync(item).ConfigureAwait(false))
                {
                    await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                    await TakeBackAsync().ConfigureAwait(false);
                    return false;
                }

                added.Add(item);
            }
        }

        if (reward.Zen > 0 && !player.TryAddMoney(reward.Zen))
        {
            await TakeBackAsync().ConfigureAwait(false);
            return false;
        }

        foreach (var item in added)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (reward.Zen > 0)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        return true;
    }

    private static async ValueTask<Player?> FindPlayerAsync(string characterId)
    {
        foreach (var context in Contexts.Values.Distinct())
        {
            var player = (await context.GetPlayersAsync().ConfigureAwait(false)).FirstOrDefault(p => p.SelectedCharacter?.Id.ToString() == characterId && IsCounted(p));
            if (player is not null)
            {
                return player;
            }
        }

        return null;
    }

    private async ValueTask TickPlayerAsync(Player player, GuildEngine engine, DateTime now)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _) || player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            return;
        }

        var key = await GuildOfAsync(player).ConfigureAwait(false);
        if (key is not null)
        {
            var minutes = this._onlineMinutes.AddOrUpdate(characterId, 1, (_, m) => m + 1);
            if (minutes >= Math.Max(1, engine.Configuration.OnlineMinutes))
            {
                this._onlineMinutes[characterId] = 0;
                await this.AddForPlayerAsync(player, GuildXpSource.OnlineTime).ConfigureAwait(false);
            }
        }

        await this.RefreshBuffAsync(player, key, now).ConfigureAwait(false);
        if (engine.Owed(characterId).Count > 0)
        {
            await this.DeliverAndTellAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives, keeps or takes away the guild buff of a player: the buff of the guild's level while the player is in the
    /// guild; it lasts a few minutes and every tick renews it, so it ends soon after the player leaves the guild or the
    /// plug-in is switched off.
    /// </summary>
    private async ValueTask RefreshBuffAsync(Player player, GuildKey? key, DateTime now)
    {
        if (player.Attributes is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var buff = key is null ? null : engine.BuffOf(engine.Progress(key.GuildId, now)?.Level ?? 1);
        player.MagicEffectList.TryGetEffect(BuffEffectNumber, out var existing);
        if (buff is null)
        {
            if (existing is not null)
            {
                await existing.DisposeAsync().ConfigureAwait(false);
            }

            return;
        }

        var wanted = BuffElements(buff);
        if (existing is not null && SameBuff(existing, wanted))
        {
            existing.ResetTimer();
            return;
        }

        if (existing is not null)
        {
            await existing.DisposeAsync().ConfigureAwait(false);
        }

        await player.MagicEffectList.AddEffectAsync(new MagicEffect(BuffDuration, BuffDefinition, wanted)).ConfigureAwait(false);
    }

    private async ValueTask ForEachMemberAsync(GuildKey key, Func<Player, Task> action)
    {
        foreach (var context in Contexts.Values.Distinct())
        {
            foreach (var player in await context.GetPlayersAsync().ConfigureAwait(false))
            {
                if (!IsCounted(player) || await GuildOfAsync(player).ConfigureAwait(false) is not { } mine || mine.GuildId != key.GuildId)
                {
                    continue;
                }

                try
                {
                    await action(player).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    player.Logger.LogDebug(ex, "Guilds: a member wasn't told.");
                }
            }
        }
    }

    private async ValueTask LevelUpAsync(IGameContext gameContext, GuildKey key, int level)
    {
        gameContext.LoggerFactory.CreateLogger<GuildsPlugIn>().LogInformation("Guilds: {Guild} reached level {Level}.", key.Name, level);
        var announce = this.GetEngine().Configuration.AnnounceInGame;
        var now = DateTime.UtcNow;
        if (HNMO.GameLogic.PlugIns.Community.CommunityPlugIn.Get(gameContext) is { } community)
        {
            await community.GuildLevelUpAsync(gameContext, key.Name, level).ConfigureAwait(false);
        }

        await this.ForEachMemberAsync(key, async player =>
        {
            if (announce)
            {
                var text = IsVietnamese(player)
                    ? string.Format(CultureInfo.InvariantCulture, "Bang hội {0} đã lên cấp {1}!", key.Name, level)
                    : string.Format(CultureInfo.InvariantCulture, "Your guild {0} reached level {1}!", key.Name, level);
                await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(text, MessageType.GuildNotice)).ConfigureAwait(false);
            }

            await this.RefreshBuffAsync(player, key, now).ConfigureAwait(false);
            await this.SendStateAsync(player, false, new GuildStateEvent("levelup", Level: level, InGame: announce)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the waiting rewards of a player (zen and items) when they fit.
    /// </summary>
    private async ValueTask<(int Given, int Left)> DeliverAsync(Player player)
    {
        if (!TryGetKeys(player, out var characterId, out _))
        {
            return (0, 0);
        }

        var engine = this.GetEngine();
        var keep = new List<GuildOwedReward>();
        var given = 0;
        foreach (var reward in engine.TakeOwed(characterId))
        {
            try
            {
                if (await GiveGoodsAsync(player, reward.Reward).ConfigureAwait(false))
                {
                    given++;
                    player.Logger.LogInformation("Guilds: {Player} got the reward {Reference}.", player, reward.Reference);
                }
                else
                {
                    keep.Add(reward);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Guilds: the reward {Reference} of {Player} wasn't given; it waits.", reward.Reference, player);
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(characterId, keep);
        return (given, keep.Count);
    }

    private async ValueTask DeliverAndTellAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        if (given > 0 || left > 0)
        {
            await this.SendStateAsync(player, false, new GuildStateEvent("goods", left > 0 ? "BagFull" : "Ok", Coins: given)).ConfigureAwait(false);
        }
    }

    private async ValueTask PayWeeksAsync(IGameContext gameContext, DateTime now)
    {
        var engine = this.GetEngine();
        var logger = gameContext.LoggerFactory.CreateLogger<GuildsPlugIn>();
        foreach (var week in engine.TakeUnpaidWeeks(now))
        {
            try
            {
                foreach (var line in week.Lines)
                {
                    if (engine.RewardOf(line.Rank, line.Xp) is not { } reward)
                    {
                        continue;
                    }

                    line.PaidMembers = await this.PayGuildAsync(gameContext, engine, week, line, reward).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // It's tried again on the next tick; the references keep it from paying twice.
                logger.LogWarning(ex, "Guilds: the ladder of the week {Week} wasn't paid yet.", week.Week);
                continue;
            }

            engine.MarkPaid(week);
            logger.LogInformation("Guilds: the guild ladder of the week of {Week} was paid.", week.Week);
            if (HNMO.GameLogic.PlugIns.Community.CommunityPlugIn.Get(gameContext) is { } community)
            {
                foreach (var line in week.Lines.OrderBy(l => l.Rank))
                {
                    await community.GuildLadderWinnerAsync(gameContext, line.Name, line.Rank, line.Xp).ConfigureAwait(false);
                }
            }
        }
    }

    private async ValueTask<int> PayGuildAsync(IGameContext gameContext, GuildEngine engine, GuildWeekResult week, GuildLadderLine line, GuildLadderReward reward)
    {
        IReadOnlyDictionary<Guid, string> members;
        string? master;
        using (var guilds = gameContext.PersistenceContextProvider.CreateNewGuildContext())
        {
            members = await guilds.GetMemberNamesAsync(line.GuildId).ConfigureAwait(false);
            var masters = await guilds.GetGuildMasterNamesAsync().ConfigureAwait(false);
            masters.TryGetValue(line.GuildId, out master);
        }

        var paid = 0;
        foreach (var (memberId, name) in members)
        {
            var reference = GuildEngine.LadderReference(week.Week, line.GuildId, memberId);
            if (week.PaidReferences.Contains(reference))
            {
                paid++;
                continue;
            }

            string? login;
            using (var players = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration))
            {
                login = (await players.GetAccountByCharacterNameAsync(name).ConfigureAwait(false))?.LoginName;
            }

            if (login is null)
            {
                continue;
            }

            var grant = GuildEngine.LadderGrant(week.Week, line, reward, memberId, name, login, string.Equals(name, master, StringComparison.Ordinal));
            if (grant is not null)
            {
                await MissionCoinGranter.GrantAsync(gameContext, grant, GrantedBy).ConfigureAwait(false);
            }

            if (reward.ZenPerMember > 0 || reward.Items.Any(i => i.Count > 0))
            {
                engine.AddOwed(memberId.ToString(), new GuildOwedReward
                {
                    Reference = reference + ":goods",
                    Reason = $"Guild ladder, week of {week.Week}: rank {line.Rank} ({line.Name})",
                    Week = week.Week,
                    Rank = line.Rank,
                    Reward = new MissionReward { Zen = reward.ZenPerMember, Items = reward.Items.Where(i => i.Count > 0).ToList() },
                });
            }

            engine.NotePaid(week, reference);
            paid++;
            this.Save();
            if (await FindPlayerAsync(memberId.ToString()).ConfigureAwait(false) is { } player)
            {
                await this.SendStateAsync(player, false, new GuildStateEvent("ladder", "Ok", Rank: line.Rank, Coins: grant?.Amount ?? 0, Week: week.Week)).ConfigureAwait(false);
                await this.DeliverAndTellAsync(player).ConfigureAwait(false);
            }
        }

        return paid;
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
