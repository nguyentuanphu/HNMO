// <copyright file="GiftBoxQueryTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using Moq;

/// <summary>Public inbox reads stay bounded and account-owned instead of loading unrelated message payloads.</summary>
[TestFixture]
public class GiftBoxQueryTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>The query-capable path reads exact letter/proof keys and only an owned page.</summary>
    [Test]
    public void ReadsUseBoundedQueriesInsteadOfGlobalPayloadLoads()
    {
        var inner = new InMemoryHnmoPlayerStateRowBackend();
        var query = new Mock<IHnmoPlayerStateRowQueryBackend>();
        query.Setup(value => value.LoadAll(It.IsAny<string>())).Throws(new InvalidOperationException("A bounded inbox may not load all player messages."));
        query.Setup(value => value.FindRow(It.IsAny<string>(), It.IsAny<string>())).Returns((string feature, string scope) => inner.LoadAll(feature).SingleOrDefault(row => row.ScopeId == scope));
        query.Setup(value => value.LoadScopePage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns((string feature, string prefix, int offset, int limit) => inner.LoadAll(feature).Where(row => row.ScopeId.StartsWith(prefix, StringComparison.Ordinal)).OrderByDescending(row => row.UpdatedAtUtc).ThenBy(row => row.ScopeId, StringComparer.Ordinal).Skip(offset).Take(limit).ToArray());
        query.Setup(value => value.UpsertBatch(It.IsAny<IReadOnlyList<HnmoFeatureRow>>(), It.IsAny<IReadOnlyList<(string Feature, string ScopeId)>>())).Callback((IReadOnlyList<HnmoFeatureRow> rows, IReadOnlyList<(string Feature, string ScopeId)> deletes) => inner.UpsertBatch(rows, deletes));
        var box = new GiftBoxDispatch(query.Object);
        var ann = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var first = Send(box, ann, Now);
        var second = Send(box, ann, Now.AddMinutes(1));
        var foreign = Send(box, bob, Now.AddMinutes(2));
        Assert.That(box.ReadLetter(ann, first)!.AccountId, Is.EqualTo(ann));
        Assert.That(box.ReadLetter(ann, foreign), Is.Null);
        Assert.That(box.ReadPage(ann, 0, 1).Single().Id, Is.EqualTo(second));
        Assert.That(box.ReadPage(ann, 1, 1).Single().Id, Is.EqualTo(first));
        Assert.That(box.ReadPage(ann, 2, 1), Is.Empty);
        Assert.That(box.ReadFrozenAudience(first), Is.EqualTo(new[] { ann }));
        query.Verify(value => value.LoadAll(It.IsAny<string>()), Times.Never);
    }

    /// <summary>A backend cannot bypass the limit or return another account's payload to the inbox.</summary>
    [Test]
    public void InvalidBackendPagesFailClosed()
    {
        var inner = new InMemoryHnmoPlayerStateRowBackend();
        var seed = new GiftBoxDispatch(inner);
        var ann = Guid.NewGuid();
        var bob = Guid.NewGuid();
        Send(seed, ann, Now);
        Send(seed, bob, Now);
        var rows = inner.LoadAll(GiftBoxDispatch.LetterFeature);
        var query = new Mock<IHnmoPlayerStateRowQueryBackend>();
        query.Setup(value => value.LoadScopePage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns(rows);
        var box = new GiftBoxDispatch(query.Object);
        Assert.Throws<InvalidDataException>(() => box.ReadPage(ann, 0, 1));
        var foreign = rows.Single(row => row.ScopeId.StartsWith(bob.ToString("N"), StringComparison.Ordinal));
        query.Setup(value => value.LoadScopePage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns([foreign]);
        Assert.Throws<InvalidDataException>(() => box.ReadPage(ann, 0, 1));
        query.Setup(value => value.FindRow(It.IsAny<string>(), It.IsAny<string>())).Returns(foreign);
        Assert.Throws<InvalidDataException>(() => box.ReadLetter(ann, Guid.NewGuid()));
    }

    /// <summary>Invalid page sizes or identities are refused before a database request.</summary>
    [Test]
    public void InvalidPageCannotReachTheBackend()
    {
        var query = new Mock<IHnmoPlayerStateRowQueryBackend>(MockBehavior.Strict);
        var box = new GiftBoxDispatch(query.Object);
        Assert.Throws<ArgumentException>(() => box.ReadPage(Guid.Empty, 0, 20));
        Assert.Throws<ArgumentException>(() => box.ReadPage(Guid.NewGuid(), -1, 20));
        Assert.Throws<ArgumentException>(() => box.ReadPage(Guid.NewGuid(), 0, 102));
        Assert.Throws<ArgumentException>(() => box.ReadLetter(Guid.Empty, Guid.NewGuid()));
        query.VerifyNoOtherCalls();
    }

    private static Guid Send(GiftBoxDispatch box, Guid account, DateTime atUtc)
    {
        var id = Guid.NewGuid();
        box.Send(id, "host", "event", [account], "Gift", "Quà", "Thanks", "Cảm ơn", new GiftCodeReward { Coins = 1 }, atUtc);
        return id;
    }
}
