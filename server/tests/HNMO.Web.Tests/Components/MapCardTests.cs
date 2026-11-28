// <copyright file="MapCardTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Components;

using Bunit;
using HNMO.Web.Map;
using HNMO.Web.Map.Components;
using Moq;

/// <summary>Tests for the <see cref="MapCard"/> component.</summary>
[TestFixture]
public class MapCardTests
{
    /// <summary>The card shows the translated caption of the player count, not the name of the resource.</summary>
    [Test]
    public void PlayerCountCaptionIsTranslatedText()
    {
        using var context = new BunitContext();
        var info = new Mock<IGameMapInfo>();
        info.SetupGet(i => i.Id).Returns(Guid.NewGuid());
        info.SetupGet(i => i.MapName).Returns("Lorencia");
        info.SetupGet(i => i.MapNumber).Returns(0);
        info.SetupGet(i => i.PlayerCount).Returns(7);
        info.SetupGet(i => i.TerrainData).Returns((byte[]?)null);

        var cut = context.Render<MapCard>(parameters => parameters.Add(c => c.MapInfo, info.Object));

        Assert.That(cut.Markup, Does.Not.Contain("Properties.Resources"));
        Assert.That(cut.Find("p.card-text").TextContent, Does.EndWith(": 7"));
    }
}
