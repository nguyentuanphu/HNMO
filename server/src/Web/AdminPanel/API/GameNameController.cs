// <copyright file="GameNameController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.PlugIns;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;

/// <summary>
/// The game's name which players see, for the web game (no login needed: it's shown on the login screen).
/// It's set in the admin panel in the configuration of the "Game name" plug-in, and read from the saved
/// configuration, so a change counts at once.
/// </summary>
[Route("api/")]
[AllowAnonymous]
public class GameNameController : Controller
{
    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(10);
    private static readonly Guid PlugInTypeId = typeof(GameNamePlugIn).GUID;
    private static Tuple<string, DateTime> _cached = Tuple.Create(GameNameConfiguration.DefaultName, DateTime.MinValue);

    private readonly IPersistenceContextProvider _contextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameNameController"/> class.
    /// </summary>
    /// <param name="contextProvider">The persistence context provider.</param>
    public GameNameController(IPersistenceContextProvider contextProvider)
    {
        this._contextProvider = contextProvider;
    }

    /// <summary>
    /// Gets the game's name.
    /// </summary>
    /// <returns>The name, as <c>{ "name": "..." }</c>.</returns>
    [HttpGet]
    [Route("game-name")]
    public async Task<IActionResult> GetGameNameAsync()
    {
        var cached = _cached;
        if (DateTime.UtcNow >= cached.Item2)
        {
            cached = Tuple.Create(await this.ReadNameAsync().ConfigureAwait(false), DateTime.UtcNow + CacheTime);
            _cached = cached;
        }

        this.Response.Headers.CacheControl = "no-cache";
        return this.Json(new { name = cached.Item1 });
    }

    /// <summary>
    /// Reads the configured name; the default when the plug-in is deactivated or has no name.
    /// </summary>
    private async Task<string> ReadNameAsync()
    {
        try
        {
            using var context = this._contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), false);
            var configurations = await context.GetAsync<PlugInConfiguration>(this.HttpContext.RequestAborted).ConfigureAwait(false);
            var configuration = configurations.FirstOrDefault(c => c.TypeId == PlugInTypeId);
            if (configuration is not { IsActive: true } || string.IsNullOrWhiteSpace(configuration.CustomConfiguration))
            {
                return GameNameConfiguration.DefaultName;
            }

            using var document = JsonDocument.Parse(configuration.CustomConfiguration);
            return document.RootElement.TryGetProperty(nameof(GameNameConfiguration.Name), out var name)
                   && name.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : GameNameConfiguration.DefaultName;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return GameNameConfiguration.DefaultName;
        }
    }
}
