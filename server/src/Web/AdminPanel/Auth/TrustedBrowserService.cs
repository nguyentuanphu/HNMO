// <copyright file="TrustedBrowserService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Auth;

using System.Security.Cryptography;
using HNMO.Persistence.AdminAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Trusted browsers of the admin panel (owner, 2026-11-21): after the 6-digit code was right once, the browser is trusted
/// for <see cref="TrustDays"/> days and the code isn't asked there again; the password always is. The trust is a protected
/// cookie bound to the user and its security stamp, so changing the password, resetting the authenticator or "forget
/// trusted browsers" (which renews the stamp) ends it everywhere.
/// </summary>
public class TrustedBrowserService
{
    /// <summary>The cookie's name.</summary>
    public const string CookieName = "hnmo_admin_trusted";

    /// <summary>The days a browser stays trusted.</summary>
    public const int TrustDays = 30;

    private readonly ITimeLimitedDataProtector _protector;

    /// <summary>Initializes a new instance of the <see cref="TrustedBrowserService"/> class.</summary>
    /// <param name="dataProtectionProvider">The data protection provider (its keys are shared by every HNMO program).</param>
    public TrustedBrowserService(IDataProtectionProvider dataProtectionProvider)
    {
        this._protector = dataProtectionProvider.CreateProtector("HNMO.AdminPanel.TrustedBrowser").ToTimeLimitedDataProtector();
    }

    /// <summary>Sets the trust cookie on a response.</summary>
    /// <param name="context">The request.</param>
    /// <param name="value">The value from <see cref="Issue"/>.</param>
    public static void SetCookie(HttpContext context, string value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Cookies.Append(CookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/",
            Expires = DateTimeOffset.UtcNow.AddDays(TrustDays),
        });
    }

    /// <summary>Makes the trust of a browser for a user.</summary>
    /// <param name="user">The user, who just gave the right code.</param>
    /// <returns>The cookie's value.</returns>
    public string Issue(AdminUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return this._protector.Protect($"{user.Id}|{user.SecurityStamp}", TimeSpan.FromDays(TrustDays));
    }

    /// <summary>Whether a browser's cookie trusts it for a user.</summary>
    /// <param name="value">The cookie's value, if any.</param>
    /// <param name="user">The user signing in.</param>
    /// <returns><c>true</c>, if the code needn't be asked.</returns>
    public bool Trusts(string? value, AdminUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            return this._protector.Unprotect(value) == $"{user.Id}|{user.SecurityStamp}";
        }
        catch (CryptographicException)
        {
            // Changed, expired or made with keys this computer no longer has.
            return false;
        }
    }
}
