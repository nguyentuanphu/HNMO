// <copyright file="LoginAttemptGuardTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using NUnit.Framework;

/// <summary>
/// Password guessing is slowed down by locking accounts and addresses for a while.
/// </summary>
[TestFixture]
public class LoginAttemptGuardTests
{
    private DateTime _now = new(2026, 11, 15, 3, 0, 0, DateTimeKind.Utc);

    /// <summary>A fresh guard with a controllable clock.</summary>
    [SetUp]
    public void SetUp()
    {
        LoginAttemptGuard.Reset();
        LoginAttemptGuard.Now = () => this._now;
    }

    /// <summary>Restores the clock.</summary>
    [TearDown]
    public void TearDown()
    {
        LoginAttemptGuard.Now = () => DateTime.UtcNow;
        LoginAttemptGuard.Reset();
    }

    /// <summary>Five wrong passwords lock the account for ten minutes; other accounts aren't affected.</summary>
    [Test]
    public void FiveFailuresLockTheAccount()
    {
        for (var i = 0; i < 4; i++)
        {
            Assert.That(LoginAttemptGuard.RecordFailure("player1", null), Is.False);
        }

        Assert.That(LoginAttemptGuard.IsLocked("player1", null), Is.False);
        Assert.That(LoginAttemptGuard.RecordFailure("PLAYER1", null), Is.True, "case doesn't matter");
        Assert.That(LoginAttemptGuard.IsLocked("player1", null), Is.True);
        Assert.That(LoginAttemptGuard.IsLocked("player2", null), Is.False);
        this._now += TimeSpan.FromMinutes(11);
        Assert.That(LoginAttemptGuard.IsLocked("player1", null), Is.False, "unlocked after ten minutes");
    }

    /// <summary>Failures older than the window don't add up, and a successful login clears them.</summary>
    [Test]
    public void OldFailuresAndSuccessReset()
    {
        for (var i = 0; i < 4; i++)
        {
            LoginAttemptGuard.RecordFailure("player1", null);
        }

        this._now += TimeSpan.FromMinutes(11);
        Assert.That(LoginAttemptGuard.RecordFailure("player1", null), Is.False);
        for (var i = 0; i < 3; i++)
        {
            LoginAttemptGuard.RecordFailure("player1", null);
        }

        LoginAttemptGuard.RecordSuccess("player1");
        Assert.That(LoginAttemptGuard.RecordFailure("player1", null), Is.False);
    }

    /// <summary>Twenty failures from one address lock the address; the web server's local address never.</summary>
    [Test]
    public void AddressesAreLockedButNotTheLocalOne()
    {
        for (var i = 0; i < 20; i++)
        {
            LoginAttemptGuard.RecordFailure($"guess{i}", "203.0.113.7:51234");
            LoginAttemptGuard.RecordFailure($"web{i}", "127.0.0.1:40000");
        }

        Assert.That(LoginAttemptGuard.IsLocked("someone", "203.0.113.7:60000"), Is.True);
        Assert.That(LoginAttemptGuard.IsLocked("someone", "127.0.0.1:40001"), Is.False);
    }
}
