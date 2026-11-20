// <copyright file="MuHelperLevelUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.MuHelper;
using HNMO.PlugIns;

/// <summary>
/// This update makes the MU helper start at level 80, as in the Season 6/9 server suites, and keeps level 1 for VIP
/// accounts (owner's choice, 20 Nov 2026). A minimum level the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7A3F0C58-E19B-4D26-A845-3B9E6F1D2C70")]
public class MuHelperLevelUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "MU helper from level 80, VIP from level 1";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The MU helper needs level 80 (VIP accounts level 1), unless the host changed the minimum level.";

    private const int OldMinimumLevel = 1;

    private const int NewMinimumLevel = 80;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(MuHelperFeaturePlugIn).GUID) is not { } plugInConfiguration
            || string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration))
        {
            // No saved settings: the plug-in's defaults (80, VIP 1) apply.
            return ValueTask.CompletedTask;
        }

        MuHelperConfiguration? configuration;
        try
        {
            // Settings saved by the admin panel's editor carry reference markers ("$id", lists as "$values").
            configuration = WithoutReferences(JsonNode.Parse(plugInConfiguration.CustomConfiguration))?.Deserialize<MuHelperConfiguration>();
        }
        catch (JsonException)
        {
            // Settings this update can't read stay as they are; the level can be changed on the admin panel.
            return ValueTask.CompletedTask;
        }

        if (configuration is { MinLevel: OldMinimumLevel })
        {
            configuration.MinLevel = NewMinimumLevel;
            plugInConfiguration.SetConfiguration(configuration, null);
        }

        return ValueTask.CompletedTask;
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
