// <copyright file="WebsitePrefixMiddleware.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel;

using System.Net;
using System.Text.RegularExpressions;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The admin panel behind the public website: the website (on the same computer) forwards
/// https://site/&lt;admin path&gt;/… here with the header <see cref="HeaderName"/>, and the admin panel then
/// works under that path (links, the live connection, the login redirect). While no admin user exists,
/// the panel is open on the host computer for the first setup; through the website it stays closed then.
/// </summary>
public static partial class WebsitePrefixMiddleware
{
    /// <summary>
    /// The header with the path prefix, set only by the website (which removes it from visitors' requests).
    /// </summary>
    public const string HeaderName = "X-HNMO-Website-Prefix";

    /// <summary>
    /// Determines whether a request came through the website (from a visitor anywhere), not straight from the host computer.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><c>true</c> for a request the website forwarded.</returns>
    public static bool IsThroughWebsite(HttpRequest request) => request.Headers.ContainsKey(HeaderName);

    /// <summary>
    /// Adds the middleware; call it first in the pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static IApplicationBuilder UseWebsitePrefix(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // Only the website on this computer may send the header; the admin panel listens on 127.0.0.1.
            var prefix = values.ToString();
            if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote) || !PrefixPattern().IsMatch(prefix))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var setupMode = context.RequestServices.GetService<AdminPanelSetupMode>();
            if (setupMode is null || await setupMode.IsActiveAsync(context.RequestAborted).ConfigureAwait(false))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(
                    "The admin panel isn't open through the website yet: create an admin user first, on the host computer (http://127.0.0.1:8095).",
                    context.RequestAborted).ConfigureAwait(false);
                return;
            }

            context.Request.PathBase = prefix;
            await next(context).ConfigureAwait(false);
        });
    }

    [GeneratedRegex("^/[A-Za-z0-9][A-Za-z0-9_-]{2,39}$")]
    private static partial Regex PrefixPattern();
}
