// <copyright file="TimetableControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Tests for <see cref="WebsiteTimetableController"/>.
/// </summary>
[TestFixture]
public sealed class TimetableControllerTests
{
    /// <summary>Returns <c>off: true</c> when <see cref="TimetablePlugIn"/> is disabled or not yet activated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnsOffWhenDisabledAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var gameConfig = new Mock<IDataSource<GameConfiguration>>();
        gameConfig.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameConfiguration());

        var controller = new WebsiteTimetableController(
            persistence,
            gameConfig.Object,
            new Dictionary<int, IGameServer>(),
            NullLogger<WebsiteTimetableController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.GetAsync().ConfigureAwait(false) as ContentResult;
        Assert.That(result, Is.Not.Null);
        using var doc = JsonDocument.Parse(result!.Content!);
        Assert.That(doc.RootElement.GetProperty("off").GetBoolean(), Is.True);
        Assert.That(doc.RootElement.GetProperty("entries").GetArrayLength(), Is.EqualTo(0));
    }

    /// <summary>Returns active scheduled entries once <see cref="TimetablePlugIn"/> is activated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnsScheduleWhenActivatedAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        using (var ctx = persistence.CreateNewConfigurationContext())
        {
            var row = ctx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(TimetablePlugIn).GUID;
            row.IsActive = true;
            row.SetConfiguration(new TimetableConfiguration
            {
                TimeZoneId = "Australia/Sydney",
                CustomEntries =
                [
                    new TimetableCustomEntry
                    {
                        Id = "demo",
                        NameEn = "Demo Invasion",
                        NameVi = "Xam lang Demo",
                        MapsEn = "Lorencia",
                        MapsVi = "Lorencia",
                        DropsEn = "Jewels",
                        DropsVi = "Ngoc",
                        ScheduleTimes = "12:00",
                    },
                ],
            }, null);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        var gameConfig = new Mock<IDataSource<GameConfiguration>>();
        gameConfig.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameConfiguration());

        var controller = new WebsiteTimetableController(
            persistence,
            gameConfig.Object,
            new Dictionary<int, IGameServer>(),
            NullLogger<WebsiteTimetableController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.GetAsync().ConfigureAwait(false) as ContentResult;
        Assert.That(result, Is.Not.Null);
        using var doc = JsonDocument.Parse(result!.Content!);
        Assert.That(doc.RootElement.GetProperty("off").GetBoolean(), Is.False);
        var entries = doc.RootElement.GetProperty("entries");
        Assert.That(entries.GetArrayLength(), Is.GreaterThanOrEqualTo(1));
        Assert.That(entries.EnumerateArray().Any(e => e.GetProperty("id").GetString() == "demo"), Is.True);
    }
}
