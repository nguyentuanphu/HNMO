// <copyright file="IncreaseStatsAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Character;

using HNMO.AttributeSystem;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Character;
using HNMO.Interfaces;

/// <summary>
/// Action to increase stat attributes.
/// </summary>
public class IncreaseStatsAction
{
    /// <summary>
    /// Increases the specified stat attribute by one point, if enough points are available.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="targetAttribute">The stat attribute definition.</param>
    /// <param name="amount">The amount of points.</param>
    public async ValueTask IncreaseStatsAsync(Player player, AttributeDefinition targetAttribute, ushort amount = 1)
    {
        if (player.SelectedCharacter is not { } selectedCharacter)
        {
            throw new InvalidOperationException("No character selected");
        }

        if (amount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The amount must be greater than 0.");
        }

        if (!selectedCharacter.CanIncreaseStats(amount))
        {
            await player.ShowLocalizedBlueMessageAsync(PlayerMessage.NotEnoughLevelUpPointsAvailable).ConfigureAwait(false);
            return;
        }

        var attributeDef = selectedCharacter.CharacterClass?.GetStatAttribute(targetAttribute);
        if (attributeDef is { IncreasableByPlayer: true })
        {
            if (attributeDef.Attribute?.MaximumValue is { } maximumValue
                && player.Attributes![attributeDef.Attribute] is { } current
                && current + amount > maximumValue)
            {
                amount = (ushort)(maximumValue - current);
                if (amount == 0)
                {
                    await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MaximumAttributeValueReachedFormat), attributeDef.Attribute?.MaximumValue, new LocalizedString(attributeDef.Attribute?.Designation).GetTranslation(player.Culture)).ConfigureAwait(false);
                    return;
                }
            }

            player.Attributes![attributeDef.Attribute] += amount;
            selectedCharacter.LevelUpPoints -= Math.Min(selectedCharacter.LevelUpPoints, amount);

            await player.InvokeViewPlugInAsync<IStatIncreaseResultPlugIn>(p => p.StatIncreaseResultAsync(targetAttribute, amount)).ConfigureAwait(false);
        }
        else
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.AttributeNotAvailable)).ConfigureAwait(false);
        }
    }
}