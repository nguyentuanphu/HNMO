// <copyright file="SkillDelayTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests that the server checks the skills' delays (classic CheckSkillDelay), with the delays of the Season 6/9 server suites.
/// </summary>
[TestFixture]
public class SkillDelayTest
{
    private const short EarthshakeNumber = 62;
    private const short EnergyBallNumber = 17;
    private const short ChainDriveNumber = 262;
    private const int ManaCost = 10;

    /// <summary>
    /// Earthshake (10 seconds) can't be used again at once; the refused use costs no mana.
    /// </summary>
    [Test]
    public async ValueTask SkillCantBeUsedAgainBeforeItsDelayAsync()
    {
        var (player, skillEntry) = await CreatePlayerWithSkillAsync(EarthshakeNumber).ConfigureAwait(false);

        var first = await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false);
        var second = await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False, "Used again before the delay passed.");
            Assert.That(player.Attributes![Stats.CurrentMana], Is.EqualTo(ManaCost * 9));
        });
    }

    /// <summary>
    /// After its delay Earthshake can be used again.
    /// </summary>
    [Test]
    public async ValueTask SkillCanBeUsedAgainAfterItsDelayAsync()
    {
        var (player, skillEntry) = await CreatePlayerWithSkillAsync(EarthshakeNumber).ConfigureAwait(false);
        Assert.That(await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false), Is.True);

        player.SkillLastUses[EarthshakeNumber] = DateTime.UtcNow - TimeSpan.FromSeconds(10);

        Assert.That(await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false), Is.True);
    }

    /// <summary>
    /// A skill without delay (Energy Ball) can be used again at once.
    /// </summary>
    [Test]
    public async ValueTask SkillWithoutDelayIsNotLimitedAsync()
    {
        var (player, skillEntry) = await CreatePlayerWithSkillAsync(EnergyBallNumber).ConfigureAwait(false);

        var first = await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false);
        var second = await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false);

        Assert.That(new[] { first, second }, Is.All.True);
    }

    /// <summary>
    /// Chain Drive sends two hits for one cast; the second isn't refused.
    /// </summary>
    [Test]
    public async ValueTask ChainDriveSecondHitIsNotRefusedAsync()
    {
        var (player, skillEntry) = await CreatePlayerWithSkillAsync(ChainDriveNumber).ConfigureAwait(false);

        var first = await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false);
        var second = await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false);

        Assert.That(new[] { first, second }, Is.All.True);
    }

    /// <summary>
    /// The delay check allows a cast which arrives a bit early (80 % of the delay), but not earlier.
    /// </summary>
    [Test]
    public void ToleranceForNetworkJitter()
    {
        var lastUse = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.Multiple(() =>
        {
            Assert.That(SkillDelays.HasPassed(EarthshakeNumber, lastUse, lastUse.AddSeconds(8)), Is.True);
            Assert.That(SkillDelays.HasPassed(EarthshakeNumber, lastUse, lastUse.AddSeconds(7.9)), Is.False);
            Assert.That(SkillDelays.HasPassed(EarthshakeNumber, null, lastUse), Is.True);
        });
    }

    private static async ValueTask<(Player Player, SkillEntry SkillEntry)> CreatePlayerWithSkillAsync(short skillNumber)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var skill = new BasicModel.Skill { Number = skillNumber, Range = 3 };
        skill.ConsumeRequirements.Add(new BasicModel.AttributeRequirement { Attribute = Stats.CurrentMana, MinimumValue = ManaCost });
        await player.SkillList!.AddLearnedSkillAsync(skill).ConfigureAwait(false);
        player.Attributes![Stats.CurrentMana] = ManaCost * 10;
        return (player, player.SkillList.GetSkill((ushort)skillNumber)!);
    }
}
