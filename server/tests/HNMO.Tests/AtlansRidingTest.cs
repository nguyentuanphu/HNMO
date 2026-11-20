// <copyright file="AtlansRidingTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Classic MU: Uniria and Dinorant can't enter Atlans (map 7).
/// </summary>
[TestFixture]
public class AtlansRidingTest
{
    /// <summary>With Uniria or Dinorant equipped Atlans is refused; another pet or another map is fine.</summary>
    /// <param name="petNumber">The number of the pet in group 13, or -1 for none.</param>
    /// <param name="mapNumber">The map.</param>
    /// <param name="refused">Whether the map is refused.</param>
    /// <returns>The task.</returns>
    [TestCase(2, 7, true)]
    [TestCase(3, 7, true)]
    [TestCase(-1, 7, false)]
    [TestCase(0, 7, false)]
    [TestCase(3, 2, false)]
    public async ValueTask UniriaAndDinorantCantEnterAtlansAsync(int petNumber, int mapNumber, bool refused)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        if (petNumber >= 0)
        {
            var pet = new BasicModel.Item
            {
                Definition = new BasicModel.ItemDefinition { Name = "Pet", Group = 13, Number = (short)petNumber, Width = 1, Height = 1, Durability = 255 },
                Durability = 255,
            };
            await player.Inventory!.AddItemAsync(InventoryConstants.PetSlot, pet).ConfigureAwait(false);
        }

        var map = new BasicModel.GameMapDefinition { Number = (short)mapNumber, Name = "Map", IsActive = true };

        Assert.That(map.TryGetRequirementError(player, out var message), Is.EqualTo(refused), message);
    }
}
