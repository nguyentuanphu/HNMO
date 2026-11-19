// <copyright file="SeasonDataTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Bridge;

/// <summary>
/// Which files the web game's bridge may take from the Season 9 pack (<c>client/web/bridge/SeasonData.cs</c>).
/// </summary>
[TestFixture]
public class SeasonDataTests
{
    /// <summary>A Season 6.3 world may take only the folders of maps that Season 9 alone has from the Season 9 pack.</summary>
    /// <param name="path">The path below <c>/data/</c>.</param>
    /// <param name="expected">Whether the Season 9 pack may answer.</param>
    [TestCase("World83/EncTerrain83.map", true)]
    [TestCase("/Object92/Object01.bmd", true)]
    [TestCase("world103\\TileGrass01.ozj", true)]
    [TestCase("World1/EncTerrain1.map", false)]
    [TestCase("Object3/Object102.bmd", false)]
    [TestCase("World830/x.map", false)]
    [TestCase("Item/Sword01.bmd", false)]
    [TestCase("Local/Item.bmd", false)]
    [TestCase("World83", false)]
    public void OnlyMapsSeasonNineAloneHasFallBackToItsPack(string path, bool expected)
    {
        Assert.That(SeasonData.IsSeasonNineOnlyMapFile(path), Is.EqualTo(expected));
    }

    /// <summary>A Season 9 object's texture is looked for in <c>Item/</c> as well; nothing else is.</summary>
    /// <param name="path">The path below <c>/data/s9/</c>.</param>
    /// <param name="expected">The second place to look, or <c>null</c>.</param>
    [TestCase("Object3/sword.jpg", "Item/sword.jpg")]
    [TestCase("/Object3/Shield01.OZJ", "Item/Shield01.OZJ")]
    [TestCase("object3\\axe.tga", "Item/axe.tga")]
    [TestCase("Object3/Object102.bmd", null)]
    [TestCase("World3/sword.jpg", null)]
    [TestCase("Object3/sub/sword.jpg", null)]
    [TestCase("Object3/..jpg", null)]
    [TestCase("Object3/", null)]
    [TestCase("Item/sword.jpg", null)]
    public void ObjectTexturesAlsoLookInTheItemFolder(string path, string? expected)
    {
        Assert.That(SeasonData.ItemFolderTextureFor(path), Is.EqualTo(expected));
    }

    /// <summary>The Season 9 pack answers only for the looks and sounds of the world.</summary>
    /// <param name="path">The path below <c>/data/s9/</c>.</param>
    /// <param name="expected">Whether the folder is the season's.</param>
    [TestCase("World1/EncTerrain1.map", true)]
    [TestCase("Monster/Monster01.bmd", true)]
    [TestCase("Local/Item.bmd", false)]
    [TestCase("Interface/x.ozj", false)]
    public void TheSeasonPackAnswersOnlyForTheWorld(string path, bool expected)
    {
        Assert.That(SeasonData.IsSeasonFolder(path), Is.EqualTo(expected));
    }
}
