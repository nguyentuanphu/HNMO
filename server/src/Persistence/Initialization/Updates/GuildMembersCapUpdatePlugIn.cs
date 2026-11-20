// <copyright file="GuildMembersCapUpdatePlugIn.cs" company="HNMO">
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
/// This update caps a guild at 80 members (owner's choice, 2026-11-20, like the Season 6/9 server suites) in saved
/// guild limits. Only the earlier default (0: no cap) is replaced; a cap the host chose stays as it is.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("E83C5F17-2B94-4A6D-8C70-49D1B6A3E205")]
public class GuildMembersCapUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Guild: at most 80 members";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Caps a guild at 80 members, unless the host chose another cap.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);

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
                // Settings this update can't read stay as they are; the cap can be set on the admin panel.
                continue;
            }

            if (configuration is not { MaximumMembers: 0 })
            {
                // The host chose a cap.
                continue;
            }

            configuration.MaximumMembers = new GuildLimitsConfiguration().MaximumMembers;
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
