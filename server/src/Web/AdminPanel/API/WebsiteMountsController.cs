// <copyright file="WebsiteMountsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.MountChain;
using HNMO.Persistence;

/// <summary>
/// Public &amp; embeddable Mount Chain collection endpoint (<c>GET /api/website/mounts</c>).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/mounts")]
public sealed class WebsiteMountsController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets Mount Chain progression tiers and collector counts.</summary>
    /// <param name="login">Optional account login name to include that account's unlocked mounts.</param>
    /// <returns>Plain-ASCII JSON; <c>"enabled":false</c> when disabled.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] string? login = null)
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<MountChainConfiguration>(
            persistence,
            typeof(MountChainPlugIn),
            () => new MountChainConfiguration(),
            cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        var store = MountChainStore.Open(configuration.FileName);
        var engine = new MountChainEngine(configuration, store, enabled: true);
        if (!string.IsNullOrWhiteSpace(login))
        {
            var trimmed = login.Trim();
            var unlocked = store.Read(d => d.Accounts.TryGetValue(trimmed, out var a)
                ? new { a.Shards, a.ActiveTierId, Unlocked = a.Unlocked.Keys.ToList() }
                : null);
            if (unlocked is not null)
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    v = 1,
                    enabled = true,
                    accountShards = unlocked.Shards,
                    accountActiveTierId = unlocked.ActiveTierId,
                    accountUnlocked = unlocked.Unlocked,
                    publicState = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(MountChainStateWriter.WritePublic(engine)),
                });
                return this.Content(payload, "application/json");
            }
        }

        return this.Content(MountChainStateWriter.WritePublic(engine), "application/json");
    }
}
