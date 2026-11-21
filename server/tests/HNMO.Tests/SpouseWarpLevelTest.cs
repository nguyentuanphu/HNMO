// <copyright file="SpouseWarpLevelTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Marriage;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The spouse warp needs the level of a normal warp to the spouse's map (owner, 21 Nov 2026).
/// </summary>
[TestFixture]
public class SpouseWarpLevelTest
{
    /// <summary>The lowest warp level to the map applies; a map without warp needs none.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask LowestWarpLevelOfTheMapAppliesAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var atlans = new BasicModel.GameMapDefinition { Number = 7, Name = "Atlans" };
        var lostTower = new BasicModel.GameMapDefinition { Number = 4, Name = "Lost Tower" };
        var noWarpMap = new BasicModel.GameMapDefinition { Number = 99, Name = "Hidden" };
        var warps = new List<WarpInfo>
        {
            new BasicModel.WarpInfo { Gate = new BasicModel.ExitGate { Map = atlans }, LevelRequirement = 80 },
            new BasicModel.WarpInfo { Gate = new BasicModel.ExitGate { Map = atlans }, LevelRequirement = 70 },
            new BasicModel.WarpInfo { Gate = new BasicModel.ExitGate { Map = lostTower }, LevelRequirement = 50 },
        };
        Mock.Get(player.GameContext.Configuration).Setup(c => c.WarpList).Returns(warps);

        Assert.Multiple(() =>
        {
            Assert.That(MarriagePlugIn.GetSpouseWarpLevel(player, atlans), Is.EqualTo(70));
            Assert.That(MarriagePlugIn.GetSpouseWarpLevel(player, lostTower), Is.EqualTo(50));
            Assert.That(MarriagePlugIn.GetSpouseWarpLevel(player, noWarpMap), Is.Null);
        });
    }
}
