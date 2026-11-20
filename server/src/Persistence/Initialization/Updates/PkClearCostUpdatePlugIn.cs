// <copyright file="PkClearCostUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.PlugIns;

/// <summary>
/// This update lowers the zen cost of clearing one player kill (/pkclear) from 10,000,000 to 1,000,000, as in the
/// Season 6/9 server suites (owner's choice, 20 Nov 2026). A cost the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("E2C85B17-4D96-4A3F-B07E-9F1A6C2D8E35")]
public class PkClearCostUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "PK clear: 1,000,000 zen per kill";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Lowers the zen cost of /pkclear from 10,000,000 to 1,000,000 per kill, unless the host changed it.";

    private const int OldCost = 10_000_000;

    private const int NewCost = 1_000_000;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 16, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(PkClearChatCommandPlugIn).GUID) is not { } plugInConfiguration
            || string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration))
        {
            // No saved settings: the plug-in's default (1,000,000) applies.
            return ValueTask.CompletedTask;
        }

        PkClearChatCommandPlugIn.PKClearConfiguration? configuration;
        try
        {
            // Settings saved by the admin panel's editor carry reference markers ("$id", lists as "$values").
            configuration = WithoutReferences(JsonNode.Parse(plugInConfiguration.CustomConfiguration))?.Deserialize<PkClearChatCommandPlugIn.PKClearConfiguration>();
        }
        catch (JsonException)
        {
            // Settings this update can't read stay as they are; the cost can be changed on the admin panel.
            return ValueTask.CompletedTask;
        }

        if (configuration is { ZenCostPerKill: OldCost })
        {
            configuration.ZenCostPerKill = NewCost;
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
