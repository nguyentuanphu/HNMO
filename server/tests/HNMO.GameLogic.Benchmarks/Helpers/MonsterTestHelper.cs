// <copyright file="MonsterTestHelper.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Benchmarks.Helpers;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;

/// <summary>
/// Helper for creating test monsters.
/// </summary>
public static class MonsterTestHelper
{
    /// <summary>
    /// Creates a mock monster definition.
    /// </summary>
    /// <param name="numberOfDrops">The maximum number of item drops.</param>
    /// <param name="level">The monster level.</param>
    /// <returns>A mock monster definition.</returns>
    public static MonsterDefinition Create(int numberOfDrops, byte level)
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = numberOfDrops;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });

        monster.Object.DropItemGroups.AddBasicDropItemGroups();

        return monster.Object;
    }
}