// <copyright file="HappyHourScheduleUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;

/// <summary>
/// Saved Happy Hour settings with the earlier default schedule get the schedule of the Season 6/9 server suites.
/// </summary>
[TestFixture]
internal class HappyHourScheduleUpdateTest
{
    private static readonly TimeOnly[] NewTimetable = [new(2, 0), new(8, 0), new(14, 0), new(20, 0)];

    /// <summary>
    /// Registers the converter of the localized messages, which the host application registers at its start.
    /// </summary>
    [OneTimeSetUp]
    public void RegisterLocalizedStringConverter()
    {
        if (!JsonConverterRegistry.Converters.OfType<HNMO.Interfaces.LocalizedStringJsonConverter>().Any())
        {
            JsonConverterRegistry.RegisterConverter(new HNMO.Interfaces.LocalizedStringJsonConverter());
        }
    }

    /// <summary>The earlier default (1 hour at 00:05, 06:05, 12:05, 18:05) becomes 2 hours at 02, 08, 14, 20 with 15 % drops.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EarlierDefaultScheduleIsReplacedAsync()
    {
        var (configuration, plugIn) = Create(new HappyHourConfiguration
        {
            TaskDuration = TimeSpan.FromHours(1),
            Timetable = [new(0, 5), new(6, 5), new(12, 5), new(18, 5)],
            ExperienceMultiplier = 1.5f,
            StartMessage = "Happy!",
        });

        await ApplyAsync(configuration).ConfigureAwait(false);
        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<HappyHourConfiguration>(null)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Timetable, Is.EqualTo(NewTimetable));
            Assert.That(result.TaskDuration, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(result.ItemDropBonusPercent, Is.EqualTo(15));
            Assert.That(result.StartMessage.Value, Is.EqualTo("Happy!"), "the host's message stays");
        });
    }

    /// <summary>A schedule the host chose stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostScheduleIsKeptAsync()
    {
        var (configuration, plugIn) = Create(new HappyHourConfiguration
        {
            TaskDuration = TimeSpan.FromHours(1),
            Timetable = [new(21, 0)],
        });

        await ApplyAsync(configuration).ConfigureAwait(false);

        var result = plugIn.GetConfiguration<HappyHourConfiguration>(null)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Timetable, Is.EqualTo(new[] { new TimeOnly(21, 0) }));
            Assert.That(result.TaskDuration, Is.EqualTo(TimeSpan.FromHours(1)));
        });
    }

    private static (HNMO.Persistence.BasicModel.GameConfiguration Configuration, HNMO.Persistence.BasicModel.PlugInConfiguration PlugIn) Create(HappyHourConfiguration saved)
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(HappyHourPlugIn).GUID, IsActive = true };
        plugIn.SetConfiguration(saved, null);
        configuration.PlugInConfigurations.Add(plugIn);
        return (configuration, plugIn);
    }

    private static ValueTask ApplyAsync(HNMO.Persistence.BasicModel.GameConfiguration configuration)
        => new HappyHourScheduleUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration);
}
