// <copyright file="IncreaseStatsActionTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Character;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Regression tests for <see cref="IncreaseStatsAction"/>.
/// </summary>
[TestFixture]
public class IncreaseStatsActionTest
{
    /// <summary>
    /// When the player's current attribute value already exceeds <see cref="AttributeDefinition.MaximumValue"/>,
    /// <see cref="IncreaseStatsAction.IncreaseStatsAsync"/> must not wrap <c>(ushort)(maximumValue - current)</c>
    /// into ~65,535 and must spend zero level-up points.
    /// </summary>
    [Test]
    public async ValueTask IncreaseStatsDoesNotUnderflowWhenCurrentExceedsMaximumValueAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var character = player.SelectedCharacter!;

        var cappedStrengthDef = new AttributeDefinition(Stats.BaseStrength.Id, "Strength", "Base Strength")
        {
            MaximumValue = 100f,
        };
        var unCappedStatAttrDef = new AttributeDefinition(Stats.BaseStrength.Id, "Strength", "Base Strength")
        {
            MaximumValue = null,
        };

        var strDef = character.CharacterClass!.StatAttributes.First(s => s.Attribute == Stats.BaseStrength);
        strDef.Attribute = cappedStrengthDef;

        character.Attributes.Clear();
        character.Attributes.Add(new BasicModel.StatAttribute(unCappedStatAttrDef, 105f));
        character.LevelUpPoints = 200;

        // EnterWorld initializes AttributeSystem using character.Attributes (which hold the uncapped definition).
        var rebuiltPlayer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await PlayerTestHelper.EnterWorldAsync(rebuiltPlayer, character).ConfigureAwait(false);

        var action = new IncreaseStatsAction();
        await action.IncreaseStatsAsync(rebuiltPlayer, cappedStrengthDef, 5).ConfigureAwait(false);

        Assert.That(character.LevelUpPoints, Is.EqualTo(200));
        Assert.That(rebuiltPlayer.Attributes![cappedStrengthDef], Is.EqualTo(105f));
    }
}
