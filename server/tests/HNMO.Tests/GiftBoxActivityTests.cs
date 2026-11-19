// <copyright file="GiftBoxActivityTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.Persistence;
using Moq;

/// <summary>Recent-player selection uses observed UTC activity without inventing historical timestamps.</summary>
[TestFixture]
public class GiftBoxActivityTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>Only observed accounts appear, and callers cannot change the store by editing a read snapshot.</summary>
    [Test]
    public void ObservationsAreDetachedAndUnknownAccountsStayUnknown()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var activity = new GiftBoxActivity(backend);
        var account = Guid.NewGuid();
        activity.Observe(account, Now);
        var snapshot = activity.Read();
        Assert.That(snapshot, Has.Count.EqualTo(1));
        Assert.That(snapshot[account], Is.EqualTo(Now));
        ((Dictionary<Guid, DateTime>)snapshot).Clear();
        Assert.That(new GiftBoxActivity(backend).Read()[account], Is.EqualTo(Now));
        Assert.That(backend.CountRows(GiftBoxActivity.Feature), Is.EqualTo(1));
    }

    /// <summary>An out-of-order observation never makes a known account appear less recently active.</summary>
    [Test]
    public void OlderObservationDoesNotRegressActivity()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var account = Guid.NewGuid();
        new GiftBoxActivity(backend).Observe(account, Now);
        new GiftBoxActivity(backend).Observe(account, Now.AddDays(-1));
        Assert.That(new GiftBoxActivity(backend).Read()[account], Is.EqualTo(Now));
    }

    /// <summary>A lost commit response reloads persisted observations before comparing the next event.</summary>
    [Test]
    public void UncertainCommitIsReloadedBeforeAnotherObservation()
    {
        var inner = new InMemoryHnmoPlayerStateRowBackend();
        var backend = new Mock<IHnmoPlayerStateRowBackend>();
        backend.Setup(value => value.LoadAll(GiftBoxActivity.Feature)).Returns(() => inner.LoadAll(GiftBoxActivity.Feature));
        var first = true;
        backend.Setup(value => value.UpsertBatch(It.IsAny<IReadOnlyList<HnmoFeatureRow>>(), It.IsAny<IReadOnlyList<(string Feature, string ScopeId)>>())).Callback((IReadOnlyList<HnmoFeatureRow> rows, IReadOnlyList<(string Feature, string ScopeId)> deletes) =>
        {
            inner.UpsertBatch(rows, deletes);
            if (first)
            {
                first = false;
                throw new IOException("committed response lost");
            }
        });
        var activity = new GiftBoxActivity(backend.Object);
        var account = Guid.NewGuid();
        Assert.Throws<IOException>(() => activity.Observe(account, Now));
        activity.Observe(account, Now.AddMinutes(-1));
        Assert.That(activity.Read()[account], Is.EqualTo(Now));
        activity.Observe(account, Now.AddMinutes(1));
        Assert.That(activity.Read()[account], Is.EqualTo(Now.AddMinutes(1)));
    }

    /// <summary>A failed uncommitted write cannot become a fabricated saved last-seen timestamp.</summary>
    [Test]
    public void FailedObservationDoesNotInventLastSeen()
    {
        var inner = new InMemoryHnmoPlayerStateRowBackend();
        var backend = new Mock<IHnmoPlayerStateRowBackend>();
        backend.Setup(value => value.LoadAll(GiftBoxActivity.Feature)).Returns(() => inner.LoadAll(GiftBoxActivity.Feature));
        backend.Setup(value => value.UpsertBatch(It.IsAny<IReadOnlyList<HnmoFeatureRow>>(), It.IsAny<IReadOnlyList<(string Feature, string ScopeId)>>())).Throws(new IOException("not committed"));
        var activity = new GiftBoxActivity(backend.Object);
        Assert.Throws<IOException>(() => activity.Observe(Guid.NewGuid(), Now));
        Assert.That(activity.Read(), Is.Empty);
    }

    /// <summary>Missing account identity and local/unspecified dates cannot enter a recent-player cohort.</summary>
    [Test]
    public void InvalidObservationIsRefused()
    {
        var activity = new GiftBoxActivity(new InMemoryHnmoPlayerStateRowBackend());
        Assert.Throws<ArgumentException>(() => activity.Observe(Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => activity.Observe(Guid.NewGuid(), DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.That(activity.Read(), Is.Empty);
    }
}
