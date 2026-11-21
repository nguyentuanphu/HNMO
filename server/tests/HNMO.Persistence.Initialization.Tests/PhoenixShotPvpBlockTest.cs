// <copyright file="PhoenixShotPvpBlockTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Phoenix Shot's decrease block: against monsters it lowers the defense rate against monsters by 50, against players
/// the PvP defense rate by 20 (the rate PvP hits read).
/// </summary>
[TestFixture]
internal class PhoenixShotPvpBlockTest
{
    /// <summary>A new database: the PvP part lowers the PvP defense rate.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseLowersThePvpDefenseRateAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var effect = Effect(gameConfiguration);

        Assert.That(effect.PowerUpDefinitionsPvp.Select(p => (p.TargetAttribute, p.Boost!.ConstantValue.Value)), Is.EqualTo(new[] { (Stats.DefenseRatePvp, -20f) }));
        Assert.That(effect.PowerUpDefinitions.Select(p => (p.TargetAttribute, p.Boost!.ConstantValue.Value)), Is.EqualTo(new[] { (Stats.DefenseRatePvm, -50f) }));
    }

    /// <summary>The update moves the old PvP target.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateMovesTheOldTargetAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var pvp = Effect(gameConfiguration).PowerUpDefinitionsPvp.Single();
        pvp.TargetAttribute = gameConfiguration.Attributes.First(a => a == Stats.DefenseRatePvm);

        await new PhoenixShotPvpBlockUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(pvp.TargetAttribute, Is.EqualTo(Stats.DefenseRatePvp));
    }

    private static MagicEffectDefinition Effect(GameConfiguration gameConfiguration)
        => gameConfiguration.MagicEffects.First(e => e.Number == (short)MagicEffectNumber.DecreaseBlock);
}
