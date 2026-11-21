// <copyright file="MonsterValuesUpdatePlugInBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using HNMO.DataModel.Configuration;

/// <summary>
/// The base of the mandatory Season 6 updates which give the monsters the values of the Season 6/9 server suites.
/// A value is only changed while it is still the value HNMO had, so a value the host chose stays.
/// </summary>
/// <remarks>
/// Many monsters of Season 6 are defined with the older versions (0.75, 0.95d) and keep their values there. A new
/// Season 6 database gets the suites' values for them from this update, so it also runs on a new database; for the
/// monsters which are defined with Season 6, the new database has the suites' values already and nothing changes.
/// </remarks>
public abstract class MonsterValuesUpdatePlugInBase : UpdatePlugInBase
{
    /// <summary>
    /// Gets the changes of this update.
    /// </summary>
    public abstract IReadOnlyList<MonsterValueChange> Changes { get; }

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override bool RunsOnNewDatabase => true;

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var change in this.Changes)
        {
            change.ApplyTo(context, gameConfiguration);
        }

        return ValueTask.CompletedTask;
    }
}
