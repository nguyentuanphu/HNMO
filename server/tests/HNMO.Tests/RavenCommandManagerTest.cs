// <copyright file="RavenCommandManagerTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.Pet;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.Views.Pet;
using Moq;

/// <summary>
/// Tests for <see cref="RavenCommandManager"/>.
/// </summary>
[TestFixture]
public class RavenCommandManagerTest
{
    /// <summary>A dark raven at 0 durability stays idle instead of starting the requested attack behaviour.</summary>
    [Test]
    public async Task ZeroDurabilityForcesIdleBehaviourAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var pet = new HNMO.Persistence.BasicModel.Item
        {
            Definition = new HNMO.Persistence.BasicModel.ItemDefinition { Number = 5, Group = 13, Name = "Dark Raven" },
            Durability = 0,
        };

        using var manager = new RavenCommandManager(player, pet);
        await manager.SetBehaviourAsync(PetBehaviour.AttackRandom, null).ConfigureAwait(false);

        Mock.Get(player.ViewPlugIns.GetPlugIn<IPetBehaviourChangedViewPlugIn>()!)
            .Verify(v => v.PetBehaviourChangedAsync(pet, PetBehaviour.Idle, null), Times.Once);
    }
}
