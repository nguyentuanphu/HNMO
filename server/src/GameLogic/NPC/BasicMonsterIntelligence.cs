// <copyright file="BasicMonsterIntelligence.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.NPC;

using System.Diagnostics;
using System.Threading;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// A basic monster AI which is pretty basic.
/// </summary>
public class BasicMonsterIntelligence : INpcIntelligence, IDisposable
{
    private Timer? _aiTimer;
    private Monster? _monster;
    private IAttackable? _provokedTarget;
    private bool _isReturningHome;

    /// <summary>
    /// Finalizes an instance of the <see cref="BasicMonsterIntelligence"/> class.
    /// </summary>
    ~BasicMonsterIntelligence()
    {
        this.Dispose();
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance can walk on safezone.
    /// </summary>
    public bool CanWalkOnSafezone { get; protected set; }

    /// <inheritdoc/>
    public NonPlayerCharacter Npc
    {
        get => this.Monster;
        set => this.Monster = (Monster)value;
    }

    /// <summary>
    /// Gets or sets the monster.
    /// </summary>
    public Monster Monster
    {
        get => this._monster ?? throw new InvalidOperationException("Instance is not initialized with a Monster yet");
        set => this._monster = value;
    }

    /// <summary>
    /// Gets the current target.
    /// </summary>
    protected IAttackable? CurrentTarget { get; private set; }

    /// <inheritdoc/>
    public void Start()
    {
        var startDelay = this.Npc.Definition.AttackDelay + TimeSpan.FromMilliseconds(Rand.NextInt(0, 100));
        this.OnStart();
        this._aiTimer ??= new Timer(_ => this.SafeTick(), null, startDelay, this.Npc.Definition.AttackDelay);
    }

    /// <inheritdoc/>
    public void Pause()
    {
        this._aiTimer?.Dispose();
        this._aiTimer = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public virtual void RegisterHit(IAttacker attacker)
    {
        if (this._isReturningHome)
        {
            return;
        }

        if (attacker is IAttackable attackable)
        {
            if (this.GetConfig().ChaseAttacker)
            {
                this._provokedTarget = attackable;
            }

            if (this.CurrentTarget is null)
            {
                this.CurrentTarget = attackable;
            }
        }
    }

    /// <inheritdoc/>
    public virtual bool CanWalkOn(Point target)
    {
        return this.Monster.CurrentMap.Terrain.AIgrid[target.X, target.Y] == 1;
    }

    /// <summary>
    /// Determines whether the specified player is being targeted by this monster.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is the current target; otherwise, false.</returns>
    public bool IsTargetingPlayer(Player player) => this.CurrentTarget == player;

    /// <summary>
    /// Called when the intelligence starts.
    /// </summary>
    protected virtual void OnStart()
    {
        // can be overwritten for additional logic.
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="managed"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool managed)
    {
        this._aiTimer?.Dispose();
        this._aiTimer = null;
    }

    /// <summary>
    /// Searches the next target in view range.
    /// </summary>
    /// <returns>The next target.</returns>
    protected virtual async ValueTask<IAttackable?> SearchNextTargetAsync()
    {
        List<IWorldObserver> tempObservers;
        using (await this.Npc.ObserverLock.ReaderLockAsync().ConfigureAwait(false))
        {
            if (this.Npc.Observers.Count == 0)
            {
                return null;
            }

            tempObservers = new List<IWorldObserver>(this.Npc.Observers);
        }

        var possibleTargets = tempObservers.OfType<IAttackable>()
            .Where(a => a.IsActive() && !a.IsAtSafezone() && a is not Player { IsInvisible: true })
            .ToList();

        // Also consider summoned monsters belonging to players in range.
        var summons = possibleTargets.OfType<Player>()
            .Select(p => p.Summon?.Item1)
            .Where(s => s is not null)
            .Cast<IAttackable>()
            .WhereActive()
            .ToList();

        possibleTargets.AddRange(summons);

        // Prefer a target with a clear line of sight, so monsters don't lock onto
        // players behind walls. If nobody is visible, fall back to the nearest
        // target so the monster still walks toward it (and around the wall).
        // todo: check the walk distance
        return NpcTargetSelection.GetNearestPreferVisible(possibleTargets, this.Npc, target => target.GetDistanceTo(this.Npc));
    }

    /// <summary>
    /// Determines whether this instance can attack this tick.
    /// </summary>
    protected virtual ValueTask<bool> CanAttackAsync() => ValueTask.FromResult(true);

    /// <summary>
    /// Handles the tick when no target is available, moves the monster randomly.
    /// </summary>
    protected virtual async ValueTask TickWithoutTargetAsync()
    {
        if (this.Monster.Attributes[Stats.IsFrozen] > 0)
        {
            return;
        }

        if (await this.IsObservedByAttackerAsync().ConfigureAwait(false))
        {
            await this.Monster.RandomMoveAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Determines whether the handled monster is observed by any attacker.
    /// </summary>
    protected async ValueTask<bool> IsObservedByAttackerAsync()
    {
        using var readerLock = await this.Monster.ObserverLock.ReaderLockAsync().ConfigureAwait(false);
        return this.Monster.Observers.OfType<IAttacker>().Any();
    }

    private async ValueTask<bool> IsTargetInObserversAsync(IAttackable target)
    {
        using (await this.Npc.ObserverLock.ReaderLockAsync().ConfigureAwait(false))
        {
            return target is IWorldObserver worldObserver && this.Npc.Observers.Contains(worldObserver);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Catching all Exceptions.")]
    private async void SafeTick()
    {
        try
        {
            await this.TickAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown.
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message, ex.StackTrace);
        }
    }

    private async ValueTask TickAsync()
    {
        if (!this.Monster.IsAlive)
        {
            this.CurrentTarget = null;
            this._provokedTarget = null;
            this._isReturningHome = false;
            return;
        }

        if (this.Monster.IsWalking)
        {
            return;
        }

        if (this.Monster.Attributes[Stats.IsStunned] > 0 || this.Monster.Attributes[Stats.IsAsleep] > 0)
        {
            return;
        }

        var cfg = this.GetConfig();
        if (cfg.LeashToSpawnArea && this.Monster.Definition.MoveRange > 0)
        {
            var distFromSpawn = this.DistanceFromSpawnArea(this.Monster.Position);
            var leashLimit = this.Monster.Definition.MoveRange + Math.Max(0, cfg.LeashExtraRange);
            if (!this._isReturningHome && distFromSpawn > leashLimit)
            {
                this.CurrentTarget = null;
                this._provokedTarget = null;
                this._isReturningHome = true;
                if (cfg.HealToFullOnLeash)
                {
                    this.Monster.RestoreHealth(int.MaxValue);
                }
            }

            if (this._isReturningHome)
            {
                if (distFromSpawn <= 1)
                {
                    this._isReturningHome = false;
                    if (cfg.HealToFullOnLeash)
                    {
                        this.Monster.RestoreHealth(int.MaxValue);
                    }
                }
                else
                {
                    if (this.Monster.Attributes[Stats.IsFrozen] == 0)
                    {
                        await this.Monster.WalkToAsync(this.GetSpawnHomePoint()).ConfigureAwait(false);
                    }

                    return;
                }
            }
        }

        if (!await this.CanAttackAsync().ConfigureAwait(false))
        {
            return;
        }

        this.CurrentTarget = await this.ResolveTargetAsync().ConfigureAwait(false);

        if (this.CurrentTarget is null)
        {
            await this.TickWithoutTargetAsync().ConfigureAwait(false);
            return;
        }

        // Target in attack range with a clear line of sight — attack.
        // IsInRange is the cheap pre-filter; HasLineOfSightTo walks the terrain
        // line only when the target is already in range. Without sight the monster
        // falls through to WalkToAsync below and paths around the wall instead of
        // shooting through it.
        if (this.CurrentTarget.IsInRange(this.Monster.Position, this.Monster.Definition.AttackRange)
            && !this.Monster.IsAtSafezone()
            && this.Monster.HasLineOfSightTo(this.CurrentTarget))
        {
            await this.Monster.AttackAsync(this.CurrentTarget).ConfigureAwait(false);
            return;
        }

        if (this.Monster.Attributes[Stats.IsFrozen] > 0)
        {
            return;
        }

        // Target tracked but outside attack range (or without line of sight),
        // walk toward it to get into a hittable position.
        var isProvoked = cfg.ChaseAttacker && ReferenceEquals(this.CurrentTarget, this._provokedTarget);
        if (isProvoked || this.CurrentTarget.IsInRange(this.Monster.Position, this.Monster.Definition.ViewRange + 1))
        {
            var walkTarget = this.Monster.CurrentMap!.Terrain.GetRandomCoordinate(this.CurrentTarget.Position, this.Monster.Definition.AttackRange);
            if (await this.Monster.WalkToAsync(walkTarget).ConfigureAwait(false))
            {
                return;
            }
        }

        // Nothing else to do, wander randomly.
        await this.Monster.RandomMoveAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the current target if still valid, otherwise searches for a new one.
    /// </summary>
    private async ValueTask<IAttackable?> ResolveTargetAsync()
    {
        if (this.CurrentTarget is not null && this.IsCurrentTargetValid())
        {
            // Double-check the target is still within the observer list (needed for
            // players who have moved out of view range server-side).
            if (!await this.IsTargetInObserversAsync(this.CurrentTarget).ConfigureAwait(false)
                && this.CurrentTarget is IWorldObserver)
            {
                return await this.SearchNextTargetAsync().ConfigureAwait(false);
            }

            return this.CurrentTarget;
        }

        return await this.SearchNextTargetAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Returns <c>true</c> if the current target is still a valid attack candidate.
    /// </summary>
    private bool IsCurrentTargetValid()
    {
        if (this.CurrentTarget is null
            || !this.CurrentTarget.IsAlive
            || this.CurrentTarget is Player { IsInvisible: true }
            || this.CurrentTarget.IsTeleporting
            || this.CurrentTarget.IsAtSafezone())
        {
            return false;
        }

        var cfg = this.GetConfig();
        if (cfg.ChaseAttacker && ReferenceEquals(this.CurrentTarget, this._provokedTarget))
        {
            var chaseRange = Math.Max((int)this.Npc.Definition.ViewRange, this.Npc.Definition.MoveRange + Math.Max(0, cfg.LeashExtraRange) + 2);
            return this.CurrentTarget.IsInRange(this.Monster.Position, chaseRange);
        }

        return this.CurrentTarget.IsInRange(this.Monster.Position, this.Npc.Definition.ViewRange)
               && this.Monster.HasLineOfSightTo(this.CurrentTarget);
    }

    private MonsterAiConfiguration GetConfig()
    {
        return this.Monster.PlugInManager?
                   .GetActivePlugInsOf<ISupportCustomConfiguration<MonsterAiConfiguration>>()
                   .OfType<MonsterAiPlugIn>()
                   .FirstOrDefault()?
                   .Configuration
               ?? new MonsterAiConfiguration();
    }

    private int DistanceFromSpawnArea(Point position)
    {
        var area = this.Monster.SpawnArea;
        var minX = Math.Min(area.X1, area.X2);
        var maxX = Math.Max(area.X1, area.X2);
        var minY = Math.Min(area.Y1, area.Y2);
        var maxY = Math.Max(area.Y1, area.Y2);
        var dx = position.X < minX ? minX - position.X : (position.X > maxX ? position.X - maxX : 0);
        var dy = position.Y < minY ? minY - position.Y : (position.Y > maxY ? position.Y - maxY : 0);
        return Math.Max(dx, dy);
    }

    private Point GetSpawnHomePoint()
    {
        var area = this.Monster.SpawnArea;
        var cx = (byte)((Math.Min(area.X1, area.X2) + Math.Max(area.X1, area.X2)) / 2);
        var cy = (byte)((Math.Min(area.Y1, area.Y2) + Math.Max(area.Y1, area.Y2)) / 2);
        return new Point(cx, cy);
    }
}