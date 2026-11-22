// <copyright file="NonPlayerCharacter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.NPC;

using HNMO.GameLogic.Views.World;
using HNMO.Pathfinding;
using Nito.AsyncEx;

/// <summary>
/// The implementation of a non-player-character (Monster) which cannot be attacked or attack.
/// </summary>
public class NonPlayerCharacter : AsyncDisposable, IObservable, IRotatable, ILocateable, IHasBucketInformation
{
    /// <summary>
    /// The disposables which are disposed together with this instance, see <see cref="RegisterDisposable"/>.
    /// Guarded by locking on itself.
    /// </summary>
    private readonly List<IDisposable> _registrations = new();

    private bool _registrationsReleased;

    private (byte X1, byte Y1, byte X2, byte Y2)? _spawnAreaBounds;

    /// <summary>
    /// Initializes a new instance of the <see cref="NonPlayerCharacter"/> class.
    /// </summary>
    /// <param name="spawnInfo">The spawn information.</param>
    /// <param name="stats">The stats.</param>
    /// <param name="map">The map on which this instance will spawn.</param>
    public NonPlayerCharacter(MonsterSpawnArea spawnInfo, MonsterDefinition stats, GameMap map)
    {
        this.SpawnArea = spawnInfo;
        this.Definition = stats;
        this.CurrentMap = map;
    }

    /// <inheritdoc/>
    public ushort Id { get; set; }

    /// <summary>
    /// Gets or sets the stats of this instance.
    /// </summary>
    public MonsterDefinition Definition { get; set; }

    /// <inheritdoc/>
    public virtual Point Position { get; set; }

    /// <summary>
    /// Gets the initial spawn coordinate chosen for this instance within <see cref="SpawnArea"/>.
    /// Subsequent respawns stay within <see cref="MonsterDefinition.MoveRange"/> of this spot so hunting in one
    /// part of a large multi-spawn area does not drain monsters to the unobserved edges of the area.
    /// </summary>
    public Point SpawnPoint { get; private set; }

    /// <inheritdoc/>
    public Direction Rotation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is invisible, so that it isn't shown to the players,
    /// e.g. a trap which isn't known by the game client.
    /// </summary>
    public bool IsInvisible { get; set; }

    /// <inheritdoc/>
    public ISet<IWorldObserver> Observers { get; } = new HashSet<IWorldObserver>();

    /// <summary>
    /// Gets the lock for <see cref="Observers"/>.
    /// </summary>
    public AsyncReaderWriterLock ObserverLock { get; } = new();

    /// <inheritdoc/>
    public GameMap CurrentMap { get; }

    /// <summary>
    /// Gets or sets the spawn area of this instance.
    /// </summary>
    public MonsterSpawnArea SpawnArea { get; protected set; }

    /// <summary>
    /// Gets or sets the index of the spawn within the <see cref="SpawnArea"/>.
    /// </summary>
    public int SpawnIndex { get; set; }

    /// <inheritdoc/>
    public Bucket<ILocateable>? NewBucket { get; set; }

    /// <inheritdoc/>
    public Bucket<ILocateable>? OldBucket { get; set; }

    /// <summary>
    /// Gets a value indicating whether this instance can spawn in a safe zone.
    /// </summary>
    protected virtual bool CanSpawnInSafezone => this.Definition.ObjectKind != NpcObjectKind.Monster && this.Definition.ObjectKind != NpcObjectKind.Trap;

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public virtual void Initialize()
    {
        const int maxRetryForAreas = 100;
        var currentBounds = (this.SpawnArea.X1, this.SpawnArea.Y1, this.SpawnArea.X2, this.SpawnArea.Y2);
        var hasValidHomeSpot = this._spawnAreaBounds == currentBounds && this.IsValidSpawnPoint(this.SpawnPoint);

        Point? spawnPoint = null;
        if (!hasValidHomeSpot)
        {
            var maxRetry = this.SpawnArea.IsPoint() ? 1 : maxRetryForAreas;
            for (var retry = 0; retry < maxRetry && spawnPoint is null; retry++)
            {
                spawnPoint = this.GetNewSpawnPoint(this.SpawnArea);
            }

            if (spawnPoint == null)
            {
                throw new InvalidOperationException("No valid spawn point found. Spawn area might not contain valid points.");
            }

            this.SpawnPoint = spawnPoint.Value;
            this._spawnAreaBounds = currentBounds;
        }
        else if (this.SpawnArea.IsPoint() || this.Definition.MoveRange <= 0)
        {
            spawnPoint = this.SpawnPoint;
        }
        else
        {
            for (var retry = 0; retry < maxRetryForAreas && spawnPoint is null; retry++)
            {
                spawnPoint = this.GetRespawnPointNearHome();
            }

            spawnPoint ??= this.SpawnPoint;
        }

        this.Position = spawnPoint.Value;
        this.Rotation = GetSpawnDirection(this.SpawnArea.Direction);
    }

    /// <summary>
    /// Called when this instance spawned on the map.
    /// </summary>
    public virtual void OnSpawn()
    {
        // can be overwritten
    }

    /// <inheritdoc/>
    public async ValueTask AddObserverAsync(IWorldObserver observer)
    {
        using var writerLock = await this.ObserverLock.WriterLockAsync().ConfigureAwait(false);
        this.Observers.Add(observer);
        if (this.Observers.Count == 1)
        {
            this.OnFirstObserverAdded();
        }

        this.OnObserverAdded();
    }

