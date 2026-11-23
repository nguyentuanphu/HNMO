// <copyright file="WorldBossesPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CastleSiege;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// HNMO's world bosses: strong monsters which appear on a schedule (Sydney time) on a map, announced before, at the
/// spawn and at the kill in the game (golden notice and the web game's chat), on Discord and on the website. The damage
/// of every player (or party) is counted; rewards (zen, items, coins) go by the damage rank, to everyone with enough
/// damage and to the last hit; players without damage get nothing. Rewards which don't fit the bag wait and are given
/// later (or with the window's button); coins are given at once. Kills give boss-hunt points for a weekly ranking; the
/// weekly rankings (boss hunt, Blood Castle, Devil Square, Chaos Castle, the castle siege winners) pay coins once per
/// week. The state is kept in a JSON file in the save folder.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.WorldBossesPlugIn_Name), Description = nameof(PlugInResources.WorldBossesPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D2C7A915-48E3-4B6F-9A1D-7E0B5C3F8A64")]
public class WorldBossesPlugIn : IPeriodicTaskPlugIn, IAttackableGotHitPlugIn, IAttackableGotKilledPlugIn, IMiniGameEndedPlugIn,
    ICastleSiegeStateChangedPlugIn, IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<WorldBossesConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>The name in the coins history's "granted by" column.</summary>
    public const string GrantedBy = "HNMO world bosses";

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DeliveryInterval = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();
    private static readonly ConcurrentDictionary<string, Monster> Monsters = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<Monster, string> Instances = new();
    private static readonly SemaphoreSlim SpawnLock = new(1, 1);

    private readonly ConcurrentDictionary<Player, DateTime> _fighting = new();
    private DateTime _nextTickUtc = DateTime.UtcNow;
    private DateTime _nextDeliveryUtc = DateTime.UtcNow + DeliveryInterval;
    private int _running;
    private (WorldBossesConfiguration Configuration, WorldBossEngine Engine)? _engine;

    /// <inheritdoc />
    public WorldBossesConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static WorldBossesPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<WorldBossesPlugIn>().FirstOrDefault();

    /// <summary>
    /// Spawns a boss now (the admin panel's "spawn now"), on the game server which hosts it.
    /// </summary>
    /// <param name="gameContexts">The game contexts of this process.</param>
    /// <param name="bossId">The boss id.</param>
    /// <returns>"Ok", "Alive" (it's on a map already), "NotFound" (no such boss or no game server hosts it) or "Failed".</returns>
    public static async ValueTask<string> SpawnNowAsync(IEnumerable<IGameContext> gameContexts, string bossId)
    {
        var contexts = gameContexts.ToList();
        foreach (var context in contexts)
        {
            Contexts.TryAdd(ServerIdOf(context), context);
        }

        foreach (var context in contexts)
        {
            if (Get(context) is not { } plugIn || plugIn.GetEngine().Boss(bossId) is not { } boss || plugIn.HostOf(boss) != ServerIdOf(context))
            {
                continue;
            }

            var engine = plugIn.GetEngine();
            if (engine.AllAlive().Any(a => a.BossId == boss.Id))
            {
                return "Alive";
            }

            var alive = await plugIn.SpawnAsync(context, boss).ConfigureAwait(false);
            plugIn.Save();
            return alive is null ? "Failed" : "Ok";
        }

        return "NotFound";
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new WorldBossesConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public WorldBossEngine GetEngine()
    {
        var configuration = this.Configuration ??= new WorldBossesConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new WorldBossEngine(configuration, WorldBossStore.Open(configuration.FileName));
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>Gets the enabled boss definitions using the same host selection as the scheduler.</summary>
    /// <param name="serverId">The hosting game server.</param>
    /// <returns>The definitions without player or encounter data.</returns>
    public IEnumerable<WorldBossDefinition> GetHostedBosses(byte serverId)
        => this.GetHostedBosses(this.Configuration ?? new WorldBossesConfiguration(), serverId);

    /// <summary>
    /// Sends the state to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="boardEvent">What happened.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, WorldBossBoardEvent? boardEvent = null)
    {
        if (!TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var json = WorldBossBoardWriter.WriteBoard(this.GetEngine(), characterId, DateTime.UtcNow, map => MapName(player.GameContext, map, player.Culture), boardEvent);
        await player.InvokeViewPlugInAsync<IHnmoWorldBossesViewPlugIn>(p => p.ShowWorldBossesAsync(json, requested)).ConfigureAwait(false);
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
        await this.SendStateAsync(player, true, new WorldBossBoardEvent("claim", left > 0 ? "BagFull" : given > 0 ? "Ok" : "Nothing")).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (now < this._nextTickUtc || Interlocked.Exchange(ref this._running, 1) == 1)
        {
            return;
        }

        try
        {
            this._nextTickUtc = now + TickInterval;
            var serverId = ServerIdOf(gameContext);
            Contexts[serverId] = gameContext;
            var engine = this.GetEngine();
            await this.CheckMonstersAsync(gameContext, serverId, now).ConfigureAwait(false);

            var hosted = this.GetHostedBosses(engine.Configuration, serverId).ToList();
            foreach (var action in engine.Plan(hosted, serverId, now))
            {
                try
                {
                    switch (action.Kind)
                    {
                        case WorldBossActionKind.Soon:
                            await this.AnnounceAsync(gameContext, action.Boss!, "soon", action.AtUtc, new Dictionary<string, string> { ["minutes"] = Math.Max(1, (int)Math.Ceiling((action.AtUtc - now).TotalMinutes)).ToString(CultureInfo.InvariantCulture) }).ConfigureAwait(false);
                            break;
                        case WorldBossActionKind.Spawn:
                            await this.SpawnAsync(gameContext, action.Boss!).ConfigureAwait(false);
                            break;
                        case WorldBossActionKind.Despawn:
                            await this.DespawnAsync(gameContext, action.InstanceId!, action.Boss).ConfigureAwait(false);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogError(ex, "World bosses: {Action} of {Boss} failed.", action.Kind, action.Boss?.Id);
                }
            }

            if (serverId == Contexts.Keys.Min())
            {
                await this.PayWeeksAsync(gameContext, now).ConfigureAwait(false);
            }

            await this.PushFightersAsync(now).ConfigureAwait(false);
            if (now >= this._nextDeliveryUtc)
            {
                this._nextDeliveryUtc = now + DeliveryInterval;
                foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
                {
                    if (IsCounted(player) && TryGetKeys(player, out var characterId, out _) && engine.Owed(characterId).Count > 0)
                    {
                        await this.DeliverAndTellAsync(player, false).ConfigureAwait(false);
                    }
                }
            }

            this.Save();
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogError(ex, "World bosses: the tick failed.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextTickUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public void AttackableGotHit(IAttackable attackable, IAttacker attacker, HitInfo hitInfo)
    {
        if (attackable is not Monster monster || !Instances.TryGetValue(monster, out var instanceId))
        {
            return;
        }

        var player = attacker as Player ?? (attacker as IPlayerSurrogate)?.Owner ?? (attacker as Monster)?.SummonedBy;
        if (player is null || !IsCounted(player) || !TryGetKeys(player, out var characterId, out var login))
        {
            return;
        }

        var party = (player.Party?.PartyMaster as Player)?.SelectedCharacter?.Name;
        if (this.GetEngine().RecordDamage(instanceId, characterId, player.SelectedCharacter!.Name, login, party, hitInfo.TotalDamage))
        {
            this._fighting[player] = DateTime.UtcNow;
        }
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Monster monster || !Instances.TryGetValue(monster, out var instanceId))
        {
            return;
        }

        var player = killer as Player ?? (killer as IPlayerSurrogate)?.Owner ?? (killer as Monster)?.SummonedBy;
        var characterId = player?.SelectedCharacter?.Id.ToString();
        await this.EndWithKillAsync(player?.GameContext ?? Contexts.Values.FirstOrDefault(), instanceId, characterId, player?.SelectedCharacter?.Name).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask MiniGameEndedAsync(MiniGameContext miniGame, Player? winner, IReadOnlyCollection<Player> finishers)
    {
        var category = miniGame.Definition.Type switch
        {
            MiniGameType.BloodCastle => WeeklyCategory.BloodCastle,
            MiniGameType.DevilSquare => WeeklyCategory.DevilSquare,
            MiniGameType.ChaosCastle => WeeklyCategory.ChaosCastle,
            _ => (WeeklyCategory?)null,
        };
        var engine = this.GetEngine();
        if (category is null || engine.Configuration.Ranking(category.Value) is not { Enabled: true } ranking)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var player in finishers.Append(winner).OfType<Player>().Distinct())
        {
            if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out var login))
            {
                continue;
            }

            var points = (finishers.Contains(player) ? ranking.PointsFinish : 0) + (player == winner ? ranking.PointsWin : 0);
            engine.AddPoints(category.Value, characterId, player.SelectedCharacter!.Name, login, points, now);
        }

        this.Save();
        await ValueTask.CompletedTask.ConfigureAwait(false);
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
            gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogWarning(ex, "World bosses: the name of the castle siege winner wasn't read.");
        }

        this.GetEngine().SetCastleSiegeWinner(guildId, name, DateTime.UtcNow);
        this.Save();
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
                await this.DeliverAndTellAsync(player, false).ConfigureAwait(false);
                this.Save();
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "World bosses: the waiting rewards of {Player} weren't given.", player);
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

    private static string MapName(IGameContext context, short map, CultureInfo culture)
    {
        var definition = context.Configuration.Maps.FirstOrDefault(m => m.Number == map);
        return definition?.Name.GetTranslation(culture) ?? definition?.Name.ValueInNeutralLanguage ?? map.ToString(CultureInfo.InvariantCulture);
    }

    private static List<(byte X, byte Y)> ParseSpots(string? text)
    {
        var spots = new List<(byte X, byte Y)>();
        foreach (var part in (text ?? string.Empty).Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var xy = part.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length == 2 && byte.TryParse(xy[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) && byte.TryParse(xy[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                spots.Add((x, y));
            }
        }

        return spots;
    }

    private static Point? PickSpot(GameMap map, WorldBossDefinition boss)
    {
        var spots = ParseSpots(boss.SpawnSpots);
        if (spots.Count > 0)
        {
            var (x, y) = spots[Random.Shared.Next(spots.Count)];
            return new Point(x, y);
        }

        var terrain = map.Terrain;
        int x1 = Math.Min(boss.AreaX1, boss.AreaX2), x2 = Math.Max(boss.AreaX1, boss.AreaX2);
        int y1 = Math.Min(boss.AreaY1, boss.AreaY2), y2 = Math.Max(boss.AreaY1, boss.AreaY2);
        for (var attempt = 0; attempt < 400; attempt++)
        {
            var x = Random.Shared.Next(x1, x2 + 1);
            var y = Random.Shared.Next(y1, y2 + 1);
            if (terrain.WalkMap[x, y] && !terrain.SafezoneMap[x, y])
            {
                return new Point((byte)x, (byte)y);
            }
        }

        return terrain.RandomWalkableCoordinate;
    }

    /// <summary>
    /// Gives the reward's zen and items to the character, all or nothing (coins aren't part of it).
    /// </summary>
    private static async ValueTask<bool> GiveGoodsAsync(Player player, MissionReward reward)
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
                player.Logger.LogWarning("World bosses: the reward item {Group} {Number} doesn't exist; it's left out.", entry.Group, entry.Number);
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
        foreach (var context in Contexts.Values)
        {
            var player = (await context.GetPlayersAsync().ConfigureAwait(false)).FirstOrDefault(p => p.SelectedCharacter?.Id.ToString() == characterId && IsCounted(p));
            if (player is not null)
            {
                return player;
            }
        }

        return null;
    }

    private static async ValueTask ForEachPlayerAsync(Func<Player, Task> action)
    {
        foreach (var context in Contexts.Values.Distinct())
        {
            await context.ForEachPlayerAsync(action).ConfigureAwait(false);
        }
    }

    private IEnumerable<WorldBossDefinition> GetHostedBosses(WorldBossesConfiguration configuration, byte serverId)
        => configuration.EnabledBosses().Where(boss => this.HostOf(boss) == serverId);

    private byte HostOf(WorldBossDefinition boss) =>
        boss.GameServerId >= 0 ? (byte)boss.GameServerId : (Contexts.IsEmpty ? (byte)0 : Contexts.Keys.Min());

    /// <summary>
    /// Ends appearances whose monster isn't there: lost by a restart of the server (no rewards), or dead without a
    /// kill notice.
    /// </summary>
    private async ValueTask CheckMonstersAsync(IGameContext gameContext, byte serverId, DateTime now)
    {
        var engine = this.GetEngine();
        foreach (var alive in engine.AllAlive().Where(a => a.ServerId == serverId))
        {
            await SpawnLock.WaitAsync().ConfigureAwait(false);
            Monster? monster;
            try
            {
                if (!Monsters.TryGetValue(alive.InstanceId, out monster))
                {
                    engine.Escape(alive.InstanceId, now, false);
                    gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogInformation("World bosses: {Instance} was lost (the server restarted).", alive.InstanceId);
                    continue;
                }
            }
            finally
            {
                SpawnLock.Release();
            }

            if (!monster.IsAlive && monster.LastDeath is { } death)
            {
                await this.EndWithKillAsync(gameContext, alive.InstanceId, null, death.KillerName).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<WorldBossAliveState?> SpawnAsync(IGameContext gameContext, WorldBossDefinition boss)
    {
        var logger = gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>();
        if (gameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == boss.MonsterNumber) is not { } definition)
        {
            logger.LogWarning("World bosses: the monster {Number} of {Boss} doesn't exist.", boss.MonsterNumber, boss.Id);
            return null;
        }

        if (await gameContext.GetMapAsync((ushort)boss.MapNumber).ConfigureAwait(false) is not { } map)
        {
            logger.LogWarning("World bosses: the map {Map} of {Boss} doesn't exist.", boss.MapNumber, boss.Id);
            return null;
        }

        if (PickSpot(map, boss) is not { } spot)
        {
            logger.LogWarning("World bosses: no free spot for {Boss} on map {Map}.", boss.Id, boss.MapNumber);
            return null;
        }

        var area = new MonsterSpawnArea
        {
            GameMap = map.Definition,
            MonsterDefinition = definition,
            SpawnTrigger = SpawnTrigger.OnceAtEventStart,
            Quantity = 1,
            X1 = spot.X,
            X2 = spot.X,
            Y1 = spot.Y,
            Y2 = spot.Y,
        };
        var intelligence = new BasicMonsterIntelligence();
        var monster = new Monster(area, definition, map, gameContext.DropGenerator, intelligence, gameContext.PlugInManager, gameContext.PathFinderPool);
        monster.Initialize();
        WorldBossAliveState alive;
        await SpawnLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await map.AddAsync(monster).ConfigureAwait(false);
            monster.OnSpawn();
            alive = this.GetEngine().RegisterSpawn(boss, ServerIdOf(gameContext), spot.X, spot.Y, (long)monster.Attributes[Stats.MaximumHealth], DateTime.UtcNow);
            Monsters[alive.InstanceId] = monster;
            Instances[monster] = alive.InstanceId;
        }
        finally
        {
            SpawnLock.Release();
        }

        logger.LogInformation("World bosses: {Boss} appeared on map {Map} at {X}, {Y} ({Instance}).", boss.Id, boss.MapNumber, spot.X, spot.Y, alive.InstanceId);
        await this.AnnounceAsync(gameContext, boss, "spawn", alive.SpawnedAtUtc, new Dictionary<string, string>
        {
            ["x"] = spot.X.ToString(CultureInfo.InvariantCulture),
            ["y"] = spot.Y.ToString(CultureInfo.InvariantCulture),
            ["minutes"] = Math.Max(1, boss.DespawnMinutes).ToString(CultureInfo.InvariantCulture),
        }).ConfigureAwait(false);
        return alive;
    }

    private async ValueTask DespawnAsync(IGameContext gameContext, string instanceId, WorldBossDefinition? boss)
    {
        if (Monsters.TryRemove(instanceId, out var monster))
        {
            Instances.TryRemove(monster, out _);
            if (!monster.IsDisposed)
            {
                await monster.CurrentMap.RemoveAsync(monster).ConfigureAwait(false);
                monster.Dispose();
            }
        }

        var record = this.GetEngine().Escape(instanceId, DateTime.UtcNow);
        if (record is not null && boss is not null)
        {
            await this.AnnounceAsync(gameContext, boss, "escape", record.EndedAtUtc).ConfigureAwait(false);
        }
    }

    private async ValueTask EndWithKillAsync(IGameContext? gameContext, string instanceId, string? lastHitCharacterId, string? killerName)
    {
        if (Monsters.TryRemove(instanceId, out var monster))
        {
            Instances.TryRemove(monster, out _);
        }

        var engine = this.GetEngine();
        var outcome = engine.Kill(instanceId, lastHitCharacterId, DateTime.UtcNow);
        if (outcome is null)
        {
            return;
        }

        var record = outcome.Record;
        gameContext ??= Contexts.Values.FirstOrDefault();
        gameContext?.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogInformation("World bosses: {Instance} was killed; {Count} players did damage.", instanceId, record.Lines.Count);
        if (engine.Boss(record.BossId) is { } boss && gameContext is not null)
        {
            await this.AnnounceAsync(gameContext, boss, "kill", record.EndedAtUtc, new Dictionary<string, string>
            {
                ["killer"] = record.LastHit ?? killerName ?? "-",
                ["top"] = record.Lines.FirstOrDefault()?.Name ?? "-",
            }).ConfigureAwait(false);
        }

        if (gameContext is not null && HNMO.GameLogic.PlugIns.Community.CommunityPlugIn.Get(gameContext) is { } community)
        {
            try
            {
                await community.WorldBossKilledAsync(gameContext, record).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogWarning(ex, "World bosses: the community post of the kill wasn't made.");
            }
        }

        if (gameContext is not null && HNMO.GameLogic.PlugIns.TopRaces.TopRacesPlugIn.Get(gameContext) is { } topRaces)
        {
            try
            {
                await topRaces.WorldBossKilledAsync(gameContext, record).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogWarning(ex, "World bosses: the top races' boss-hunt points of the kill weren't counted.");
            }
        }

        await HNMO.GameLogic.PlugIns.SeasonPlan.SeasonPlanPlugIn.NotifyBossKilledAsync(gameContext, record.BossId).ConfigureAwait(false);

        if (gameContext is not null && HNMO.GameLogic.PlugIns.Guilds.GuildsPlugIn.Get(gameContext) is { } guilds)
        {
            try
            {
                await guilds.WorldBossKilledAsync(gameContext, record).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogWarning(ex, "World bosses: the guild experience of the kill wasn't added.");
            }
        }

        foreach (var characterId in outcome.Rewards.Keys)
        {
            if (await FindPlayerAsync(characterId).ConfigureAwait(false) is { } player)
            {
                await this.DeliverAndTellAsync(player, true).ConfigureAwait(false);
            }
            else if (gameContext is not null)
            {
                await this.GrantCoinsOnlyAsync(gameContext, characterId).ConfigureAwait(false);
            }
        }

        this.Save();
    }

    /// <summary>
    /// Gives the coins of the waiting rewards of a character who isn't in the game (the zen and items keep waiting).
    /// </summary>
    private async ValueTask GrantCoinsOnlyAsync(IGameContext gameContext, string characterId)
    {
        var engine = this.GetEngine();
        var owed = engine.TakeOwed(characterId);
        var keep = new List<WorldBossOwedReward>();
        foreach (var reward in owed)
        {
            if (!reward.CoinsGiven && reward.Reward.Coins > 0)
            {
                await MissionCoinGranter.GrantAsync(gameContext, new CoinGrant(reward.Login, reward.Reward.CoinType, reward.Reward.Coins, reward.Reason, reward.Reference), GrantedBy).ConfigureAwait(false);
                reward.CoinsGiven = true;
            }

            if (reward.Reward.Zen > 0 || reward.Reward.Items.Any(i => i.Count > 0))
            {
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(characterId, keep);
    }

    /// <summary>
    /// Gives the waiting rewards of a player: the coins at once, the zen and items when they fit.
    /// </summary>
    /// <returns>The rewards given completely and the ones which still wait.</returns>
    private async ValueTask<(int Given, int Left)> DeliverAsync(Player player)
    {
        if (!TryGetKeys(player, out var characterId, out _))
        {
            return (0, 0);
        }

        var engine = this.GetEngine();
        var owed = engine.TakeOwed(characterId);
        var keep = new List<WorldBossOwedReward>();
        var given = 0;
        foreach (var reward in owed)
        {
            try
            {
                if (!reward.CoinsGiven && reward.Reward.Coins > 0)
                {
                    await MissionCoinGranter.GrantAsync(player.GameContext, new CoinGrant(reward.Login, reward.Reward.CoinType, reward.Reward.Coins, reward.Reason, reward.Reference), GrantedBy).ConfigureAwait(false);
                    reward.CoinsGiven = true;
                }

                var hasGoods = reward.Reward.Zen > 0 || reward.Reward.Items.Any(i => i.Count > 0);
                if (!hasGoods || await GiveGoodsAsync(player, reward.Reward).ConfigureAwait(false))
                {
                    given++;
                    player.Logger.LogInformation("World bosses: {Player} got the reward {Reference}.", player, reward.Reference);
                }
                else
                {
                    keep.Add(reward);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "World bosses: the reward {Reference} of {Player} wasn't given; it waits.", reward.Reference, player);
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(characterId, keep);
        return (given, keep.Count);
    }

    private async ValueTask DeliverAndTellAsync(Player player, bool tellFullBag)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        if (given > 0 || (left > 0 && tellFullBag))
        {
            await this.SendStateAsync(player, false, new WorldBossBoardEvent("reward", left > 0 ? "BagFull" : "Ok", Count: given)).ConfigureAwait(false);
        }
    }

    private async ValueTask AnnounceAsync(IGameContext gameContext, WorldBossDefinition boss, string phase, DateTime atUtc, Dictionary<string, string>? values = null)
    {
        var configuration = this.GetEngine().Configuration;
        var (en, vi) = phase switch
        {
            "soon" => (boss.SoonTextEn, boss.SoonTextVi),
            "spawn" => (boss.SpawnTextEn, boss.SpawnTextVi),
            "kill" => (boss.KillTextEn, boss.KillTextVi),
            _ => (boss.EscapeTextEn, boss.EscapeTextVi),
        };
        var english = CultureInfo.GetCultureInfo("en");
        var vietnamese = CultureInfo.GetCultureInfo("vi");
        var textEn = WorldBossEngine.Fill(en, boss.NameEn, MapName(gameContext, boss.MapNumber, english), values);
        var textVi = WorldBossEngine.Fill(string.IsNullOrWhiteSpace(vi) ? en : vi, string.IsNullOrWhiteSpace(boss.NameVi) ? boss.NameEn : boss.NameVi, MapName(gameContext, boss.MapNumber, vietnamese), values);
        this.GetEngine().AddAnnouncement(new WorldBossAnnouncement { AtUtc = DateTime.UtcNow, BossId = boss.Id, Phase = phase, TextEn = textEn, TextVi = textVi });

        // InGame: the golden notice already shows the text; the web game then adds no chat line of its own.
        var boardEvent = new WorldBossBoardEvent(phase, BossId: boss.Id, TextEn: textEn, TextVi: textVi, InGame: configuration.AnnounceInGame);
        await ForEachPlayerAsync(async player =>
        {
            try
            {
                if (configuration.AnnounceInGame && player.SelectedCharacter is not null)
                {
                    var text = IsVietnamese(player) ? textVi : textEn;
                    await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(text, MessageType.GoldenCenter)).ConfigureAwait(false);
                }

                await this.SendStateAsync(player, false, boardEvent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogDebug(ex, "World bosses: the announcement wasn't shown.");
            }
        }).ConfigureAwait(false);

        if (configuration.AnnounceOnDiscord && gameContext is IGameServerContext serverContext)
        {
            try
            {
                await serverContext.EventPublisher.GameEventAsync(new WorldBossAnnouncedEvent(serverContext.Id, DateTime.UtcNow, phase, boss.NameEn, string.IsNullOrWhiteSpace(boss.NameVi) ? boss.NameEn : boss.NameVi, textEn, textVi, atUtc)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>().LogWarning(ex, "World bosses: the Discord post failed.");
            }
        }
    }

    /// <summary>
    /// Sends the state to the players who hit a boss in the last minute (their damage and rank change).
    /// </summary>
    private async ValueTask PushFightersAsync(DateTime now)
    {
        foreach (var (player, at) in this._fighting.ToList())
        {
            if (now - at > TimeSpan.FromMinutes(1) || player.PlayerState.CurrentState.IsDisconnectedOrFinished())
            {
                this._fighting.TryRemove(player, out _);
                continue;
            }

            try
            {
                await this.SendStateAsync(player, false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogDebug(ex, "World bosses: the state wasn't sent.");
            }
        }
    }

    private async ValueTask PayWeeksAsync(IGameContext gameContext, DateTime now)
    {
        var engine = this.GetEngine();
        var logger = gameContext.LoggerFactory.CreateLogger<WorldBossesPlugIn>();
        foreach (var week in engine.TakeUnpaidWeeks(now))
        {
            foreach (var grant in WorldBossEngine.RankingGrants(week))
            {
                await MissionCoinGranter.GrantAsync(gameContext, grant, GrantedBy).ConfigureAwait(false);
            }

            var siege = engine.Configuration.CastleSiege;
            if (siege.Enabled && week.CastleSiegeGuildId is { } guildId && (siege.CoinsPerMember > 0 || siege.GuildMasterBonus > 0))
            {
                try
                {
                    week.CastleSiegeMembers = await this.PayCastleSiegeAsync(gameContext, week.Week, guildId, siege).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // It's tried again on the next tick; the references keep it from paying twice.
                    logger.LogWarning(ex, "World bosses: the castle siege coins of the week {Week} weren't paid yet.", week.Week);
                    continue;
                }
            }

            engine.MarkPaid(week);
            logger.LogInformation("World bosses: the weekly rankings of the week of {Week} were paid.", week.Week);
        }
    }

    private async ValueTask<List<WeeklyWinner>> PayCastleSiegeAsync(IGameContext gameContext, string week, Guid guildId, CastleSiegeWeeklyConfiguration siege)
    {
        IReadOnlyDictionary<Guid, string> members;
        string? master;
        using (var guilds = gameContext.PersistenceContextProvider.CreateNewGuildContext())
        {
            members = await guilds.GetMemberNamesAsync(guildId).ConfigureAwait(false);
            var masters = await guilds.GetGuildMasterNamesAsync().ConfigureAwait(false);
            masters.TryGetValue(guildId, out master);
        }

        var paid = new List<WeeklyWinner>();
        foreach (var (memberId, name) in members)
        {
            string? login;
            using (var players = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration))
            {
                login = (await players.GetAccountByCharacterNameAsync(name).ConfigureAwait(false))?.LoginName;
            }

            var coins = siege.CoinsPerMember + (string.Equals(name, master, StringComparison.Ordinal) ? siege.GuildMasterBonus : 0);
            if (login is null || coins <= 0)
            {
                continue;
            }

            await MissionCoinGranter.GrantAsync(
                gameContext,
                new CoinGrant(login, siege.CoinType, coins, $"Castle siege winners, week of {week} ({name})", $"hnmo-week:{week}:CastleSiege:{memberId}"),
                GrantedBy).ConfigureAwait(false);
            paid.Add(new WeeklyWinner { Rank = paid.Count + 1, CharacterId = memberId.ToString(), Name = name, Login = login, Coins = coins, CoinType = siege.CoinType });
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
