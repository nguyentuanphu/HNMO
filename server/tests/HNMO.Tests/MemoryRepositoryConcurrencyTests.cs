// <copyright file="MemoryRepositoryConcurrencyTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.Persistence;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests concurrent creation, saving, and reads in the shared demo repository.
/// </summary>
[TestFixture]
public class MemoryRepositoryConcurrencyTests
{
    /// <summary>
    /// Concurrent demo workers can save newly keyed entities while other workers enumerate snapshots.
    /// </summary>
    [Test]
    public async Task ConcurrentWorkersSaveAndReadEntitiesAsync()
    {
        const int workers = 8;
        const int perWorker = 100;
        var repository = new MemoryRepository<Entity>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = Enumerable.Range(0, workers).Select(async _ =>
        {
            await start.Task.ConfigureAwait(false);
            for (var index = 0; index < perWorker; index++)
            {
                var id = Guid.NewGuid();
                repository.Add(Guid.NewGuid(), new Entity(id));
                repository.OnSaveChanges();
                Assert.That((await repository.GetByIdAsync(id).ConfigureAwait(false))?.Id, Is.EqualTo(id));
                var snapshot = await repository.GetAllAsync().ConfigureAwait(false);
                Assert.That(snapshot.Select(value => value.Id).Distinct().Count(), Is.EqualTo(snapshot.Count()));
            }
        }).ToArray();
        start.SetResult();

        await Task.WhenAll(writers).ConfigureAwait(false);

        Assert.That((await repository.GetAllAsync().ConfigureAwait(false)).Count(), Is.EqualTo(workers * perWorker));
    }

    /// <summary>
    /// Adapters using the non-generic API get a stable view while other workers change the repository.
    /// </summary>
    [Test]
    public async Task NonGenericReadsUseSnapshotsAsync()
    {
        var repository = new MemoryRepository<Entity>();
        var originalId = Guid.NewGuid();
        var original = new Entity(originalId);
        repository.Add(originalId, original);
        var snapshot = await ((IRepository)repository).GetAllAsync().ConfigureAwait(false);

        await repository.DeleteAsync(originalId).ConfigureAwait(false);
        var replacementId = Guid.NewGuid();
        repository.Add(replacementId, new Entity(replacementId));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot, Has.Count.EqualTo(1));
            Assert.That(snapshot.Cast<Entity>(), Is.EqualTo(new[] { original }));
        });
    }

    private sealed record Entity(Guid Id);
}
