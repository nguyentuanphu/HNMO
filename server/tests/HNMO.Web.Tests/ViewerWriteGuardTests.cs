// <copyright file="ViewerWriteGuardTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using Bunit;
using HNMO.Interfaces;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>Pages which any signed-in role can open must not let a viewer change the host's files.</summary>
[TestFixture]
public class ViewerWriteGuardTests
{
    /// <summary>A viewer who saves the crash watch policy is refused and nothing is written.</summary>
    [Test]
    public void ViewerCannotSaveCrashWatchPolicy()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hnmo-vw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            using var context = new BunitContext();
            var authorization = context.AddAuthorization();
            authorization.SetAuthorized("viewer");
            authorization.SetRoles(AdminRoles.Viewer);
            authorization.SetPolicies(AdminPolicies.Viewer);
            context.Services.AddScoped<AdminPolicyGuard>();
            var host = new Mock<IHostSettingsService>();
            host.SetupGet(h => h.SettingsFilePath).Returns(Path.Combine(dir, "hnmo-settings.json"));
            var toast = new Mock<IToastService>();
            context.Services.AddSingleton(host.Object);
            context.Services.AddSingleton(toast.Object);

            var cut = context.Render<CrashWatch>();
            cut.Find("button.btn-primary.btn-sm").Click();

            toast.Verify(t => t.ShowError(It.IsAny<string>()), Times.Once);
            toast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
            Assert.That(Directory.GetFiles(dir), Is.Empty);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
