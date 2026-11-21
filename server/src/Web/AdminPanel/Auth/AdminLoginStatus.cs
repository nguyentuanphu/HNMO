// <copyright file="AdminLoginStatus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Auth;

/// <summary>
/// The result status of a login attempt.
/// </summary>
public enum AdminLoginStatus
{
    /// <summary>
    /// The credentials were wrong or the user is not allowed to log in.
    /// </summary>
    Failed,

    /// <summary>
    /// The user is locked out because of too many failed attempts.
    /// </summary>
    LockedOut,

    /// <summary>
    /// The password was correct, but a second factor is required now.
    /// </summary>
    TwoFactorRequired,

    /// <summary>
    /// The login succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The password was correct, but the login comes from afar (through the website) and the user has no authenticator
    /// set up yet: it has to be set up on the host computer first (owner, 2026-11-21).
    /// </summary>
    TwoFactorSetupRequired,
}
