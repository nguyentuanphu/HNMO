// <copyright file="ShopDraftContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Components.Shops;

using System.Collections;
using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.Persistence;

/// <summary>A read-through context which isolates the option links created by the existing ItemEdit component.</summary>
/// <param name="source">The read-only lookup context, owned by the page.</param>
public sealed class ShopDraftContext(IContext source) : IContext
{
    /// <inheritdoc />
    public bool HasChanges => false;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public ValueTask<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Save the shop draft through its operation.");

    /// <inheritdoc />
    public IDisposable SuspendChangeNotifications() => this;

    /// <inheritdoc />
    public bool Detach(object item) => false;

    /// <inheritdoc />
    public void Attach(object item) => throw new NotSupportedException("Drafts cannot attach persisted objects.");

    /// <inheritdoc />
    public T CreateNew<T>(params object?[] args)
        where T : class => (T)this.CreateNew(typeof(T), args);

    /// <inheritdoc />
    public object CreateNew(Type type, params object?[] args) => type == typeof(ItemOptionLink)
        ? new ItemOptionLink() : throw new NotSupportedException("Only detached item option links may be created here.");

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync<T>(T obj)
        where T : class => ValueTask.FromResult(true);

    /// <inheritdoc />
    public Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken = default)
        where T : class => source.GetByIdAsync<T>(id, cancellationToken);

    /// <inheritdoc />
    public Task<object?> GetByIdAsync(Guid id, Type type, CancellationToken cancellationToken = default) => source.GetByIdAsync(id, type, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IEnumerable<T>> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class => source.GetAsync<T>(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IEnumerable> GetAsync(Type type, CancellationToken cancellationToken = default) => source.GetAsync(type, cancellationToken);

    /// <inheritdoc />
    public bool IsSupporting(Type type) => source.IsSupporting(type);
}
