// <copyright file="MoneyDropDurationTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.GameLogic;

/// <summary>
/// Tests that dropped zen has its own time on the ground (15 s; items 20 s, Season 9 server suites).
/// </summary>
[TestFixture]
public class MoneyDropDurationTest
{
    /// <summary>
    /// The maps take the zen duration of the game configuration.
    /// </summary>
    [Test]
    public void MapsTakeTheZenDuration()
    {
        var configuration = PlayerTestHelper.CreateGameConfiguration();
        configuration.ItemDropDuration = TimeSpan.FromSeconds(20);
        configuration.MoneyDropDuration = TimeSpan.FromSeconds(15);
        var initializer = new MapInitializer(configuration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);

        var map = initializer.CreateGameMap(configuration.Maps.First());

        Assert.Multiple(() =>
        {
            Assert.That(map.ItemDropDuration, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(map.MoneyDropDuration, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
    }

    /// <summary>
    /// Without a zen duration in the configuration, zen stays as long as items.
    /// </summary>
    [Test]
    public void WithoutZenDurationZenStaysAsLongAsItems()
    {
        var configuration = PlayerTestHelper.CreateGameConfiguration();
        configuration.ItemDropDuration = TimeSpan.FromSeconds(20);
        var initializer = new MapInitializer(configuration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);

        var map = initializer.CreateGameMap(configuration.Maps.First());

        Assert.That(map.MoneyDropDuration, Is.EqualTo(TimeSpan.FromSeconds(20)));
    }
}
