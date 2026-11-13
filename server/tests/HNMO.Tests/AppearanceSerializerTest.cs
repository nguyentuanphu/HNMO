// <copyright file="AppearanceSerializerTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameServer.RemoteView;

/// <summary>
/// Tests the <see cref="AppearanceSerializer"/>.
/// </summary>
[TestFixture]
public class AppearanceSerializerTest
{
    /// <summary>
    /// Tests if a new (naked) dark knight with small axe would be serialized correctly.
    /// </summary>
    [Test]
    public void NewDarkKnightWithSmallAxe()
    {
        var serializer = new AppearanceSerializer();
        var appearanceData = new Mock<IAppearanceData>();
        appearanceData.Setup(a => a.CharacterClass).Returns(new CharacterClass { Number = 0x20 >> 3 }); // Dark Knight;
        appearanceData.Setup(a => a.EquippedItems).Returns(this.GetSmallAxeEquipped());
        var data = new byte[serializer.NeededSpace];
        serializer.WriteAppearanceData(data, appearanceData.Object, false);
        var expected = new byte[] { 0x20, 0x00, 0xFF, 0xFF, 0xFF, 0xF3, 0x00, 0x00, 0x00, 0xF8, 0x00, 0x00, 0x20, 0xFF, 0xFF, 0xFF, 0x00, 0x00 };
        Assert.That(data, Is.EquivalentTo(expected));
    }

    private IEnumerable<ItemAppearance> GetSmallAxeEquipped()
    {
        yield return new ItemAppearance { Definition = new ItemDefinition { Group = 1 } };
    }
}