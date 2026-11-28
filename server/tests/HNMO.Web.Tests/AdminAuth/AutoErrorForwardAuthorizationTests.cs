// <copyright file="AutoErrorForwardAuthorizationTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.AdminAuth;

using System.IO;
using System.Reflection;
using System.Security.Claims;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Who may see and forward the automatic error reports: the Bug reports page and its forward action need the
/// Administrator policy, which an anonymous visitor and an operator don't meet once an admin user exists.
/// </summary>
[TestFixture]
public sealed class AutoErrorForwardAuthorizationTests
{
    /// <summary>The page and the forward action ask for the Administrator policy.</summary>
    [Test]
    public void BugReportsPageNeedsTheAdministratorPolicy()
    {
        var page = typeof(BugReports).GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();
        Assert.That(page, Does.Contain(AdminPolicies.Administrator), "besides the panel-wide [Authorize]");
        var forward = typeof(BugReports).GetMethod("ForwardAutoAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(forward, Is.Not.Null, "the forward action checks the policy again when it runs");
    }

    /// <summary>With an admin user on the host, only an administrator meets the policy.</summary>
    /// <param name="roles">The signed-in user's roles; null for an anonymous visitor.</param>
    /// <param name="expected">Whether the Administrator policy is met.</param>
    /// <returns>The task.</returns>
    [TestCase(null, false)]
    [TestCase("Viewer", false)]
    [TestCase("Operator", false)]
    [TestCase("Viewer,Operator,CashShop", false)]
    [TestCase("Administrator", true)]
    public async Task OnlyAnAdministratorMayForwardAsync(string? roles, bool expected)
    {
        var repository = new InMemoryAdminUserRepository();
        await repository.AddAsync(new AdminUser { LoginName = "owner", NormalizedLoginName = "OWNER", Roles = AdminRoles.Administrator }).ConfigureAwait(false);
        await using var services = Build(repository);
        var authorization = services.GetRequiredService<IAuthorizationService>();

        var principal = roles is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(roles.Split(',').Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.Name, "someone")), "test"));
        var result = await authorization.AuthorizeAsync(principal, AdminPolicies.Administrator).ConfigureAwait(false);
        Assert.That(result.Succeeded, Is.EqualTo(expected));
    }

    /// <summary>
    /// Before the first admin user exists the panel is in setup mode and lets everyone through (it listens on
    /// 127.0.0.1 by default). This test documents it: forwarding is then open to whoever reaches the panel.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WithoutAnyAdminUserTheSetupModeLetsAnonymousThroughAsync()
    {
        await using var services = Build(new InMemoryAdminUserRepository());
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), AdminPolicies.Administrator).ConfigureAwait(false);
        Assert.That(result.Succeeded, Is.True);
    }

    private static ServiceProvider Build(InMemoryAdminUserRepository repository)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AdminPanel:Auth:DataProtectionKeyPath"] = Path.Combine(Path.GetTempPath(), $"hnmo-keys-{Guid.NewGuid():N}") })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IAdminUserRepository>(repository);
        services.AddAdminPanelAuth(configuration);
        return services.BuildServiceProvider();
    }
}
