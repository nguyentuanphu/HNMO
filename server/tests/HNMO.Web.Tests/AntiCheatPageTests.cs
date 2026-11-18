// <copyright file="AntiCheatPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.AntiCheat;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Integration checks for <see cref="AntiCheat"/> route and audit log storage.
/// </summary>
[TestFixture]
public class AntiCheatPageTests
{
    /// <summary>Verifies <see cref="AntiCheat"/> exposes route <c>/anti-cheat</c> and audit store clear/append works cleanly.</summary>
    [Test]
    public void RegistersRouteAndManagesAuditEntries()
    {
        var routeAttr = typeof(AntiCheat)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/anti-cheat"));

        var tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-ac-page-{Guid.NewGuid():N}.json");
        try
        {
            var store = AntiCheatAuditStore.Open(tempFile);
            store.Append(
                new AntiCheatViolationEntry
                {
                    OccurredAtUtc = DateTime.UtcNow,
                    Account = "test1",
                    Character = "Knight1",
                    IpAddress = "198.51.100.5",
                    ViolationType = "SafeZoneAttack",
                    Details = "Blocked in Lorencia",
                    ActionTaken = "Blocked",
                },
                50);
            Assert.That(store.ListRecent(), Has.Count.EqualTo(1));
            store.Clear();
            Assert.That(store.ListRecent(), Is.Empty);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
