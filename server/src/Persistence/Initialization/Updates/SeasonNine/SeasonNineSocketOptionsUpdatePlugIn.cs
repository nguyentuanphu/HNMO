// <copyright file="SeasonNineSocketOptionsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Updates socket seed sphere option values that differ in the Season 9 server suite when running in Season 9:
/// Water "shield protection increase" level 5 (20 % -&gt; 30 %), Wind "maximum AG increase" (25..50 -&gt; 50..130),
/// and Wind "AG value increase" (3..15 -&gt; 5..13). Customized operator values are preserved.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineSocketOptionsUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineSocketOptionsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C93D6E42-7AF4-4B8D-93F5-9EAE406F3003")]
public class SeasonNineSocketOptionsUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    private static readonly SocketLevelChange[] Changes =
    [
        new(SocketSubOptionType.Water, 2, Stats.ShieldItemDefenseIncrease, 5, 0.20f, 0.30f),
        new(SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 1, 25f, 50f),
        new(SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 2, 30f, 70f),
        new(SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 3, 35f, 90f),
        new(SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 4, 40f, 110f),
        new(SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 5, 50f, 130f),
        new(SocketSubOptionType.Wind, 5, Stats.AbilityRecoveryAbsolute, 1, 3f, 5f),
        new(SocketSubOptionType.Wind, 5, Stats.AbilityRecoveryAbsolute, 2, 5f, 7f),
        new(SocketSubOptionType.Wind, 5, Stats.AbilityRecoveryAbsolute, 3, 7f, 9f),
        new(SocketSubOptionType.Wind, 5, Stats.AbilityRecoveryAbsolute, 4, 10f, 11f),
        new(SocketSubOptionType.Wind, 5, Stats.AbilityRecoveryAbsolute, 5, 15f, 13f),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineSocketOptionsUpdatePlugIn"/> class.
    /// </summary>
    public SeasonNineSocketOptionsUpdatePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineSocketOptionsUpdatePlugIn"/> class with a test season selector.
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    internal SeasonNineSocketOptionsUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineSocketOptionsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineSocketOptionsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 23, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(SocketShieldProtectionUpdatePlugIn).GUID];

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var socketOptions = gameConfiguration.ItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Where(o => o.OptionType == ItemOptionTypes.SocketOption)
            .ToList();

        foreach (var change in Changes)
        {
            var levelValues = socketOptions
                .Where(o => o.SubOptionType == (int)change.SubType && o.Number == change.OptionNumber)
                .SelectMany(o => o.LevelDependentOptions)
                .Where(l => l.Level == change.Level && l.PowerUpDefinition?.TargetAttribute == change.TargetAttribute);

            foreach (var level in levelValues)
            {
                if (level.PowerUpDefinition?.Boost?.ConstantValue is { } constant
                    && Math.Abs(constant.Value - change.OldValue) < 0.0001f)
                {
                    constant.Value = change.NewValue;
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Represents a single Season 9 socket level value change.</summary>
    private readonly record struct SocketLevelChange(
        SocketSubOptionType SubType,
        int OptionNumber,
        AttributeDefinition TargetAttribute,
        int Level,
        float OldValue,
        float NewValue);
}
