// <copyright file="EntityDataContextFactory.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for <see cref="EntityDataContext"/>.
/// </summary>
public class EntityDataContextFactory : IDesignTimeDbContextFactory<EntityDataContext>
{
    /// <inheritdoc />
    public EntityDataContext CreateDbContext(string[] args)
    {
        if (!ConnectionConfigurator.IsInitialized)
        {
            ConnectionConfigurator.Initialize(new ConfigFileDatabaseConnectionStringProvider());
        }

        return new EntityDataContext();
    }
}