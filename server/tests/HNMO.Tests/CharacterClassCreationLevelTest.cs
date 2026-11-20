// <copyright file="CharacterClassCreationLevelTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Character;
using HNMO.Persistence.InMemory;

/// <summary>
/// Magic Gladiator (220), Dark Lord (250) and Rage Fighter (150) need a character of the account at that level
/// before they can be created; the creation didn't check it.
/// </summary>
[TestFixture]
public class CharacterClassCreationLevelTest
{
    /// <summary>A class with a level requirement is available once unlocked or once a character has the level.</summary>
    [Test]
    public void ClassWithLevelRequirementNeedsUnlockOrLevel()
    {
        var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var levelDefinition = context.CreateNew<AttributeDefinition>();
        levelDefinition.Id = Stats.Level.Id;
        var knight = context.CreateNew<CharacterClass>();
        knight.Number = 16;
        var gladiator = context.CreateNew<CharacterClass>();
        gladiator.Number = 48;
        gladiator.LevelRequirementByCreation = 220;

        var account = context.CreateNew<Account>();
        var character = context.CreateNew<Character>();
        var level = new HNMO.Persistence.BasicModel.StatAttribute(levelDefinition, 150);
        character.Attributes.Add(level);
        account.Characters.Add(character);

        Assert.Multiple(() =>
        {
            Assert.That(CreateCharacterAction.IsClassAvailable(account, knight), Is.True, "no requirement");
            Assert.That(CreateCharacterAction.IsClassAvailable(account, gladiator), Is.False, "level 150 < 220");
        });

        level.Value = 220;
        Assert.That(CreateCharacterAction.IsClassAvailable(account, gladiator), Is.True, "a character of level 220");

        level.Value = 1;
        account.UnlockedCharacterClasses.Add(gladiator);
        Assert.That(CreateCharacterAction.IsClassAvailable(account, gladiator), Is.True, "unlocked");
    }
}