    /// <inheritdoc/>
    public async ValueTask RemoveObserverAsync(IWorldObserver observer)
    {
        using var writerLock = await this.ObserverLock.WriterLockAsync().ConfigureAwait(false);
        this.Observers.Remove(observer);
        if (this.Observers.Count == 0)
        {
            this.OnLastObserverRemoved();
        }

        this.OnObserverRemoved();
    }

    /// <summary>
    /// Registers a disposable (e.g. a configuration change registration) to be disposed
    /// together with this instance.
    /// </summary>
    /// <param name="disposable">The disposable.</param>
    public void RegisterDisposable(IDisposable disposable)
    {
        lock (this._registrations)
        {
            if (!this._registrationsReleased)
            {
                this._registrations.Add(disposable);
                return;
            }
        }

        // Registered after this instance was disposed, e.g. by a spawn which raced the teardown of its map.
        disposable.Dispose();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{this.Definition.Designation} - Id: {this.Id} - Position: {this.Position}";
    }

    /// <summary>
    /// Called when an observer has been added.
    /// </summary>
    protected virtual void OnObserverAdded()
    {
        // can be overwritten.
    }

    /// <summary>
    /// Called when an observer has been removed.
    /// </summary>
    protected virtual void OnObserverRemoved()
    {
        // can be overwritten.
    }

    /// <summary>
    /// Called when the first observer has been added.
    /// </summary>
    protected virtual void OnFirstObserverAdded()
    {
        // can be overwritten.
    }

    /// <summary>
    /// Called when the last observer has been removed.
    /// </summary>
    protected virtual void OnLastObserverRemoved()
    {
        // can be overwritten.
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IDisposable[] registrations;
            lock (this._registrations)
            {
                this._registrationsReleased = true;
                registrations = [.. this._registrations];
                this._registrations.Clear();
            }

            foreach (var registration in registrations)
            {
                registration.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await this.CurrentMap.RemoveAsync(this).ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    /// <summary>
    /// Moves the instance to the specified position.
    /// </summary>
    /// <param name="target">The new coordinates.</param>
    /// <param name="type">The type of moving.</param>
    protected virtual ValueTask MoveAsync(Point target, MoveType type)
    {
        throw new NotSupportedException("NPCs can't be moved");
    }

    /// <summary>
    /// Gets the spawn direction.
    /// </summary>
    /// <param name="configuredDirection">The configured direction.</param>
    /// <returns>The spawn direction.</returns>
    private static Direction GetSpawnDirection(Direction configuredDirection)
    {
        if (configuredDirection == Direction.Undefined)
        {
            return (Direction)Rand.NextInt(1, 9);
        }

        return configuredDirection;
    }

    private Point? GetNewSpawnPoint(MonsterSpawnArea spawnArea)
    {
        var minX = Math.Min(spawnArea.X1, spawnArea.X2);
        var maxX = Math.Max(spawnArea.X1, spawnArea.X2);
        var minY = Math.Min(spawnArea.Y1, spawnArea.Y2);
        var maxY = Math.Max(spawnArea.Y1, spawnArea.Y2);

        var x = Rand.NextInt(minX, maxX + 1);
        var y = Rand.NextInt(minY, maxY + 1);
        var point = new Point((byte)x, (byte)y);
        if (this.IsValidSpawnPoint(point))
        {
            return point;
        }

        return null;
    }

    private Point? GetRespawnPointNearHome()
    {
        var range = Math.Max(0, (int)this.Definition.MoveRange);
        var areaMinX = Math.Min(this.SpawnArea.X1, this.SpawnArea.X2);
        var areaMaxX = Math.Max(this.SpawnArea.X1, this.SpawnArea.X2);
        var areaMinY = Math.Min(this.SpawnArea.Y1, this.SpawnArea.Y2);
        var areaMaxY = Math.Max(this.SpawnArea.Y1, this.SpawnArea.Y2);

        var minX = Math.Max(areaMinX, this.SpawnPoint.X - range);
        var maxX = Math.Min(areaMaxX, this.SpawnPoint.X + range);
        var minY = Math.Max(areaMinY, this.SpawnPoint.Y - range);
        var maxY = Math.Min(areaMaxY, this.SpawnPoint.Y + range);

        var x = Rand.NextInt(minX, maxX + 1);
        var y = Rand.NextInt(minY, maxY + 1);
        var point = new Point((byte)x, (byte)y);
        return this.IsValidSpawnPoint(point) ? point : null;
    }

    private bool IsValidSpawnPoint(Point spawnPoint)
    {
        var isSafezoneAllowed = this.CanSpawnInSafezone;
        var isInSafezone = this.CurrentMap.Terrain.SafezoneMap[spawnPoint.X, spawnPoint.Y];
        var npcCanWalk = this.Definition.ObjectKind == NpcObjectKind.Monster || this.Definition.ObjectKind == NpcObjectKind.Guard;
        var isWalkable = this.CurrentMap.Terrain.WalkMap[spawnPoint.X, spawnPoint.Y];
        return (isSafezoneAllowed || !isInSafezone) && (!npcCanWalk || isWalkable);
    }
}