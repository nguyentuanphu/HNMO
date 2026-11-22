// <copyright file="SeasonNineSharedItemsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Applies the Season 9 server suite values on shared items that differ from Season 6 when running as Season 9:
/// Cape of Fighter drop level (180 -&gt; 150), Cape of Lord required level (180 -&gt; 150), and native 255 stacks
/// on Apple and basic healing/mana potions (3 -&gt; 255).
/// Values customized by an operator (not equal to HNMO's old Season 6 values) are preserved.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineSharedItemsUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineSharedItemsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A91B4C20-58E2-4F6B-91D3-7C8A2E4F1001")]
public class SeasonNineSharedItemsUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    private const byte OldCapeOfFighterDropLevel = 180;
    private const byte SeasonNineCapeOfFighterDropLevel = 150;
    private const int OldCapeOfLordRequiredLevel = 180;
    private const int SeasonNineCapeOfLordRequiredLevel = 150;
    private const byte OldPotionDurability = 3;
    private const byte SeasonNinePotionDurability = 255;

    private static readonly short[] BasicPotionNumbers = [0, 1, 2, 3, 4, 5, 6];

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineSharedItemsUpdatePlugIn"/> class.
    /// </summary>
    public SeasonNineSharedItemsUpdatePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineSharedItemsUpdatePlugIn"/> class with a test season selector.
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    internal SeasonNineSharedItemsUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineSharedItemsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineSharedItemsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 23, 10, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Items.FirstOrDefault(item => item.Group == 12 && item.Number == 49) is { } capeOfFighter
            && capeOfFighter.DropLevel == OldCapeOfFighterDropLevel)
        {
            capeOfFighter.DropLevel = SeasonNineCapeOfFighterDropLevel;
        }

        if (gameConfiguration.Items.FirstOrDefault(item => item.Group == 13 && item.Number == 30) is { } capeOfLord
            && capeOfLord.Requirements.FirstOrDefault(r => r.Attribute == Stats.Level) is { } levelReq
            && levelReq.MinimumValue == OldCapeOfLordRequiredLevel)
        {
            levelReq.MinimumValue = SeasonNineCapeOfLordRequiredLevel;
        }

        foreach (var number in BasicPotionNumbers)
        {
            if (gameConfiguration.Items.FirstOrDefault(item => item.Group == 14 && item.Number == number) is { } potion
                && potion.Durability == OldPotionDurability)
            {
                potion.Durability = SeasonNinePotionDurability;
            }
        }

        return ValueTask.CompletedTask;
    }
}
