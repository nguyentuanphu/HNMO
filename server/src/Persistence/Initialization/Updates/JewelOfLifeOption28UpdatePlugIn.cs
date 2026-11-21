// <copyright file="JewelOfLifeOption28UpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.PlugIns;

/// <summary>
/// This update lets the Jewel of Life raise the additional option up to +28 (option level 7), like the Season 6/9
/// server suites (owner's choice, 2026-11-20). The additional options which go up to level 4 today (the weapons',
/// armour's, shields' and wings' +4/+5/+1 % per level) get the levels 5 to 7 with the same step per level. Options
/// with a lower maximum (the rings' and pendants' recovery up to 3 %) stay as they are, and so do the drops
/// (<see cref="GameConfiguration.MaximumItemOptionLevelDrop"/>).
/// Saved rate tuning settings with the earlier default cap of +16 (<see cref="RateTuningConfiguration.MaxLifeOptionLevel"/>
/// of 4) get the new default of +28.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("3F7C1A92-5D2E-4B86-9A41-C0E8D6B27F15")]
public class JewelOfLifeOption28UpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Jewel of Life up to +28";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Adds the option levels 5 to 7 (+20, +24, +28) to the additional options which end at level 4, and raises the rate tuning's Jewel of Life cap from +16 to +28.";

    private const int PreviousMaximumLevel = 4;

    private const int NewMaximumLevel = 7;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var option in gameConfiguration.ItemOptions
                     .Concat(gameConfiguration.Items.SelectMany(item => item.PossibleItemOptions))
                     .Distinct()
                     .SelectMany(definition => definition.PossibleOptions)
                     .Where(option => option.OptionType == ItemOptionTypes.Option && option.LevelType == LevelType.OptionLevel)
                     .Distinct())
        {
            AddHigherLevels(context, option);
        }

        RaiseRateTuningCap(gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static void AddHigherLevels(IContext context, IncreasableItemOption option)
    {
        var levels = option.LevelDependentOptions;
        if (levels.Count == 0 || levels.Max(l => l.Level) != PreviousMaximumLevel)
        {
            // A fixed option, one with a lower maximum (jewelery) or one which has the higher levels already.
            return;
        }

        var third = levels.FirstOrDefault(l => l.Level == PreviousMaximumLevel - 1);
        var fourth = levels.First(l => l.Level == PreviousMaximumLevel);
        if (third?.PowerUpDefinition?.Boost is not { } thirdBoost
            || fourth.PowerUpDefinition?.Boost is not { } fourthBoost
            || fourth.PowerUpDefinition.TargetAttribute is not { } targetAttribute
            || thirdBoost.RelatedValues.Count > 0
            || fourthBoost.RelatedValues.Count > 0)
        {
            // Not a plain value per level, so there's no clear step to continue with.
            return;
        }

        var step = (double)fourthBoost.ConstantValue.Value - thirdBoost.ConstantValue.Value;
        for (var level = PreviousMaximumLevel + 1; level <= NewMaximumLevel; level++)
        {
            var optionOfLevel = context.CreateNew<ItemOptionOfLevel>();
            optionOfLevel.Level = level;
            optionOfLevel.RequiredItemLevel = fourth.RequiredItemLevel;
            optionOfLevel.PowerUpDefinition = context.CreateNew<PowerUpDefinition>();
            optionOfLevel.PowerUpDefinition.TargetAttribute = targetAttribute;
            optionOfLevel.PowerUpDefinition.Boost = context.CreateNew<PowerUpDefinitionValue>();
            optionOfLevel.PowerUpDefinition.Boost.ConstantValue.Value = (float)Math.Round(fourthBoost.ConstantValue.Value + (step * (level - PreviousMaximumLevel)), 4);
            optionOfLevel.PowerUpDefinition.Boost.ConstantValue.AggregateType = fourthBoost.ConstantValue.AggregateType;
            optionOfLevel.PowerUpDefinition.Boost.MaximumValue = fourthBoost.MaximumValue;
            levels.Add(optionOfLevel);
        }
    }

    private static void RaiseRateTuningCap(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(RateTuningPlugIn).GUID) is not { } plugInConfiguration
            || string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration))
        {
            // No saved settings: the plug-in's default (+28) applies.
            return;
        }

        var saved = plugInConfiguration.CustomConfiguration;
        RateTuningConfiguration? configuration;
        try
        {
            // Settings saved by the admin panel's editor carry reference markers ("$id", lists as "$values").
            plugInConfiguration.CustomConfiguration = WithoutReferences(JsonNode.Parse(saved))?.ToJsonString() ?? saved;
            configuration = plugInConfiguration.GetConfiguration<RateTuningConfiguration>(null);
        }
        catch (JsonException)
        {
            // Settings this update can't read stay as they are; the cap can be set on the admin panel.
            configuration = null;
        }

        if (configuration is not { MaxLifeOptionLevel: PreviousMaximumLevel })
        {
            // The host chose another cap, or the settings can't be read: they stay exactly as saved.
            plugInConfiguration.CustomConfiguration = saved;
            return;
        }

        configuration.MaxLifeOptionLevel = NewMaximumLevel;
        plugInConfiguration.SetConfiguration(configuration, null);
    }

    private static JsonNode? WithoutReferences(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj when obj["$values"] is JsonArray values:
                return WithoutReferences(values);
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    if (key is not ("$id" or "$ref"))
                    {
                        result[key] = WithoutReferences(value);
                    }
                }

                return result;
            case JsonArray array:
                var list = new JsonArray();
                foreach (var item in array)
                {
                    list.Add(WithoutReferences(item));
                }

                return list;
            default:
                return node?.DeepClone();
        }
    }
}
