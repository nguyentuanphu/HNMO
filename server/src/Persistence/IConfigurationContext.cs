// <copyright file="IConfigurationContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Threading;
using HNMO.DataModel.Configuration;

/// <summary>
/// Persistence context which is used to access the <see cref="GameConfiguration"/>.
/// </summary>
public interface IConfigurationContext : IContext
{
    /// <summary>
    /// Gets the default context identifier.
    /// Usually, there is only one game configuration on a database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The id of the default game configuration.
    /// </returns>
    ValueTask<Guid?> GetDefaultGameConfigurationIdAsync(CancellationToken cancellationToken);
}