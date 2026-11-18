// <copyright file="GiftBoxAudienceTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.GiftBox;

/// <summary>Exact account identity, explicit bulk selection and truthful recent-activity filtering.</summary>
[TestFixture]
public class GiftBoxAudienceTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);
    private static readonly GiftBoxRecipient Ann = new(Guid.Parse("d778240e-808d-4962-b594-f3ab55865580"), "Ann", Now.AddDays(-1));
    private static readonly GiftBoxRecipient Bob = new(Guid.Parse("1ff44c9c-a381-4d5d-be83-51a68e7b6723"), "Bob", null);

    /// <summary>A single login or a pasted mixed-separator list preserves exact case and removes duplicates.</summary>
    [Test]
    public void SingleAndPastedListsResolveExactAccounts()
    {
        Assert.That(GiftBoxAudience.Resolve([Ann, Bob], "Ann", null, Now), Is.EqualTo(new[] { Ann }));
        var selected = GiftBoxAudience.Resolve([Ann, Bob], " Bob;Ann\r\nBob,\tAnn ", null, Now);
        Assert.That(selected.Select(account => account.AccountId), Is.EquivalentTo(new[] { Ann.AccountId, Bob.AccountId }));
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann, Bob], "ann", null, Now));
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann, Bob], "Ann missing", null, Now));
    }

    /// <summary>An empty typed selection cannot accidentally mean everyone.</summary>
    [Test]
    public void AllRequiresExplicitNullSelection()
    {
        Assert.That(GiftBoxAudience.Resolve([Ann, Bob], null, null, Now), Has.Count.EqualTo(2));
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann, Bob], " ,;\n", null, Now));
    }

    /// <summary>Only observed recent activity or a live connection qualifies; missing/future dates are not invented.</summary>
    [Test]
    public void RecentFilterUsesObservedActivityAndOnlineState()
    {
        var future = new GiftBoxRecipient(Guid.NewGuid(), "Future", Now.AddDays(1));
        var old = new GiftBoxRecipient(Guid.NewGuid(), "Old", Now.AddDays(-2).AddTicks(-1));
        var boundary = new GiftBoxRecipient(Guid.NewGuid(), "Boundary", Now.AddDays(-2));
        var selected = GiftBoxAudience.Resolve([Ann, Bob with { Online = true }, future, old, boundary], null, 2, Now);
        Assert.That(selected.Select(account => account.Login), Is.EquivalentTo(new[] { "Ann", "Bob", "Boundary" }));
        Assert.That(GiftBoxAudience.Resolve([Bob], null, 2, Now), Is.Empty);
    }

    /// <summary>Ambiguous account identity or invalid clocks/days fail before any send can occur.</summary>
    [Test]
    public void InvalidSnapshotsAndFiltersAreRefused()
    {
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann, Ann], null, null, Now));
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann], null, 0, Now));
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann], null, 3651, Now));
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann], null, null, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => GiftBoxAudience.Resolve([Ann with { AccountId = Guid.Empty }], null, null, Now));
    }
}
