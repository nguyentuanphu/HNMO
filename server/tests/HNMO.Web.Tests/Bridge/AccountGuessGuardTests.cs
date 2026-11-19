// <copyright file="AccountGuessGuardTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Bridge;

/// <summary>Password guessing on the website is stopped per account, not only per address (audit 2.8).</summary>
[TestFixture]
public class AccountGuessGuardTests
{
    /// <summary>Ten refused passwords lock the account for 15 minutes; it unlocks on its own.</summary>
    [Test]
    public void TenFailuresLockTheAccountForAWhile()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 11, 19, 14, 0, 0, TimeSpan.Zero));
        var guard = new AccountGuessGuard(clock);
        for (var i = 1; i < AccountGuessGuard.MaximumFailures; i++)
        {
            Assert.That(guard.RecordFailure("test1"), Is.False);
        }

        Assert.That(guard.IsLocked("TEST1"), Is.False);
        Assert.That(guard.RecordFailure("test1"), Is.True);
        Assert.That(guard.IsLocked("test1"), Is.True);
        Assert.That(guard.IsLocked("test2"), Is.False, "other accounts are not affected");

        guard.RecordSuccess("test1");
        Assert.That(guard.IsLocked("test1"), Is.True, "a right password during the lock doesn't lift it");

        clock.Now += AccountGuessGuard.LockTime + TimeSpan.FromSeconds(1);
        Assert.That(guard.IsLocked("test1"), Is.False);
    }

    /// <summary>Failures spread over more than the window don't add up; a right password forgets them.</summary>
    [Test]
    public void OldFailuresAndARightPasswordDontCount()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 11, 19, 14, 0, 0, TimeSpan.Zero));
        var guard = new AccountGuessGuard(clock);
        for (var i = 0; i < AccountGuessGuard.MaximumFailures - 1; i++)
        {
            guard.RecordFailure("a");
        }

        clock.Now += AccountGuessGuard.Window + TimeSpan.FromSeconds(1);
        Assert.That(guard.RecordFailure("a"), Is.False, "the window started again");

        for (var i = 0; i < AccountGuessGuard.MaximumFailures - 2; i++)
        {
            guard.RecordFailure("a");
        }

        guard.RecordSuccess("a");
        Assert.That(guard.RecordFailure("a"), Is.False, "forgotten after a right password");
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => this.Now;
    }
}
