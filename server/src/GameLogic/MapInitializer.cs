// <copyright file="MapInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using System.Collections.Concurrent;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views.NPC;
using HNMO.Pathfinding;
using HNMO.PlugIns;
using Nito.AsyncEx;

/// <summary>
/// A basic map initializer.
/// </summary>
/// <seealso cref="HNMO.GameLogic.IMapInitializer" />
public class MapInitializer : IMapInitializer
{
    private readonly IDropGenerator _dropGenerator;
    private readonly IConfigurationChangeMediator? _configurationChangeMediator;
    private readonly GameConfiguration _configuration;
    private readonly ILogger<MapInitializer> _logger;

    private readonly ConcurrentDictionary<MonsterSpawnArea, int> _spawnedMonsters = new();

    /// <summary>
    /// The maps whose monster amounts follow the <see cref="MonsterAmountPlugIn"/>, with their spawned monsters
    /// by spawn area and spawn index.
    /// </summary>
    private readonly ConcurrentDictionary<GameMap, ConcurrentDictionary<MonsterSpawnArea, ConcurrentDictionary<int, NonPlayerCharacter>>> _amountMaps = new();

    private readonly AsyncLock _amountLock = new();

    private PlugInManager? _plugInManager;
    private MonsterAmountConfiguration? _monsterAmounts;
    private MonsterAmountConfiguration? _lastConfiguredMonsterAmounts;
    private bool _monsterAmountsRead;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapInitializer" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="dropGenerator">The drop generator.</param>
    /// <param name="configurationChangeMediator">The configuration change mediator.</param>
    public MapInitializer(GameConfiguration configuration, ILogger<MapInitializer> logger, IDropGenerator dropGenerator, IConfigurationChangeMediator? configurationChangeMediator)
    {
        this._dropGenerator = dropGenerator;
        this._configurationChangeMediator = configurationChangeMediator;
        this._configuration = configuration;
        this._logger = logger;
        this.ChunkSize = 8;
    }

    /// <summary>
    /// Gets or sets the plug in manager.
    /// </summary>
    public PlugInManager? PlugInManager
    {
        get => this._plugInManager;
        set
        {
            if (this._plugInManager == value)
            {
                return;
            }

            if (this._plugInManager is { } oldManager)
            {
                oldManager.PlugInConfigurationChanged -= this.OnPlugInConfigurationChanged;
                oldManager.PlugInActivated -= this.OnPlugInActivated;
                oldManager.PlugInDeactivated -= this.OnPlugInDeactivated;
            }

            this._plugInManager = value;
            if (value is not null)
            {
                value.PlugInConfigurationChanged += this.OnPlugInConfigurationChanged;
                value.PlugInActivated += this.OnPlugInActivated;
                value.PlugInDeactivated += this.OnPlugInDeactivated;
            }
        }
    }

    /// <summary>
    /// Gets or sets the path finder pool.
    /// </summary>
    /// <value>
    /// The path finder pool.
    /// </value>
    public IObjectPool<PathFinder>? PathFinderPool { get; set; }

    /// <summary>
    /// Gets or sets the size of the chunk of created <see cref="GameMap"/>s.
    /// </summary>
    /// <value>
    /// The size of the chunk of created <see cref="GameMap"/>s.
    /// </value>
    protected byte ChunkSize { get; set; }

    /// <inheritdoc/>
    public GameMap? CreateGameMap(ushort mapNumber)
    {
        var definition = this.GetMapDefinition(mapNumber);
        if (definition != null)
        {
            return this.InternalCreateGameMap(definition);
        }

        return null;
    }

    /// <summary>
    /// Creates a new game map instance with the specified definition.
    /// </summary>
    /// <param name="mapDefinition">The map definition.</param>
    /// <returns>The new game map instance.</returns>
    public GameMap CreateGameMap(GameMapDefinition mapDefinition)
    {
        return this.InternalCreateGameMap(mapDefinition);
    }

