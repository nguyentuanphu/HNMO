// <copyright file="BadRequestStatusExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Makes a request which the server refuses while reading it (e.g. a body over the size limit) answer with the
/// status of the refusal (413, 400, ...) instead of the exception handler's 500 page.
/// </summary>
public static class BadRequestStatusExtensions
{
    /// <summary>
    /// Adds the middleware; it has to be added after the exception handler, so that it is the inner one.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same instance, to allow chaining of further calls.</returns>
    public static IApplicationBuilder UseBadRequestStatus(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ex.StatusCode;
            }
        });
    }
}
