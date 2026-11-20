// <copyright file="HappyHourScheduleUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.PlugIns;

/// <summary>
/// This update gives saved Happy Hour settings which still have the earlier default schedule (1 hour at 00:05, 06:05,
/// 12:05 and 18:05) the schedule of the Season 6/9 server suites: 2 hours at 02:00, 08:00, 14:00 and 20:00, with 15 %
/// more item drops (owner's choice, 20 Nov 2026). A schedule the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("C6D14E82-5B3A-4F07-9E61-A28F7B0D3C95")]
public class HappyHourScheduleUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Happy Hour: 8 hours a day with more item drops";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Happy Hour 2 hours at 02:00, 08:00, 14:00 and 20:00 with 15 % more item drops, unless the host changed the schedule.";

    private static readonly TimeOnly[] OldTimetable = [new(0, 5), new(6, 5), new(12, 5), new(18, 5)];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(HappyHourPlugIn).GUID) is not { } plugInConfiguration
            || string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration))
        {
            // No saved settings: the plug-in's defaults apply.
            return ValueTask.CompletedTask;
        }

        HappyHourConfiguration? configuration;
        try
        {
            // Settings saved by the admin panel's editor carry reference markers ("$id", lists as "$values").
            // Read with the plug-in configuration's own serializer options: the messages are localized strings.
            var saved = plugInConfiguration.CustomConfiguration;
            plugInConfiguration.CustomConfiguration = WithoutReferences(JsonNode.Parse(saved))?.ToJsonString();
            try
            {
                configuration = plugInConfiguration.GetConfiguration<HappyHourConfiguration>(null);
            }
            finally
            {
                plugInConfiguration.CustomConfiguration = saved;
            }
        }
        catch (JsonException)
        {
            // Settings this update can't read stay as they are; the schedule can be changed on the admin panel.
            return ValueTask.CompletedTask;
        }

        if (configuration is not null
            && configuration.TaskDuration == TimeSpan.FromHours(1)
            && configuration.Timetable.OrderBy(t => t).SequenceEqual(OldTimetable))
        {
            var defaults = HappyHourConfiguration.Default;
            configuration.Timetable = defaults.Timetable;
            configuration.TaskDuration = defaults.TaskDuration;
            configuration.ItemDropBonusPercent = defaults.ItemDropBonusPercent;
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
