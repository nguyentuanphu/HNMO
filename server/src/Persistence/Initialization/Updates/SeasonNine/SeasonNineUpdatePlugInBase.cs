// <copyright file="SeasonNineUpdatePlugInBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using HNMO.DataModel.Configuration;
using HNMO.Interfaces;

/// <summary>
/// Base class for Season 9-only mandatory configuration updates keyed to <see cref="VersionSeasonSix.DataInitialization.Id"/>.
/// On Season 6 instances (<see cref="HnmoSeasonInstance.IsSeasonNine"/> is <c>false</c>), these updates are skipped on
/// new databases and recorded as installed no-ops on existing databases so Season 6 data remains untouched.
/// </summary>
public abstract class SeasonNineUpdatePlugInBase : UpdatePlugInBase
{
    private readonly Func<bool> _isSeasonNine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineUpdatePlugInBase"/> class.
    /// </summary>
    protected SeasonNineUpdatePlugInBase()
        : this(() => HnmoSeasonInstance.IsSeasonNine)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineUpdatePlugInBase"/> class with an explicit season check (for tests).
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    protected SeasonNineUpdatePlugInBase(Func<bool> isSeasonNine)
    {
        this._isSeasonNine = isSeasonNine;
    }

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override bool RunsOnNewDatabase => this._isSeasonNine();

    /// <inheritdoc />
    protected sealed override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (!this._isSeasonNine())
        {
            return ValueTask.CompletedTask;
        }

        return this.ApplySeasonNineAsync(context, gameConfiguration);
    }

    /// <summary>
    /// Applies this update when running as a Season 9 instance (<c>HNMO_SEASON=s9</c>).
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    protected abstract ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration);
}
