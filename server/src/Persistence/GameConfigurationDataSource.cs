// <copyright file="GameConfigurationDataSource.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Collections;
using Microsoft.Extensions.Logging;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;

/// <summary>
/// Provider which provides the latest <see cref="GameConfiguration"/> and
/// it's child data.
/// </summary>
public sealed class GameConfigurationDataSource : DataSourceBase<GameConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameConfigurationDataSource"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    public GameConfigurationDataSource(ILogger<GameConfigurationDataSource> logger, IPersistenceContextProvider persistenceContextProvider)
        : base(logger, persistenceContextProvider)
    {
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<Type, Func<GameConfiguration, IEnumerable>> TypeToEnumerables => GameConfigurationHelper.Enumerables;
}