// <copyright file="SeasonNineExtendedSharedItemsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Applies the remaining Season 9 server suite values on shared items that differ from Season 6 when running as Season 9:
/// Sphere (4) drop level (0 -&gt; 132) and Reset Fruits (13-54..58) max stack/durability (100/0 -&gt; 10).
/// Values customized by an operator are preserved.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineExtendedSharedItemsUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineExtendedSharedItemsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B8E3A91C-4D72-4F5A-9E18-3C6F2A7D9E02")]
public class SeasonNineExtendedSharedItemsUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    private const byte SphereFourGroup = 12;
    private const short SphereFourNumber = 73;
    private const byte OldSphereFourDropLevel = 0;
    private const byte SeasonNineSphereFourDropLevel = 132;

    private const byte FruitGroup = 13;
    private const short FirstResetFruitNumber = 54;
    private const short LastResetFruitNumber = 58;
    private const byte OldResetFruitDurability = 100;
    private const byte SeasonNineResetFruitDurability = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineExtendedSharedItemsUpdatePlugIn"/> class.
    /// </summary>
    public SeasonNineExtendedSharedItemsUpdatePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineExtendedSharedItemsUpdatePlugIn"/> class with a test season selector.
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    internal SeasonNineExtendedSharedItemsUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineExtendedSharedItemsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineExtendedSharedItemsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 8, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Items.FirstOrDefault(item => item.Group == SphereFourGroup && item.Number == SphereFourNumber) is { } sphereFour
            && sphereFour.DropLevel == OldSphereFourDropLevel)
        {
            sphereFour.DropLevel = SeasonNineSphereFourDropLevel;
        }

        for (var number = FirstResetFruitNumber; number <= LastResetFruitNumber; number++)
        {
            if (gameConfiguration.Items.FirstOrDefault(item => item.Group == FruitGroup && item.Number == number) is { } fruit
                && (fruit.Durability == OldResetFruitDurability || fruit.Durability == 0))
            {
                fruit.Durability = SeasonNineResetFruitDurability;
            }
        }

        return ValueTask.CompletedTask;
    }
}
