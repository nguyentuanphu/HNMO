// <copyright file="GiftBoxDispatchTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;

/// <summary>Immutable recipient snapshots, send recovery, privacy and owed-message persistence.</summary>
[TestFixture]
public class GiftBoxDispatchTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Ann = Guid.Parse("d778240e-808d-4962-b594-f3ab55865580");
    private static readonly Guid Bob = Guid.Parse("1ff44c9c-a381-4d5d-be83-51a68e7b6723");

    /// <summary>A repeated send freezes the original resolved audience, time, messages and item options.</summary>
    [Test]
    public void FrozenAudienceAndRewardSurviveReplayAndNewService()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var first = new GiftBoxDispatch(backend);
        var id = Guid.NewGuid();
        var reward = Reward();
        Assert.That(Send(first, id, [Ann, Bob, Ann], reward), Is.EqualTo(2));
        reward.Items[0].Level = 15;
        var restarted = new GiftBoxDispatch(backend);
        Assert.That(Send(restarted, id, [Bob, Ann], Reward(), Now.AddHours(1)), Is.EqualTo(2));
        var letter = restarted.Read(Ann).Single();
        Assert.Multiple(() =>
        {
            Assert.That(letter.Id, Is.EqualTo(id));
            Assert.That(letter.SentAtUtc, Is.EqualTo(Now));
            Assert.That(letter.TitleVi, Is.EqualTo("Quà sự kiện"));
            Assert.That(letter.MessageVi, Is.EqualTo("Cảm ơn bạn đã tham gia."));
            Assert.That(letter.Reward.Items.Single().Level, Is.EqualTo(3));
            Assert.That(letter.Reward.Items.Single().ExcellentOptions, Is.EqualTo(5));
            Assert.That(backend.CountRows(GiftBoxDispatch.SendFeature), Is.EqualTo(1));
            Assert.That(backend.CountRows(GiftBoxDispatch.LetterFeature), Is.EqualTo(2));
        });
    }

    /// <summary>A browser mutation of a returned letter cannot change a stored reward.</summary>
    [Test]
    public void ReadIsDetachedAndAccountOwned()
    {
        var box = new GiftBoxDispatch(new InMemoryHnmoPlayerStateRowBackend());
        Send(box, Guid.NewGuid(), [Ann]);
        box.Read(Ann).Single().Reward.Items.Clear();
        Assert.That(box.Read(Ann).Single().Reward.Items, Has.Count.EqualTo(1));
        Assert.That(box.Read(Bob), Is.Empty);
        Assert.Throws<ArgumentException>(() => box.Read(Guid.Empty));
    }

    /// <summary>Private host attribution is persisted separately and never appears in player letters.</summary>
    [Test]
    public void HostReasonDoesNotLeakIntoPlayerContract()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var box = new GiftBoxDispatch(backend);
        Send(box, Guid.NewGuid(), [Ann]);
        var player = JsonSerializer.Serialize(box.Read(Ann));
        Assert.That(player, Does.Not.Contain("private-host-identity").And.Not.Contain("private-host-reason"));
        Assert.That(backend.LoadAll(GiftBoxDispatch.SendFeature).Single().PayloadJson, Does.Contain("private-host-identity").And.Contain("private-host-reason"));
    }

    /// <summary>A reused identifier refuses changes to audience, reward or host attribution.</summary>
    [Test]
    public void ChangedSendCannotReuseItsIdentifier()
    {
        var box = new GiftBoxDispatch(new InMemoryHnmoPlayerStateRowBackend());
        var id = Guid.NewGuid();
        Send(box, id, [Ann]);
        Assert.Throws<InvalidOperationException>(() => Send(box, id, [Ann, Bob]));
        Assert.Throws<InvalidOperationException>(() => Send(box, id, [Ann], new GiftCodeReward { Coins = 11 }));
        Assert.Throws<InvalidOperationException>(() => box.Send(id, "another-host", "private-host-reason", [Ann], "Event gift", "Quà sự kiện", "Thank you for joining.", "Cảm ơn bạn đã tham gia.", Reward(), Now));
        Assert.That(box.Read(Bob), Is.Empty);
    }

    /// <summary>An unsuccessful atomic write leaves neither partial mail nor a send proof.</summary>
    [Test]
    public void FailureBeforeCommitCanBeRetried()
    {
        var backend = new FailingBackend { FailBeforeCommit = true };
        var box = new GiftBoxDispatch(backend);
        var id = Guid.NewGuid();
        Assert.Throws<IOException>(() => Send(box, id, [Ann, Bob]));
        Assert.That(backend.CountRows(), Is.Zero);
        backend.FailBeforeCommit = false;
        Assert.That(Send(box, id, [Ann, Bob]), Is.EqualTo(2));
    }

    /// <summary>An ambiguous committed write replays its proof rather than creating more rewards.</summary>
    [Test]
    public void FailureAfterCommitReplaysOriginalLetters()
    {
        var backend = new FailingBackend { FailAfterCommit = true };
        var id = Guid.NewGuid();
        Assert.Throws<IOException>(() => Send(new GiftBoxDispatch(backend), id, [Ann, Bob]));
        backend.FailAfterCommit = false;
        Assert.That(Send(new GiftBoxDispatch(backend), id, [Ann, Bob], now: Now.AddDays(1)), Is.EqualTo(2));
        Assert.That(backend.CountRows(GiftBoxDispatch.LetterFeature), Is.EqualTo(2));
    }

    /// <summary>Owed rewards without a deadline remain available; expiry is exact and never consumes mail.</summary>
    [Test]
    public void ExpiryDoesNotConsumeOwedReward()
    {
        var box = new GiftBoxDispatch(new InMemoryHnmoPlayerStateRowBackend());
        var id = Guid.NewGuid();
        Send(box, id, [Ann], expiry: Now.AddHours(1));
        var letter = box.Read(Ann).Single();
        Assert.That(letter.IsExpired(Now.AddHours(1).AddTicks(-1)), Is.False);
        Assert.That(letter.IsExpired(Now.AddHours(1)), Is.True);
        Assert.That(Send(box, id, [Ann], now: Now.AddHours(2), expiry: Now.AddHours(1)), Is.EqualTo(1));
        Assert.That(box.Read(Ann), Has.Count.EqualTo(1));
        Assert.Throws<ArgumentException>(() => Send(box, Guid.NewGuid(), [Bob], expiry: Now));
        Send(box, Guid.NewGuid(), [Bob]);
        Assert.That(box.Read(Bob).Single().IsExpired(Now.AddYears(10)), Is.False);
    }

    /// <summary>Missing translations, invalid recipients and non-UTC dates never create a send.</summary>
    [Test]
    public void InvalidRequestCreatesNothing()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var box = new GiftBoxDispatch(backend);
        Assert.Throws<ArgumentException>(() => Send(box, Guid.Empty, [Ann]));
        Assert.Throws<ArgumentException>(() => Send(box, Guid.NewGuid(), []));
        Assert.Throws<ArgumentException>(() => Send(box, Guid.NewGuid(), [Guid.Empty]));
        Assert.Throws<ArgumentException>(() => Send(box, Guid.NewGuid(), [Ann], now: DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => box.Send(Guid.NewGuid(), "host", "reason", [Ann], "Title", " ", "Text", "Nội dung", Reward(), Now));
        Assert.That(backend.CountRows(), Is.Zero);
    }

    /// <summary>Invalid or empty rewards are refused instead of silently reducing a host's promise.</summary>
    [Test]
    public void InvalidRewardCreatesNothing()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var box = new GiftBoxDispatch(backend);
        foreach (var reward in new[] { new GiftCodeReward(), new GiftCodeReward { Coins = -1 }, new GiftCodeReward { Zen = -1 }, new GiftCodeReward { Coins = 1000001 }, new GiftCodeReward { Coins = 1, Items = [new() { Count = 51 }] }, new GiftCodeReward { Coins = 1, Items = [new() { ExcellentOptions = 64 }] } })
        {
            Assert.Throws<ArgumentException>(() => Send(box, Guid.NewGuid(), [Ann], reward));
        }

        Assert.That(backend.CountRows(), Is.Zero);
    }

    /// <summary>Concurrent services sharing a backend issue only one letter for each account.</summary>
    [Test]
    public void ConcurrentRetriesShareTheSendProof()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var id = Guid.NewGuid();
        Parallel.For(0, 12, _ => Send(new GiftBoxDispatch(backend), id, [Ann, Bob]));
        Assert.That(backend.CountRows(GiftBoxDispatch.SendFeature), Is.EqualTo(1));
        Assert.That(backend.CountRows(GiftBoxDispatch.LetterFeature), Is.EqualTo(2));
    }

    /// <summary>A retry uses the original resolved recipients even if a newer all/recent cohort would differ.</summary>
    [Test]
    public void BulkRetryReadsItsOriginalFrozenAudience()
    {
        var box = new GiftBoxDispatch(new InMemoryHnmoPlayerStateRowBackend());
        var id = Guid.NewGuid();
        Send(box, id, [Ann, Bob]);
        var original = box.ReadFrozenAudience(id)!;
        Assert.That(original, Is.EquivalentTo(new[] { Ann, Bob }));
        Assert.That(Send(box, id, original, now: Now.AddDays(1)), Is.EqualTo(2));
        Assert.That(box.ReadFrozenAudience(Guid.NewGuid()), Is.Null);
        ((Guid[])original)[0] = Guid.NewGuid();
        Assert.That(box.ReadFrozenAudience(id), Is.EquivalentTo(new[] { Ann, Bob }));
    }

    /// <summary>The initial proof format recovers its audience from complete immutable letter rows.</summary>
    [Test]
    public void LegacyProofRecoversAudienceWithoutRecalculatingIt()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var box = new GiftBoxDispatch(backend);
        var id = Guid.NewGuid();
        Send(box, id, [Ann, Bob]);
        var row = backend.LoadAll(GiftBoxDispatch.SendFeature).Single();
        var json = JsonNode.Parse(row.PayloadJson)!.AsObject();
        json.Remove("accountIds");
        backend.UpsertBatch([row with { PayloadJson = json.ToJsonString() }], []);
        Assert.That(box.ReadFrozenAudience(id), Is.EquivalentTo(new[] { Ann, Bob }));
        backend.UpsertBatch([], [(GiftBoxDispatch.LetterFeature, $"{Bob:N}:{id:N}")]);
        Assert.Throws<InvalidDataException>(() => box.ReadFrozenAudience(id));
    }

    private static GiftCodeReward Reward() => new() { Coins = 10, Zen = 100, Items = [new() { Group = 0, Number = 1, Level = 3, Luck = true, Skill = true, OptionLevel = 2, ExcellentOptions = 5, Count = 2 }] };

    private static int Send(GiftBoxDispatch box, Guid id, IEnumerable<Guid> accounts, GiftCodeReward? reward = null, DateTime? now = null, DateTime? expiry = null) => box.Send(id, "private-host-identity", "private-host-reason", accounts, "Event gift", "Quà sự kiện", "Thank you for joining.", "Cảm ơn bạn đã tham gia.", reward ?? Reward(), now ?? Now, expiry);

    private sealed class FailingBackend : IHnmoPlayerStateRowBackend
    {
        private readonly InMemoryHnmoPlayerStateRowBackend _inner = new();

        public bool FailBeforeCommit { get; set; }

        public bool FailAfterCommit { get; set; }

        public void EnsureSchema() => this._inner.EnsureSchema();

        public IReadOnlyList<HnmoFeatureRow> LoadAll(string feature) => this._inner.LoadAll(feature);

        public int ArchiveExpired(DateTime cutoffUtc, string? feature = null) => this._inner.ArchiveExpired(cutoffUtc, feature);

        public int CountRows(string? feature = null) => this._inner.CountRows(feature);

        public int CountArchivedRows(string? feature = null) => this._inner.CountArchivedRows(feature);

        public void UpsertBatch(IReadOnlyList<HnmoFeatureRow> upserts, IReadOnlyList<(string Feature, string ScopeId)> deletes)
        {
            if (this.FailBeforeCommit)
            {
                throw new IOException("Not committed.");
            }

            this._inner.UpsertBatch(upserts, deletes);
            if (this.FailAfterCommit)
            {
                throw new IOException("Committed, response lost.");
            }
        }
    }
}
