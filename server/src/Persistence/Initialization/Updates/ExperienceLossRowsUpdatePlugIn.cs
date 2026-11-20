// <copyright file="ExperienceLossRowsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// This update sets the experience loss on death to the rows of the Season 6/9 server suites (owner's choice,
/// 2026-11-21): normal players 3 % (level 11-150), 2 % (151-220) and 1 % (221 and master), Warning 5 %,
/// PK level 2 10 % and murderer 20 %. Only a save which still holds the earlier default rows is changed;
/// rows the host chose stay.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A84E1D3B-56C2-4F7A-9B0E-2C7D51E6F948")]
public class ExperienceLossRowsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Experience loss on death like the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Sets the experience loss on death to 3/2/1 % by level, Warning 5 %, PK level 2 10 % and murderer 20 %, unless the host changed it.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 11, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets the earlier default rows, as the plug-in had them before this update.
    /// </summary>
    /// <returns>The earlier default rows.</returns>
    internal static PlayerLosesExperienceAfterDeathPlugInConfiguration CreateOldDefaults()
    {
        static List<PlayerLosesExperienceAfterDeathPlugInConfiguration.LossPerStage> Row(double normal, double warning, double pk1, double pk2) =>
        [
            new(HeroState.New, normal),
            new(HeroState.Hero, normal),
            new(HeroState.LightHero, normal),
            new(HeroState.Normal, normal),
            new(HeroState.PlayerKillWarning, warning),
            new(HeroState.PlayerKiller1stStage, pk1),
            new(HeroState.PlayerKiller2ndStage, pk2),
        ];

        return new()
        {
            LossesPerLevel =
            [
                new(10, false, Row(1, 5, 6, 7)),
                new(150, false, Row(1, 4, 5, 6)),
                new(220, false, Row(1, 3, 4, 5)),
                new(0, true, Row(0.7, 2, 3, 4)),
            ],
        };
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(PlayerLosesExperienceAfterDeathPlugIn).GUID) is not { } plugInConfiguration
            || string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration))
        {
            // No saved settings: the plug-in's defaults apply.
            return ValueTask.CompletedTask;
        }

        PlayerLosesExperienceAfterDeathPlugInConfiguration? configuration;
        try
        {
            // Settings saved by the admin panel's editor carry reference markers ("$id", lists as "$values").
            var saved = plugInConfiguration.CustomConfiguration;
            plugInConfiguration.CustomConfiguration = WithoutReferences(JsonNode.Parse(saved))?.ToJsonString();
            try
            {
                configuration = plugInConfiguration.GetConfiguration<PlayerLosesExperienceAfterDeathPlugInConfiguration>(null);
            }
            finally
            {
                plugInConfiguration.CustomConfiguration = saved;
            }
        }
        catch (JsonException)
        {
            // Settings this update can't read stay as they are; the rows can be changed on the admin panel.
            return ValueTask.CompletedTask;
        }

        if (configuration is null || !AreEqual(configuration, CreateOldDefaults()))
        {
            // The host chose the rows.
            return ValueTask.CompletedTask;
        }

        plugInConfiguration.SetConfiguration((PlayerLosesExperienceAfterDeathPlugInConfiguration)new PlayerLosesExperienceAfterDeathPlugIn().CreateDefaultConfig(), null);
        return ValueTask.CompletedTask;
    }

    private static bool AreEqual(PlayerLosesExperienceAfterDeathPlugInConfiguration saved, PlayerLosesExperienceAfterDeathPlugInConfiguration old)
    {
        static string Key(PlayerLosesExperienceAfterDeathPlugInConfiguration configuration) => string.Join(
            ";",
            configuration.LossesPerLevel
                .OrderBy(l => l.IsMaster)
                .ThenBy(l => l.MinimumLevel)
                .Select(l => $"{l.MinimumLevel}/{l.IsMaster}:" + string.Join(",", l.Losses.OrderBy(s => s.State).Select(s => $"{s.State}={s.ExperienceLossPercentage}"))));

        return Key(saved) == Key(old);
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
