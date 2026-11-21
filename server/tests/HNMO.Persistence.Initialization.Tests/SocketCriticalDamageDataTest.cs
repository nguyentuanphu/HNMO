// <copyright file="SocketCriticalDamageDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the lightning socket option "critical damage increase" (Season 6/9 server suites, socket options:
/// +30, +32, +35, +40, +50 by seed sphere level).
/// </summary>
[TestFixture]
internal class SocketCriticalDamageDataTest
{
    /// <summary>A new Season 6 database has the suites' values.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuitesValuesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        Assert.That(GetValues(gameConfiguration), Is.EqualTo(SocketCriticalDamageUpdatePlugIn.NewValues));
    }

    /// <summary>The update changes the old values; a value the host changed stays; twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldValuesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var levels = GetOption(gameConfiguration).LevelDependentOptions.OrderBy(o => o.Level).ToList();
        float[] old = [15, 20, 25, 30, 40];
        for (var i = 0; i < levels.Count; i++)
        {
            levels[i].PowerUpDefinition!.Boost!.ConstantValue.Value = old[i];
        }

        levels[4].PowerUpDefinition!.Boost!.ConstantValue.Value = 45;
        var update = new SocketCriticalDamageUpdatePlugIn();

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(GetValues(gameConfiguration), Is.EqualTo(new float[] { 30, 32, 35, 40, 45 }), "level 5 keeps the host's 45");
    }

    private static IncreasableItemOption GetOption(GameConfiguration gameConfiguration)
        => gameConfiguration.ItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Single(o => o.OptionType == ItemOptionTypes.SocketOption
                         && o.Number == SocketCriticalDamageUpdatePlugIn.OptionNumber
                         && o.LevelDependentOptions.Any(l => l.PowerUpDefinition?.TargetAttribute == Stats.CriticalDamageBonus));

    private static float[] GetValues(GameConfiguration gameConfiguration)
        => GetOption(gameConfiguration).LevelDependentOptions.OrderBy(o => o.Level).Select(o => o.PowerUpDefinition!.Boost!.ConstantValue.Value).ToArray();
}
