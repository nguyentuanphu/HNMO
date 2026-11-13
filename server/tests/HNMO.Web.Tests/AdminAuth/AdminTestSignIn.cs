// <copyright file="AdminTestSignIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.AdminAuth;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Auth;

/// <summary>
/// Signs the HTTP client of a test in, without the login page.
/// </summary>
internal static class AdminTestSignIn
{
    /// <summary>
    /// Maps an endpoint which signs the caller in as the specified user, with the claims of a real login.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The path of the endpoint.</param>
    /// <param name="user">The user which is signed in.</param>
    public static void MapTestSignIn(this IEndpointRouteBuilder endpoints, string path, AdminUser user)
    {
        endpoints.MapGet(path, async (HttpContext http) =>
        {
            // The claims of a real login, which include the roles implied by the assigned one.
            var claims = AdminLoginService.CreateClaims(user, usedSecondFactor: true);
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)).ConfigureAwait(false);
        });
    }
}
