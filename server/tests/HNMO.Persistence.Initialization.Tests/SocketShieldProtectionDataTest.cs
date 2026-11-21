// <copyright file="SocketShieldProtectionDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the water socket option "shield protection increase" (Season 6 server suite: 7, 10, 15, 20, 20 %).
/// </summary>
[TestFixture]
internal class SocketShieldProtectionDataTest
{
    /// <summary>A new Season 6 database has the Season 6 suite's values.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSeason6ValuesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        Assert.That(GetValues(gameConfiguration), Is.EqualTo(new[] { 0.07f, 0.10f, 0.15f, 0.20f, 0.20f }).Within(0.0001f));
    }

    /// <summary>The update changes the old 30 %; a value the host changed stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldValueAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var levelFive = GetOption(gameConfiguration).LevelDependentOptions.Single(l => l.Level == 5).PowerUpDefinition!.Boost!.ConstantValue;
        levelFive.Value = 0.30f;
        var update = new SocketShieldProtectionUpdatePlugIn();

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(levelFive.Value, Is.EqualTo(0.20f).Within(0.0001f));

        levelFive.Value = 0.25f;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(levelFive.Value, Is.EqualTo(0.25f).Within(0.0001f), "the host's own value stays");
    }

    private static IncreasableItemOption GetOption(GameConfiguration gameConfiguration)
        => gameConfiguration.ItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Single(o => o.OptionType == ItemOptionTypes.SocketOption
                         && o.LevelDependentOptions.Any(l => l.PowerUpDefinition?.TargetAttribute == Stats.ShieldItemDefenseIncrease));

    private static float[] GetValues(GameConfiguration gameConfiguration)
        => GetOption(gameConfiguration).LevelDependentOptions.OrderBy(o => o.Level).Select(o => o.PowerUpDefinition!.Boost!.ConstantValue.Value).ToArray();
}
