// <copyright file="SecondWingOptionsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// This update adds the 2nd wings' excellent options "maximum AG +50" (option 4, bit 3) and "attack speed +5"
/// (option 5, bit 4), as in the excellent options of the Season 6/9 server suites (owner's choice, 21 Nov 2026).
/// The clients show both for the 2nd wings already. The Cape of Fighter, which shared the 2nd wings' options, gets
/// its own option definition with the three options it had, because a cape has no AG or speed option.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A0C6F3E5-2B7D-48E1-9F04-73D1B5A8C26E")]
public class SecondWingOptionsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "2nd wings: maximum AG and attack speed options (Season 6/9 server suites)";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The 2nd wings can get the excellent options maximum AG +50 and attack speed +5; the Cape of Fighter keeps its three options.";

    /// <summary>The number of the option definition of the Cape of Fighter.</summary>
    internal const short CapeOfFighterOptionsNumber = 0x68;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 50, 0, DateTimeKind.Utc);

    /// <summary>Gives the Cape of Fighter its own options and adds the AG and speed options to the 2nd wings.</summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var secondWings = gameConfiguration.ItemOptions.FirstOrDefault(o => o.GetId() == GuidHelper.CreateGuid<ItemOptionDefinition>(ItemOptionDefinitionNumbers.Wing2nd));
        if (secondWings is null)
        {
            return;
        }

        if (gameConfiguration.Items.FirstOrDefault(i => i.Group == 12 && i.Number == 49) is { } capeOfFighter
            && capeOfFighter.PossibleItemOptions.Contains(secondWings))
        {
            var capeOptions = context.CreateNew<ItemOptionDefinition>();
            capeOptions.SetGuid(CapeOfFighterOptionsNumber);
            capeOptions.Name = LocalizedString.FromResource(() => ItemOptionNames.CapeOfFighterOptions); // texts in every language
            capeOptions.AddChance = secondWings.AddChance;
            capeOptions.AddsRandomly = secondWings.AddsRandomly;
            capeOptions.MaximumOptionsPerItem = secondWings.MaximumOptionsPerItem;
            foreach (var option in secondWings.PossibleOptions.Where(o => o.Number <= 3).OrderBy(o => o.Number))
            {
                capeOptions.PossibleOptions.Add(Copy(context, option, CapeOfFighterOptionsNumber));
            }

            gameConfiguration.ItemOptions.Add(capeOptions);
            capeOfFighter.PossibleItemOptions.Remove(secondWings);
            capeOfFighter.PossibleItemOptions.Add(capeOptions);
        }

        AddIfMissing(4, Stats.MaximumAbility, 50);
        AddIfMissing(5, Stats.AttackSpeedAny, 5);

        void AddIfMissing(byte number, AttributeDefinition attribute, float value)
        {
            if (secondWings.PossibleOptions.Any(o => o.Number == number))
            {
                return;
            }

            var option = context.CreateNew<IncreasableItemOption>();
            option.SetGuid(ItemOptionDefinitionNumbers.Wing2nd, attribute.Id.ExtractFirstTwoBytes(), number);
            option.OptionType = gameConfiguration.ItemOptionTypes.First(t => t == ItemOptionTypes.Wing);
            option.Number = number;
            option.PowerUpDefinition = CreatePowerUp(context, attribute.GetPersistent(gameConfiguration), value, AggregateType.AddRaw);
            secondWings.PossibleOptions.Add(option);
        }
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static IncreasableItemOption Copy(IContext context, IncreasableItemOption source, short parentNumber)
    {
        var option = context.CreateNew<IncreasableItemOption>();
        var attribute = source.PowerUpDefinition!.TargetAttribute!;
        option.SetGuid(parentNumber, attribute.Id.ExtractFirstTwoBytes(), (byte)source.Number);
        option.OptionType = source.OptionType;
        option.Number = source.Number;
        option.LevelType = source.LevelType;
        option.PowerUpDefinition = CreatePowerUp(context, attribute, source.PowerUpDefinition.Boost!.ConstantValue.Value, source.PowerUpDefinition.Boost.ConstantValue.AggregateType);
        foreach (var level in source.LevelDependentOptions)
        {
            var copy = context.CreateNew<ItemOptionOfLevel>();
            copy.Level = level.Level;
            copy.RequiredItemLevel = level.RequiredItemLevel;
            copy.PowerUpDefinition = CreatePowerUp(context, attribute, level.PowerUpDefinition!.Boost!.ConstantValue.Value, level.PowerUpDefinition.Boost.ConstantValue.AggregateType);
            option.LevelDependentOptions.Add(copy);
        }

        return option;
    }

    private static PowerUpDefinition CreatePowerUp(IContext context, AttributeDefinition attribute, float value, AggregateType aggregateType)
    {
        var powerUp = context.CreateNew<PowerUpDefinition>();
        powerUp.TargetAttribute = attribute;
        powerUp.Boost = context.CreateNew<PowerUpDefinitionValue>();
        powerUp.Boost.ConstantValue.Value = value;
        powerUp.Boost.ConstantValue.AggregateType = aggregateType;
        return powerUp;
    }
}
