// <copyright file="GuildCreateLevelUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.PlugIns;

/// <summary>
/// This update sets the level to create a guild to 150 (owner's choice, 2026-11-21, like the Season 6/9 server suites)
/// in saved guild limits. Only the earlier default (0: any level) is replaced; a level the host chose stays as it is.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7C2A9E41-6B3D-4F85-A1E0-93D5C8B4F716")]
public class GuildCreateLevelUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Guild: created from level 150";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "A guild is created from level 150, unless the host chose another level.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var plugInConfiguration in gameConfiguration.PlugInConfigurations.Where(c => c.TypeId == typeof(GuildLimitsPlugIn).GUID && !string.IsNullOrWhiteSpace(c.CustomConfiguration)))
        {
            GuildLimitsConfiguration? configuration;
            try
            {
                // Settings saved by the admin panel's editor carry reference markers ("$id").
                configuration = WithoutReferences(JsonNode.Parse(plugInConfiguration.CustomConfiguration!))?.Deserialize<GuildLimitsConfiguration>();
            }
            catch (JsonException)
            {
                // Settings this update can't read stay as they are; the level can be set on the admin panel.
                continue;
            }

            if (configuration is not { MinimumCreateLevel: 0 })
            {
                // The host chose a level.
                continue;
            }

            configuration.MinimumCreateLevel = new GuildLimitsConfiguration().MinimumCreateLevel;
            plugInConfiguration.SetConfiguration(configuration, null);
        }

        return ValueTask.CompletedTask;
    }

    private static JsonNode? WithoutReferences(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return node?.DeepClone();
        }

        var result = new JsonObject();
        foreach (var (key, value) in obj)
        {
            if (key is not ("$id" or "$ref"))
            {
                result[key] = value?.DeepClone();
            }
        }

        return result;
    }
}
