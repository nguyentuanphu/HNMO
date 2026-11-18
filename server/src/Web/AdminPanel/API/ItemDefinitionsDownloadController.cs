// <copyright file="ItemDefinitionsDownloadController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.ItemDefinitions;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;

/// <summary>
/// Admin-only download endpoint that packages updated web/desktop client <c>Data/Items</c> files into a ZIP archive.
/// </summary>
[ApiController]
[Authorize(Policy = AdminPolicies.Administrator)]
[Route("api/admin/item-definitions")]
public sealed class ItemDefinitionsDownloadController(IHostSettingsService hostSettingsService) : ControllerBase
{
    /// <summary>
    /// Downloads a ZIP bundle containing patched <c>Data/Items/*.json</c>, <c>Data/Items/Models/*.json</c>,
    /// <c>Data/Effects/GlowColors.json</c>, and the portable <c>hnmo-item-overrides.json</c> manifest.
    /// </summary>
    /// <returns>The ZIP archive response.</returns>
    [HttpGet("export")]
    public IActionResult ExportZip()
    {
        var settings = hostSettingsService.Load();
        var dataRoot = ItemClientDataSync.TryResolveDataRoot(settings.DataPath);
        var overrides = ItemDefinitionStateStore.GetAll();
        var zipBytes = ItemClientDataSync.BuildExportZip(dataRoot, overrides);
        return this.File(zipBytes, "application/zip", "hnmo-item-definitions-export.zip");
    }
}
