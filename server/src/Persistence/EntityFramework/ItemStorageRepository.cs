// <copyright file="ItemStorageRepository.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using System.Linq;
using System.Threading;
using HNMO.Persistence.EntityFramework.Json;
using HNMO.Persistence.EntityFramework.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Repository for item storages which loads a storage with all of its items, exactly like the vault of an account is
/// loaded with the account. The multi-vault feature keeps each extra vault of an account as its own storage and loads
/// it by its id when the player switches to it.
/// </summary>
internal class ItemStorageRepository : CachingGenericRepository<ItemStorage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemStorageRepository" /> class.
    /// </summary>
    /// <param name="repositoryProvider">The repository provider.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public ItemStorageRepository(IContextAwareRepositoryProvider repositoryProvider, ILoggerFactory loggerFactory)
        : base(repositoryProvider, loggerFactory)
    {
    }

    /// <inheritdoc />
    public override async ValueTask<ItemStorage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        (this.RepositoryProvider as ICacheAwareRepositoryProvider)?.EnsureCachesForCurrentGameConfiguration();

        using var context = this.GetContext();
        if (context.Context.ChangeTracker.Entries<ItemStorage>().FirstOrDefault(s => s.Entity.Id == id) is { } tracked)
        {
            return tracked.Entity;
        }

        await context.Context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var objectLoader = new AccountJsonObjectLoader((this.RepositoryProvider as CacheAwareRepositoryProvider)?.ConfigurationReferences);
            var storage = await objectLoader.LoadObjectAsync<ItemStorage>(id, context.Context, cancellationToken).ConfigureAwait(false);
            if (storage is not null && context.Context.Entry(storage).State == EntityState.Detached)
            {
                context.Context.Attach(storage);
            }

            return storage;
        }
        finally
        {
            await context.Context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }
}
