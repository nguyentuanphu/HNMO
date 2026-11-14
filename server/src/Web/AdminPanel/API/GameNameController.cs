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
    private static Tuple<GameInfo, DateTime> _cached = Tuple.Create(GameInfo.Default, DateTime.MinValue);

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
            cached = Tuple.Create(await this.ReadAsync().ConfigureAwait(false), DateTime.UtcNow + CacheTime);
            _cached = cached;
        }

        this.Response.Headers.CacheControl = "no-cache";
        var info = cached.Item1;
        return this.Json(new { name = info.Name, links = new { zalo = info.Zalo, discord = info.Discord, facebook = info.Facebook } });
    }

    /// <summary>
    /// Reads the configured name and links; the defaults when the plug-in is deactivated or empty.
    /// </summary>
    private async Task<GameInfo> ReadAsync()
    {
        try
        {
            using var context = this._contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), false);
            var configurations = await context.GetAsync<PlugInConfiguration>(this.HttpContext.RequestAborted).ConfigureAwait(false);
            var configuration = configurations.FirstOrDefault(c => c.TypeId == PlugInTypeId);
            if (configuration is not { IsActive: true } || string.IsNullOrWhiteSpace(configuration.CustomConfiguration))
            {
                return GameInfo.Default;
            }

            using var document = JsonDocument.Parse(configuration.CustomConfiguration);
            var root = document.RootElement;
            string Text(string property) => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;
            var name = Text(nameof(GameNameConfiguration.Name));
            return new GameInfo(
                string.IsNullOrWhiteSpace(name) ? GameNameConfiguration.DefaultName : name,
                SafeLink(Text(nameof(GameNameConfiguration.ZaloLink))),
                SafeLink(Text(nameof(GameNameConfiguration.DiscordLink))),
                SafeLink(Text(nameof(GameNameConfiguration.FacebookLink))));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return GameInfo.Default;
        }
    }

    /// <summary>
    /// Only https links go to the players' pages; anything else is left out.
    /// </summary>
    private static string SafeLink(string link)
    {
        return Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : string.Empty;
    }

    private sealed record GameInfo(string Name, string Zalo, string Discord, string Facebook)
    {
        public static GameInfo Default { get; } = new(GameNameConfiguration.DefaultName, string.Empty, string.Empty, string.Empty);
    }
}
