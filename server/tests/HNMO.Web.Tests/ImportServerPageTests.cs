// <copyright file="ImportServerPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using HNMO.GameLogic.ServerImporter;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Integration checks for <see cref="ImportServer"/> route declaration and backup store listing.
/// </summary>
[TestFixture]
public class ImportServerPageTests
{
    /// <summary>Verifies <see cref="ImportServer"/> registers route <c>/import-server</c> and backup snapshots serialize cleanly.</summary>
    [Test]
    public void RegistersRouteAndManagesBackupSnapshots()
    {
        var routeAttr = typeof(ImportServer)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/import-server"));

        var tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-imp-page-{Guid.NewGuid():N}");
        try
        {
            var id = $"import-test-{Guid.NewGuid():N}";
            LegacyImportBackupSnapshot.SaveAtomic(tempDir, new LegacyImportBackupSnapshot
            {
                BackupId = id,
                CreatedAtSydney = "2026-11-18 21:00:00",
                SourceVersion = LegacyServerVersion.Season6,
                SummaryText = "Season6: 4 applied, 1 skipped",
            });

            var list = LegacyImportBackupSnapshot.ListAll(tempDir);
            Assert.That(list.Any(x => x.BackupId == id), Is.True);
            LegacyImportBackupSnapshot.Delete(tempDir, id);
            Assert.That(LegacyImportBackupSnapshot.TryLoad(tempDir, id), Is.Null);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