    /// <inheritdoc />
    public async ValueTask InitializeStateAsync(GameMap createdMap)
    {
        _ = this.PlugInManager ?? throw new InvalidOperationException("PlugInManager must be set first");
        _ = this.PathFinderPool ?? throw new InvalidOperationException("PathFinderPool must be set first");

        this._logger.LogDebug("Start creating monster instances for map {createdMap}", createdMap.Definition.Name);
        var automaticSpawns = createdMap.Definition.MonsterSpawns
            .Where(m => m.MonsterDefinition is { IsActive: true })
            .Where(m => m.SpawnTrigger is SpawnTrigger.Automatic);
        if (!this.IsMiniGameMap(createdMap.Definition))
        {
            this._amountMaps.TryAdd(createdMap, new());
            createdMap.RegisterOwnedRegistration(new DisposeAction(() => this._amountMaps.TryRemove(createdMap, out _)));
        }

        foreach (var spawnArea in automaticSpawns)
        {
            var quantity = this.GetEffectiveQuantity(createdMap, spawnArea);
            for (int i = 0; i < quantity; i++)
            {
                await this.InitializeSpawnAsync(i, createdMap, spawnArea).ConfigureAwait(false);
            }

            this._spawnedMonsters.AddOrUpdate(spawnArea, quantity, (_, _) => quantity);
        }

        var newSpawnRegistration = this._configurationChangeMediator?.RegisterForNew<MonsterSpawnArea, GameMap>(createdMap, async (spawnArea, map) =>
        {
            if (!Equals(spawnArea.GameMap, map.Definition)
                || spawnArea.MonsterDefinition is not { IsActive: true })
            {
                return;
            }

            var quantity = this.GetEffectiveQuantity(map, spawnArea);
            for (int i = 0; i < quantity; i++)
            {
                await this.InitializeSpawnAsync(i, map, spawnArea).ConfigureAwait(false);
            }

            this._spawnedMonsters.AddOrUpdate(spawnArea, quantity, (_, _) => quantity);
        });

        if (newSpawnRegistration is not null)
        {
            createdMap.RegisterOwnedRegistration(newSpawnRegistration);
        }

        this._logger.LogDebug("Finished creating monster instances for map {createdMap}", createdMap.Definition.Name);
    }

    /// <summary>
    /// Initializes the event NPCs of the previously created game map.
    /// </summary>
    /// <param name="createdMap">The created map.</param>
    /// <param name="eventStateProvider">The event state provider.</param>
    public async ValueTask InitializeNpcsOnEventStartAsync(GameMap createdMap, IEventStateProvider eventStateProvider)
    {
        _ = this.PlugInManager ?? throw new InvalidOperationException("PlugInManager must be set first");
        _ = this.PathFinderPool ?? throw new InvalidOperationException("PathFinderPool must be set first");

        this._logger.LogDebug("Start creating event monster instances for map {createdMap}", createdMap.Definition.Name);
        var eventSpawns = createdMap.Definition.MonsterSpawns
            .Where(m => m.MonsterDefinition is { IsActive: true })
            .Where(m => m.SpawnTrigger is SpawnTrigger.OnceAtEventStart or SpawnTrigger.AutomaticDuringEvent);

        foreach (var spawnArea in eventSpawns)
        {
            for (int i = 0; i < spawnArea.Quantity; i++)
            {
                await this.InitializeSpawnAsync(i, createdMap, spawnArea, eventStateProvider).ConfigureAwait(false);
            }
        }

        this._logger.LogDebug("Finished creating event monster instances for map {createdMap}", createdMap.Definition.Name);
    }

    /// <inheritdoc />
    public async ValueTask InitializeNpcsOnWaveStartAsync(GameMap createdMap, IEventStateProvider eventStateProvider, byte waveNumber)
    {
        _ = this.PlugInManager ?? throw new InvalidOperationException("PlugInManager must be set first");
        _ = this.PathFinderPool ?? throw new InvalidOperationException("PathFinderPool must be set first");

        this._logger.LogDebug("Start creating event monster instances for map {createdMap}", createdMap.Definition.Name);
        var waveSpawns = createdMap.Definition.MonsterSpawns
            .Where(m => m.MonsterDefinition is { IsActive: true })
            .Where(m => m.SpawnTrigger is SpawnTrigger.AutomaticDuringWave or SpawnTrigger.OnceAtWaveStart)
            .Where(m => m.WaveNumber == waveNumber);

        foreach (var spawnArea in waveSpawns)
        {
            for (int i = 0; i < spawnArea.Quantity; i++)
            {
                await this.InitializeSpawnAsync(i, createdMap, spawnArea, eventStateProvider).ConfigureAwait(false);
            }
        }

        this._logger.LogDebug("Finished creating event monster instances for map {createdMap}", createdMap.Definition.Name);
    }

