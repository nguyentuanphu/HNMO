// <copyright file="MarriageControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.Marriage;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Tests for <see cref="WebsiteMarriageController"/>.
/// </summary>
[TestFixture]
public sealed class MarriageControllerTests
{
    /// <summary>Reports <c>enabled: false</c> until the Marriage plug-in is activated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnsDisabledUntilActivatedAsync()
    {
        var controller = new WebsiteMarriageController(new InMemoryPersistenceContextProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var res = await controller.GetAsync().ConfigureAwait(false) as ContentResult;
        Assert.That(res, Is.Not.Null);
        using var doc = JsonDocument.Parse(res!.Content!);
        Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
    }

    /// <summary>Returns registered couples and titles when activated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnsCouplesListWhenActiveAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-marriage-web-{Guid.NewGuid():N}.json");
        try
        {
            var cfg = new MarriageConfiguration { FileName = file, MinimumLevel = 1, RequireCeremonyLocation = false };
            var engine = new MarriageEngine(cfg, MarriageStore.Open(file), true);
            engine.Propose("Alice", "a", 50, "Bob", 50, DateTime.UtcNow);
            engine.AcceptCeremony("Bob", "b", 50, 50, true, DateTime.UtcNow, out _);
            engine.SetCustomTitle("Alice", "True Love");

            var persistence = new InMemoryPersistenceContextProvider();
            using (var ctx = persistence.CreateNewConfigurationContext())
            {
                var row = ctx.CreateNew<PlugInConfiguration>();
                row.TypeId = typeof(MarriagePlugIn).GUID;
                row.IsActive = true;
                row.SetConfiguration(cfg, null);
                await ctx.SaveChangesAsync().ConfigureAwait(false);
            }

            var controller = new WebsiteMarriageController(persistence);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            var res = await controller.GetAsync().ConfigureAwait(false) as ContentResult;
            Assert.That(res, Is.Not.Null);
            using var doc = JsonDocument.Parse(res!.Content!);
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            var couples = doc.RootElement.GetProperty("couples");
            Assert.That(couples.GetArrayLength(), Is.EqualTo(1));
            Assert.That(couples[0].GetProperty("partnerA").GetString(), Is.EqualTo("Alice"));
            Assert.That(couples[0].GetProperty("title").GetString(), Is.EqualTo("True Love"));
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
