// <copyright file="RageSkillAttackTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.GameServer.RemoteView.World;
using HNMO.Pathfinding;

/// <summary>
/// Tests the rage fighter's attack messages: the dark side target list (C1 4B) and the fighter attack (C3 4A), as the
/// game client reads them, and how the targets are chosen.
/// </summary>
[TestFixture]
public class RageSkillAttackTest
{
    /// <summary>
    /// The target list is 16 bytes: C1, length, 4B, a byte, the skill (little endian) and five target ids (little
    /// endian), 10000 in the slots without a target.
    /// </summary>
    [Test]
    public async ValueTask DarkSideTargetListAsync()
    {
        var (player, output) = CastleSiegeRemoteViewTestHelper.CreatePlayer();
        var targets = new[] { Identifiable(0x1234), Identifiable(0x0042) };

        await new ShowRageAttackRangePlugIn(player).ShowRageAttackRangeAsync(263, targets).ConfigureAwait(false);

        var data = output.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(data, Has.Length.EqualTo(16));
            Assert.That(data[0], Is.EqualTo(0xC1));
            Assert.That(data[1], Is.EqualTo(16));
            Assert.That(data[2], Is.EqualTo(0x4B));
            Assert.That(data[4] | (data[5] << 8), Is.EqualTo(263));
            var ids = Enumerable.Range(0, 5).Select(i => data[6 + (i * 2)] | (data[7 + (i * 2)] << 8)).ToArray();
            Assert.That(ids, Is.EqualTo(new[] { 0x1234, 0x0042, 10000, 10000, 10000 }));
        });
    }

    /// <summary>
    /// The fighter attack is C3, 9, 4A with the skill, the attacker and the target (with the success bit) in big endian.
    /// </summary>
    [Test]
    public async ValueTask FighterAttackAsync()
    {
        var (player, output) = CastleSiegeRemoteViewTestHelper.CreatePlayer();

        await new ShowRageAttackPlugIn(player).ShowAttackAsync(Identifiable(0x0123), Identifiable(0x0456), 559).ConfigureAwait(false);

        var data = output.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(data, Has.Length.EqualTo(9));
            Assert.That(data[0], Is.EqualTo(0xC3));
            Assert.That(data[2], Is.EqualTo(0x4A));
            Assert.That((data[3] << 8) | data[4], Is.EqualTo(559));
            Assert.That((data[5] << 8) | data[6], Is.EqualTo(0x0123));
            Assert.That((data[7] << 8) | data[8], Is.EqualTo(0x8456));
        });
    }

    /// <summary>
    /// The explicit target comes first, then the nearest other non-player targets around it, five at most.
    /// </summary>
    [Test]
    public void TargetsExplicitFirstThenNearestMonsters()
    {
        var fighter = Attackable(1, 100, 100);
        var explicitTarget = Attackable(2, 110, 100);
        var candidates = new List<IAttackable>
        {
            fighter, explicitTarget,
            Attackable(3, 116, 100), Attackable(4, 111, 100), Attackable(5, 113, 100),
            Attackable(6, 112, 100), Attackable(7, 114, 100), Attackable(8, 115, 100),
        };

        var targets = RageSkillAttackAction.SelectTargets(fighter, explicitTarget, explicitTarget.Position, candidates);

        Assert.That(targets.Select(t => t.Id), Is.EqualTo(new ushort[] { 2, 4, 6, 5, 7 }));
    }

    /// <summary>
    /// A target a little beyond the skill's range is still in reach (the client casts from up to 1.2 times the range).
    /// </summary>
    [Test]
    public void TargetInReachWithTolerance()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RageSkillAttackAction.IsTargetInReach(new Point(100, 100), new Point(104, 100), 4), Is.True);
            Assert.That(RageSkillAttackAction.IsTargetInReach(new Point(100, 100), new Point(105, 100), 4), Is.True, "4.8 cells: the client's reach for range 4");
            Assert.That(RageSkillAttackAction.IsTargetInReach(new Point(100, 100), new Point(106, 100), 4), Is.True);
            Assert.That(RageSkillAttackAction.IsTargetInReach(new Point(100, 100), new Point(107, 100), 4), Is.False);
        });
    }

    private static IIdentifiable Identifiable(ushort id)
    {
        var mock = new Mock<IIdentifiable>();
        mock.SetupGet(i => i.Id).Returns(id);
        return mock.Object;
    }

    private static IAttackable Attackable(ushort id, byte x, byte y)
    {
        var mock = new Mock<IAttackable>();
        mock.SetupGet(a => a.Id).Returns(id);
        mock.SetupGet(a => a.Position).Returns(new Point(x, y));
        return mock.Object;
    }
}
