// <copyright file="WorldEditorController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading.Tasks;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Serves game world textures, heights, terrain files, and models for the Admin Panel 3D World Editor (ROADMAP 14.24).
/// </summary>
[ApiController]
[Route("api/world-editor")]
[Authorize(Policy = AdminPolicies.Administrator)]
public sealed class WorldEditorController(WorldEditorDataService dataService) : ControllerBase
{
    /// <summary>Streams a world or object asset file for the Three.js 3D viewport.</summary>
    /// <param name="path">Relative path under the game data root (for example <c>World1/TerrainHeight.OZB</c>).</param>
    /// <returns>Asset bytes or 404 when missing.</returns>
    [HttpGet("data/{**path}")]
    public async Task<IActionResult> GetDataFileAsync(string path)
    {
        var result = await dataService.TryReadDataBytesAsync(path, this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (result is null)
        {
            return this.NotFound();
        }

        this.Response.Headers.CacheControl = "no-store";
        return this.File(result.Value.Bytes, result.Value.ContentType);
    }
}