    /// <inheritdoc />
    public async ValueTask<NonPlayerCharacter?> InitializeSpawnAsync(
        int spawnIndex,
        GameMap createdMap,
        MonsterSpawnArea spawnArea,
        IEventStateProvider? eventStateProvider = null,
        IDropGenerator? dropGenerator = null)
    {
        _ = this.PlugInManager ?? throw new InvalidOperationException("PlugInManager must be set first");
        _ = this.PathFinderPool ?? throw new InvalidOperationException("PathFinderPool must be set first");

        if (createdMap.IsTornDown)
        {
            // E.g. a spawn wave of a mini game which ended in the meantime.
            this._logger.LogDebug("Skipping spawn {spawn}, because its map is torn down.", spawnArea);
            return null;
        }

        if (spawnArea.MonsterDefinition is not { IsActive: true } monsterDef)
        {
            // E.g. a spawn of a mini game, which doesn't filter its spawn areas before.
            this._logger.LogDebug("Skipping spawn {spawn}, because its monster is not active.", spawnArea);
            return null;
        }

        NonPlayerCharacter npc;

        var intelligence = this.TryCreateConfiguredNpcIntelligence(monsterDef, createdMap);

        if (monsterDef.ObjectKind == NpcObjectKind.Monster)
        {
            this._logger.LogDebug("Creating monster {spawn}", spawnArea);
            npc = new Monster(spawnArea, monsterDef, createdMap, dropGenerator ?? this._dropGenerator, intelligence ?? new BasicMonsterIntelligence(), this.PlugInManager, this.PathFinderPool, eventStateProvider);
        }
        else if (monsterDef.ObjectKind == NpcObjectKind.Guard)
        {
            this._logger.LogDebug("Creating guard {spawn}", spawnArea);
            npc = new Monster(spawnArea, monsterDef, createdMap, NullDropGenerator.Instance, intelligence ?? new GuardIntelligence(), this.PlugInManager, this.PathFinderPool, eventStateProvider);
        }
        else if (monsterDef.ObjectKind == NpcObjectKind.Trap)
        {
            this._logger.LogDebug("Creating trap {spawn}", spawnArea);
            npc = new Trap(spawnArea, monsterDef, createdMap, intelligence ?? new RandomAttackInRangeTrapIntelligence(createdMap));
        }
        else if (monsterDef.ObjectKind == NpcObjectKind.SoccerBall)
        {
            this._logger.LogDebug("Creating soccer ball {spawn}", spawnArea);
            npc = new SoccerBall(spawnArea, monsterDef, createdMap);
        }
        else if (monsterDef.ObjectKind == NpcObjectKind.Destructible)
        {
            this._logger.LogDebug("Creating destructible {spawn}", spawnArea);
            npc = new Destructible(spawnArea, monsterDef, createdMap, eventStateProvider, dropGenerator ?? this._dropGenerator, this.PlugInManager!);
        }
        else if (monsterDef.MerchantStore is not null)
        {
            this._logger.LogDebug("Creating merchant npc {spawn}", spawnArea);
            npc = new MerchantNpc(spawnArea, monsterDef, createdMap);
        }
        else
        {
            this._logger.LogDebug("Creating npc {spawn}", spawnArea);
            npc = new NonPlayerCharacter(spawnArea, monsterDef, createdMap);
        }

        try
        {
            npc.SpawnIndex = spawnIndex;
            npc.Initialize();
            await createdMap.AddAsync(npc).ConfigureAwait(false);
            if (createdMap.IsTornDown)
            {
                // The map got torn down while the NPC was created, so the teardown may have missed it.
                await npc.DisposeAsync().ConfigureAwait(false);
                return null;
            }

            npc.OnSpawn();
            if (spawnArea.SpawnTrigger is SpawnTrigger.Automatic or SpawnTrigger.Wandering)
            {
                this.RegisterForConfigChanges(createdMap, spawnArea, npc);
            }

            this.TrackAmountSpawn(createdMap, spawnArea, npc);

            return npc;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, $"Object {spawnArea} couldn't be initialized.", spawnArea);
            await npc.DisposeAsync().ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Applies the current monster amounts of the <see cref="MonsterAmountPlugIn"/> to the running maps:
    /// missing monsters of a spawn area spawn, the ones above its amount are removed. Mini game maps are left as they are.
    /// </summary>
    /// <returns>The task.</returns>
    public async ValueTask ApplyMonsterAmountsAsync()
    {
        if (this.PlugInManager is null || this.PathFinderPool is null)
        {
            return;
        }

        using var lck = await this._amountLock.LockAsync().ConfigureAwait(false);
        foreach (var (map, spawned) in this._amountMaps.ToList())
        {
            if (map.IsTornDown)
            {
                continue;
            }

            var areas = map.Definition.MonsterSpawns
                .Where(m => m.SpawnTrigger is SpawnTrigger.Automatic && IsAmountMultiplied(m))
                .ToList();
            foreach (var area in areas)
            {
                var quantity = this.GetEffectiveQuantity(map, area);
                var byIndex = spawned.GetOrAdd(area, _ => new());
                foreach (var (index, npc) in byIndex.Where(p => p.Key >= quantity).ToList())
                {
                    byIndex.TryRemove(index, out _);
                    await npc.DisposeAsync().ConfigureAwait(false);
                }

                for (var i = 0; i < quantity; i++)
                {
                    if (!byIndex.ContainsKey(i))
                    {
                        await this.InitializeSpawnAsync(i, map, area).ConfigureAwait(false);
                    }
                }

                this._spawnedMonsters.AddOrUpdate(area, quantity, (_, _) => quantity);
            }
        }
    }

    /// <summary>
    /// Gets the quantity of a spawn area which spawns on a map: its stored quantity,
    /// multiplied by the map's multiplier of the <see cref="MonsterAmountPlugIn"/> for automatic monster spawns.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="spawnArea">The spawn area.</param>
    /// <returns>The quantity which spawns.</returns>
    protected int GetEffectiveQuantity(GameMap map, MonsterSpawnArea spawnArea)
    {
        if (spawnArea.SpawnTrigger is not SpawnTrigger.Automatic
            || !IsAmountMultiplied(spawnArea)
            || !this._amountMaps.ContainsKey(map))
        {
            return spawnArea.Quantity;
        }

        var amounts = this.GetMonsterAmounts();
        return amounts is null
            ? spawnArea.Quantity
            : MonsterAmountConfiguration.Apply(spawnArea.Quantity, amounts.GetMultiplier(map.Definition.Number));
    }

    /// <summary>
    /// Gets the map definition by searching for it at the <see cref="GameConfiguration"/>.
    /// Inactive maps are not considered.
    /// </summary>
    /// <param name="mapNumber">The map number.</param>
    /// <returns>The game map definition.</returns>
    protected virtual GameMapDefinition? GetMapDefinition(ushort mapNumber)
    {
        return this._configuration.Maps.FirstOrDefault(m => m.Number == mapNumber && m.IsActive);
    }

    /// <summary>
    /// Creates the game map instance with the specified definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>
    /// The created game map instance.
    /// </returns>
    protected virtual GameMap InternalCreateGameMap(GameMapDefinition definition)
    {
        this._logger.LogDebug("Creating GameMap {0}", definition);
        return definition.BattleZone?.Type == BattleType.Soccer
            ? new SoccerGameMap(definition, this._configuration.ItemDropDuration, this.ChunkSize)
            : new GameMap(definition, this._configuration.ItemDropDuration, this.ChunkSize);
    }

    private static bool IsAmountMultiplied(MonsterSpawnArea spawnArea)
    {
        return spawnArea.MonsterDefinition is { IsActive: true, ObjectKind: NpcObjectKind.Monster };
    }

    private static MonsterAmountConfiguration? ReadMonsterAmounts(PlugInConfiguration? configuration)
    {
        if (configuration is null)
        {
            return null;
        }

        try
        {
            return configuration.GetConfiguration<MonsterAmountConfiguration>(null);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private MonsterAmountConfiguration? GetMonsterAmounts()
    {
        if (!this._monsterAmountsRead)
        {
            // Before the plug-in manager reports anything, the saved configuration counts.
            var configuration = this._configuration.PlugInConfigurations?.FirstOrDefault(c => c.TypeId == typeof(MonsterAmountPlugIn).GUID);
            this._lastConfiguredMonsterAmounts = ReadMonsterAmounts(configuration);
            this._monsterAmounts = configuration is { IsActive: true } ? this._lastConfiguredMonsterAmounts : null;
            this._monsterAmountsRead = true;
        }

        return this._monsterAmounts;
    }

    private void OnPlugInConfigurationChanged(object? sender, PlugInConfigurationChangedEventArgs e)
    {
        if (e.PlugInType != typeof(MonsterAmountPlugIn))
        {
            return;
        }

        this._lastConfiguredMonsterAmounts = ReadMonsterAmounts(e.Configuration);
        this._monsterAmounts = e.Configuration.IsActive ? this._lastConfiguredMonsterAmounts : null;
        this._monsterAmountsRead = true;
        this.ApplyMonsterAmountsInBackground();
    }

    private void OnPlugInActivated(object? sender, PlugInEventArgs e)
    {
        if (e.PlugInType != typeof(MonsterAmountPlugIn))
        {
            return;
        }

        this.GetMonsterAmounts();
        this._monsterAmounts = this._lastConfiguredMonsterAmounts;
        this.ApplyMonsterAmountsInBackground();
    }

    private void OnPlugInDeactivated(object? sender, PlugInEventArgs e)
    {
        if (e.PlugInType != typeof(MonsterAmountPlugIn))
        {
            return;
        }

        this.GetMonsterAmounts();
        this._monsterAmounts = null;
        this.ApplyMonsterAmountsInBackground();
    }

    private void ApplyMonsterAmountsInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await this.ApplyMonsterAmountsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Applying the monster amounts failed.");
            }
        });
    }

    private void TrackAmountSpawn(GameMap map, MonsterSpawnArea spawnArea, NonPlayerCharacter npc)
    {
        if (spawnArea.SpawnTrigger is not SpawnTrigger.Automatic
            || !IsAmountMultiplied(spawnArea)
            || !this._amountMaps.TryGetValue(map, out var spawned))
        {
            return;
        }

        var byIndex = spawned.GetOrAdd(spawnArea, _ => new());
        byIndex[npc.SpawnIndex] = npc;
        npc.RegisterDisposable(new DisposeAction(() => ((ICollection<KeyValuePair<int, NonPlayerCharacter>>)byIndex).Remove(new(npc.SpawnIndex, npc))));
    }

    private bool IsMiniGameMap(GameMapDefinition definition)
    {
        return this._configuration.MiniGameDefinitions?.Any(m => Equals(m.Entrance?.Map, definition)) ?? false;
    }

    private void RegisterForConfigChanges(GameMap createdMap, MonsterSpawnArea spawnArea, NonPlayerCharacter spawnedObject)
    {
        // Apply changes of the monster definition (e.g. its attributes) instantly to the
        // already spawned instance, without having to re-spawn it. The registration is owned
        // by the NPC, so it gets disposed whenever the NPC is disposed.
        if (spawnedObject is AttackableNpcBase attackableNpc
            && this._configurationChangeMediator?.RegisterObject(
                spawnedObject.Definition,
                attackableNpc,
                (_, _, o) =>
                {
                    o.ReloadAttributes();
                    return ValueTask.CompletedTask;
                }) is { } definitionRegistration)
        {
            attackableNpc.RegisterDisposable(definitionRegistration);
        }

        // The mediator keeps the registrations below under the configuration, which is shared by every
        // instance of the map. So they are owned by the NPC as well, and released when it's disposed, e.g.
        // when the map of a mini game is torn down.
        var spawnAreaRegistration = this._configurationChangeMediator?.RegisterObject(
            spawnArea,
            spawnedObject,
            async (unregisterAction, area, o) =>
            {
                var quantity = this.GetEffectiveQuantity(createdMap, area);
                if (quantity < o.SpawnIndex + 1)
                {
                    await o.DisposeAsync().ConfigureAwait(false);
                    unregisterAction();
                    this._spawnedMonsters.AddOrUpdate(spawnArea, quantity, (_, _) => quantity);
                    return;
                }

                // If the monster definition changed, we need to spawn a completely new NPC
                // because MonsterAttributeHolder caches stats from the definition at construction time.
                if (area.MonsterDefinition != o.Definition)
                {
                    await o.DisposeAsync().ConfigureAwait(false);
                    unregisterAction();
                    var newNpc = await this.InitializeSpawnAsync(o.SpawnIndex, createdMap, area).ConfigureAwait(false);
                    if (newNpc is not null)
                    {
                        this._spawnedMonsters.AddOrUpdate(spawnArea, quantity, (_, _) => quantity);
                        this.RegisterForConfigChanges(createdMap, area, newNpc);
                    }

                    return;
                }

                await createdMap.RemoveAsync(o).ConfigureAwait(false);
                o.Initialize();
                await createdMap.AddAsync(o).ConfigureAwait(false);
                o.OnSpawn();

                if (this._spawnedMonsters.TryGetValue(area, out var previousSpawnCount))
                {
                    for (int i = previousSpawnCount; i < quantity; i++)
                    {
                        await this.InitializeSpawnAsync(i, createdMap, area).ConfigureAwait(false);
                    }

                    this._spawnedMonsters.AddOrUpdate(spawnArea, quantity, (_, _) => quantity);
                }
            },
            async (_, o) =>
            {
                await o.DisposeAsync().ConfigureAwait(false);
                this._spawnedMonsters.TryRemove(spawnArea, out var _);
            });

        if (spawnAreaRegistration is not null)
        {
            spawnedObject.RegisterDisposable(spawnAreaRegistration);
        }

        if (spawnedObject.Definition.MerchantStore is { } merchantStore
            && this._configurationChangeMediator?.RegisterObject(merchantStore, spawnedObject, async (_, itemStorage, o) =>
            {
                await o.ForEachObservingAsync<Player>(
                    async player =>
                    {
                        if (player.OpenedNpc == o)
                        {
                            await player.InvokeViewPlugInAsync<IShowMerchantStoreItemListPlugIn>(
                                    plugin => plugin.ShowMerchantStoreItemListAsync(itemStorage.GetOfferedItems(), StoreKind.Normal))
                                .ConfigureAwait(false);
                        }
                    },
                    false).ConfigureAwait(false);
            }) is { } storeRegistration)
        {
            spawnedObject.RegisterDisposable(storeRegistration);
        }
    }

    private INpcIntelligence? TryCreateConfiguredNpcIntelligence(MonsterDefinition monsterDefinition, GameMap createdMap)
    {
        if (string.IsNullOrWhiteSpace(monsterDefinition.IntelligenceTypeName))
        {
            return null;
        }

        try
        {
            if (NpcIntelligenceFactory.Create(monsterDefinition.IntelligenceTypeName, createdMap) is { } intelligence)
            {
                return intelligence;
            }

            // Not an intelligence of this assembly, e.g. an assembly qualified type name of another assembly.
            var type = Type.GetType(monsterDefinition.IntelligenceTypeName);
            if (type is null)
            {
                this._logger.LogError($"Could not find type {monsterDefinition.IntelligenceTypeName}");
                return null;
            }

            var constructorNeedsMap = type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(GameMap)));
            if (constructorNeedsMap)
            {
                return Activator.CreateInstance(type, createdMap) as INpcIntelligence;
            }
            else
            {
                return Activator.CreateInstance(type) as INpcIntelligence;
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, $"Could not create npc intelligence for monster {monsterDefinition.Designation}, type name {monsterDefinition.IntelligenceTypeName}");
        }

        return null;
    }

    /// <summary>
    /// Runs an action when it's disposed.
    /// </summary>
    private sealed class DisposeAction : IDisposable
    {
        private Action? _action;

        public DisposeAction(Action action)
        {
            this._action = action;
        }

        public void Dispose()
        {
            System.Threading.Interlocked.Exchange(ref this._action, null)?.Invoke();
        }
    }
}
