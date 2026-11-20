// <copyright file="ZenLossBagOnlyUpdatePlugIn.cs" company="HNMO">
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
/// This update stops the zen loss on death from taking zen out of the vault (owner's choice, 2026-11-21, as the
/// Season 6/9 server suites): the 1-4 % by level are only taken from the bag. Only a save which still holds the
/// earlier default vault losses (1, 2, 3 and 4 %) is changed; vault losses the host chose stay.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("3F0C7E52-9A1D-4B86-A2E4-6D5B19C80F37")]
public class ZenLossBagOnlyUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Zen loss on death only from the bag";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The zen loss on death no longer takes zen from the vault, unless the host chose vault losses.";

    /// <summary>
    /// The earlier default vault losses by (minimum level, master).
    /// </summary>
    private static readonly (int MinimumLevel, bool IsMaster, double VaultLoss)[] OldVaultLosses =
    [
        (10, false, 1),
        (150, false, 2),
        (220, false, 3),
        (0, true, 4),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(PlayerLosesMoneyAfterDeathPlugIn).GUID) is not { } plugInConfiguration
            || string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration))
        {
            // No saved settings: the plug-in's defaults (no vault loss) apply.
            return ValueTask.CompletedTask;
        }

        PlayerLosesMoneyAfterDeathPlugInConfiguration? configuration;
        try
        {
            // Settings saved by the admin panel's editor carry reference markers ("$id", lists as "$values").
            var saved = plugInConfiguration.CustomConfiguration;
            plugInConfiguration.CustomConfiguration = WithoutReferences(JsonNode.Parse(saved))?.ToJsonString();
            try
            {
                configuration = plugInConfiguration.GetConfiguration<PlayerLosesMoneyAfterDeathPlugInConfiguration>(null);
            }
            finally
            {
                plugInConfiguration.CustomConfiguration = saved;
            }
        }
        catch (JsonException)
        {
            // Settings this update can't read stay as they are; the vault loss can be changed on the admin panel.
            return ValueTask.CompletedTask;
        }

        if (configuration?.LossesPerLevel is not { Count: > 0 } losses
            || losses.Count != OldVaultLosses.Length
            || !OldVaultLosses.All(old => losses.Any(l => l.MinimumLevel == old.MinimumLevel && l.IsMaster == old.IsMaster && l.VaultMoneyLossPercentage == old.VaultLoss)))
        {
            // The host chose the vault losses.
            return ValueTask.CompletedTask;
        }

        foreach (var loss in losses)
        {
            loss.VaultMoneyLossPercentage = 0;
        }

        plugInConfiguration.SetConfiguration(configuration, null);
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
