// <copyright file="TestGameContextPeriodicTaskTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.InvasionEvents;

/// <summary>
/// The game contexts of the test helpers run the periodic task plugins on a timer, like a real
/// server. When an invasion's schedule came up during a test run, its preparation read the monster
/// list of the mocked game configuration, which was <c>null</c> - the exception crashed the test host
/// after all tests had passed. The test configurations have to provide that list.
/// </summary>
[TestFixture]
public class TestGameContextPeriodicTaskTest
{
    /// <summary>
    /// Preparing an invasion on the game context of <see cref="PlayerTestHelper"/> doesn't throw.
    /// </summary>
    [Test]
    public async ValueTask InvasionPreparesOnThePlayerTestHelperContextAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var gameContext = (GameContext)player.GameContext;
        var invasion = new GoldenInvasionPlugIn();
        invasion.ForceStart();

        Assert.DoesNotThrowAsync(async () => await invasion.ExecuteTaskAsync(gameContext).ConfigureAwait(false));
    }
}
