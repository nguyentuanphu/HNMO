// <copyright file="WebsiteBuildCalculatorController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.BuildCalculator;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Public stats / build calculator endpoint (ROADMAP 14.21):
/// exposes character classes &amp; equippable items metadata (<c>GET /api/website/build-calculator</c>)
/// and computes exact server attributes via <see cref="BuildCalculatorService"/> (<c>POST /api/website/build-calculator</c>).
/// </summary>
[ApiController]
[Route("api/website/build-calculator")]
[AllowAnonymous]
public sealed class WebsiteBuildCalculatorController(IDataSource<GameConfiguration> gameConfigurationSource) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Gets available character classes and equippable items metadata for calculator pickers.</summary>
    /// <returns>JSON metadata.</returns>
    [HttpGet]
    public async Task<IActionResult> GetMetadataAsync()
    {
        var ct = this.HttpContext.RequestAborted;
        var config = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, ct).ConfigureAwait(false);
        return this.Content(JsonSerializer.Serialize(BuildCalculatorService.GetMetadata(config), JsonOptions), "application/json");
    }

    /// <summary>Computes exact character damage, defense, speed, HP/MP/SD/AG, and rates using server formulas.</summary>
    /// <param name="input">Build parameters.</param>
    /// <returns>Computed stats JSON or 400 if class not found.</returns>
    [HttpPost]
    public async Task<IActionResult> CalculateAsync([FromBody] BuildCalculatorInput input)
    {
        if (input is null)
        {
            return this.BadRequest(new { error = "invalid_request" });
        }

        var ct = this.HttpContext.RequestAborted;
        var config = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, ct).ConfigureAwait(false);
        var result = BuildCalculatorService.Compute(config, input);
        if (result is null)
        {
            return this.BadRequest(new { error = "unknown_class" });
        }

        return this.Content(JsonSerializer.Serialize(result, JsonOptions), "application/json");
    }
}
