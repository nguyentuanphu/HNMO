// <copyright file="ServerLogPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using HNMO.Web.AdminPanel.Pages;

/// <summary>The admin page "Server log": its route and reading the end of a log of the logs folder only.</summary>
[TestFixture]
public class ServerLogPageTests
{
    /// <summary>The page answers at /server-log.</summary>
    [Test]
    public void RegistersRoute()
    {
        var route = typeof(ServerLog).GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>().FirstOrDefault();
        Assert.That(route?.Template, Is.EqualTo("/server-log"));
    }

    /// <summary>The last lines of a log come back in order; files outside the folder or not logs are refused.</summary>
    [Test]
    public void ReadsTheEndOfALogOnly()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hnmo-log-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllLines(Path.Combine(dir, "launcher.log"), Enumerable.Range(1, 1000).Select(i => $"line {i}"));
            File.WriteAllText(Path.Combine(dir, "secret.txt"), "no");
            var lines = ServerLog.Tail(dir, "launcher.log", 5);
            Assert.That(lines, Is.EqualTo(new[] { "line 996", "line 997", "line 998", "line 999", "line 1000" }));
            Assert.That(ServerLog.Tail(dir, "secret.txt", 5), Is.Empty);
            Assert.That(ServerLog.Tail(dir, "../launcher.log", 5), Is.Empty);
            Assert.That(ServerLog.Tail(dir, "missing.log", 5), Is.Empty);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
