// <copyright file="LuckyNumberControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.LuckyNumber;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Tests for <see cref="WebsiteLuckyNumberController"/>.
/// </summary>
[TestFixture]
public sealed class LuckyNumberControllerTests
{
    /// <summary>Public endpoint reports <c>enabled: false</c> until the plug-in is activated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnsDisabledUntilActivatedAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var gameConfig = new Mock<IDataSource<GameConfiguration>>();
        gameConfig.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameConfiguration());

        var controller = new WebsiteLuckyNumberController(persistence, gameConfig.Object, NullLogger<WebsiteLuckyNumberController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var res = await controller.GetAsync().ConfigureAwait(false) as ContentResult;
        Assert.That(res, Is.Not.Null);
        using var doc = JsonDocument.Parse(res!.Content!);
        Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
    }

    /// <summary>Activated plug-in exposes public odds, prizes, and drawn numbers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnsOddsPrizesAndDrawsWhenActiveAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-lucky-web-{Guid.NewGuid():N}.json");
        try
        {
            var cfg = new LuckyNumberConfiguration { FileName = file, ExactMatchCoins = 333 };
            var engine = new LuckyNumberEngine(cfg, LuckyNumberStore.Open(file), true);
            engine.Pick("p1", "Hero1", 12, DateTime.UtcNow.AddDays(-1), "game");
            engine.DrawNow(DateTime.UtcNow, overrideWinningNumber: 12);

            var persistence = new InMemoryPersistenceContextProvider();
            using (var ctx = persistence.CreateNewConfigurationContext())
            {
                var row = ctx.CreateNew<PlugInConfiguration>();
                row.TypeId = typeof(LuckyNumberPlugIn).GUID;
                row.IsActive = true;
                row.SetConfiguration(cfg, null);
                await ctx.SaveChangesAsync().ConfigureAwait(false);
            }

            var gameConfig = new Mock<IDataSource<GameConfiguration>>();
            gameConfig.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GameConfiguration());

            var controller = new WebsiteLuckyNumberController(persistence, gameConfig.Object, NullLogger<WebsiteLuckyNumberController>.Instance);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            var res = await controller.GetAsync().ConfigureAwait(false) as ContentResult;
            Assert.That(res, Is.Not.Null);
            using var doc = JsonDocument.Parse(res!.Content!);
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("prizes").GetProperty("exact").GetProperty("coins").GetInt32(), Is.EqualTo(333));
            Assert.That(doc.RootElement.GetProperty("recentDraws").GetArrayLength(), Is.GreaterThanOrEqualTo(1));
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }

            if (File.Exists(file + ".bak"))
            {
                File.Delete(file + ".bak");
            }
        }
    }
}
