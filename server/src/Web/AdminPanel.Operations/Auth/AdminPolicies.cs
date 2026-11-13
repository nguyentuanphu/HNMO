// <copyright file="AdminPolicies.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Auth;

/// <summary>
/// The authorization policies of the admin panel.
/// </summary>
internal static class AdminPolicies
{
    /// <summary>The API-key-only policy of the MCP endpoint.</summary>
    internal const string McpAccess = "HNMO.McpAccess";

    /// <summary>
    /// Gets the policy which requires the viewer role.
    /// </summary>
    internal const string Viewer = "HNMO.Viewer";

    /// <summary>
    /// Gets the  policy which requires the operator role.
    /// </summary>
    internal const string Operator = "HNMO.Operator";

    /// <summary>
    /// Gets the policy which requires the administrator role.
    /// </summary>
    internal const string Administrator = "HNMO.Administrator";

    /// <summary>
    /// The policy which requires the <see cref="HNMO.Persistence.AdminAuth.AdminRoles.CashShop"/> role, to grant cash shop coins.
    /// </summary>
    internal const string CashShop = "HNMO.CashShop";

    /// <summary>Authenticated operational API callers; initial setup anonymity is not accepted.</summary>
    internal const string AuthenticatedOperator = "Admin.AuthenticatedOperator";

    /// <summary>Authenticated cash shop API callers.</summary>
    internal const string AuthenticatedCashShop = "Admin.AuthenticatedCashShop";

    /// <summary>
    /// The policy which needs an API key with the viewer role.
    /// The login cookie of the admin panel isn't accepted: the policy names the API key
    /// scheme itself, so an endpoint only names the policy.
    /// </summary>
    internal const string ApiKeyViewer = "HNMO.ApiKeyViewer";
}
