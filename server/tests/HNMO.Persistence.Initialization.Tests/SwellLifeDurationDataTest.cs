// <copyright file="SwellLifeDurationDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies Swell Life and Swell Life Proficiency durations match the Season 6 server suite
/// (<c>60 + Energy / 10</c> s, uncapped).
/// </summary>
[TestFixture]
internal class SwellLifeDurationDataTest
{
    /// <summary>A fresh Season 6 database configures Swell Life and its proficiency variant with <c>60 + Energy / 10</c> s and no cap.</summary>
    [Test]
    public async Task NewDatabaseHasUncappedTenDivisorDurationAsync()
    {
        var (context, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        foreach (var number in new[] { MagicEffectNumber.GreaterFortitude, MagicEffectNumber.GreaterFortitudeProficiency })
        {
            var duration = GetDuration(config, number);
            Assert.Multiple(() =>
            {
                Assert.That(duration.ConstantValue.Value, Is.EqualTo(60f));
                Assert.That(duration.MaximumValue, Is.Null);
                Assert.That(duration.RelatedValues.Single(r => r.InputAttribute == Stats.TotalEnergy).InputOperand, Is.EqualTo(1f / 10f).Within(1e-6f));
            });
        }
    }

    /// <summary>The mandatory update plug-in migrates old 180-s cap / 180-divisor durations while keeping host edits intact.</summary>
    [Test]
    public async Task UpdateMigratesOldValuesAndPreservesHostChangesAsync()
    {
        var (context, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        var baseDuration = GetDuration(config, MagicEffectNumber.GreaterFortitude);
        var profDuration = GetDuration(config, MagicEffectNumber.GreaterFortitudeProficiency);
        foreach (var duration in new[] { baseDuration, profDuration })
        {
            duration.MaximumValue = 180f;
            duration.RelatedValues.Single(r => r.InputAttribute == Stats.TotalEnergy).InputOperand = 1f / 180f;
        }

        var update = new SwellLifeDurationUpdatePlugIn();
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(update.IsMandatory, Is.True);
            Assert.That(baseDuration.MaximumValue, Is.Null);
            Assert.That(profDuration.MaximumValue, Is.Null);
            Assert.That(baseDuration.RelatedValues.Single(r => r.InputAttribute == Stats.TotalEnergy).InputOperand, Is.EqualTo(1f / 10f).Within(1e-6f));
            Assert.That(profDuration.RelatedValues.Single(r => r.InputAttribute == Stats.TotalEnergy).InputOperand, Is.EqualTo(1f / 10f).Within(1e-6f));
        });

        // Host customizes base cap to 300s and operand to 1/20; update leaves both untouched.
        baseDuration.MaximumValue = 300f;
        baseDuration.RelatedValues.Single(r => r.InputAttribute == Stats.TotalEnergy).InputOperand = 1f / 20f;
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(baseDuration.MaximumValue, Is.EqualTo(300f));
            Assert.That(baseDuration.RelatedValues.Single(r => r.InputAttribute == Stats.TotalEnergy).InputOperand, Is.EqualTo(1f / 20f).Within(1e-6f));
        });
    }

    private static PowerUpDefinitionValue GetDuration(GameConfiguration config, MagicEffectNumber number)
    {
        return config.MagicEffects.Single(e => e.Number == (short)number).Duration!;
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var init = new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory());
        await init.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, config);
    }
}
