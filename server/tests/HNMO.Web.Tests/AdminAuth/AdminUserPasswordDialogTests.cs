// <copyright file="AdminUserPasswordDialogTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.AdminAuth;

using Bunit;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Modal;
using HNMO.Web.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests the admin user password dialogs.
/// </summary>
[TestFixture]
public class AdminUserPasswordDialogTests
{
    /// <summary>
    /// Password fields mask typed characters in both admin user dialogs.
    /// </summary>
    /// <param name="changePassword">Whether to open the password change dialog.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task PasswordIsMaskedAsync(bool changePassword)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var modals = new ModalService();
        context.Services.AddSingleton(modals);
        context.Services.AddSingleton<IChangeNotificationService, ChangeNotificationService>();
        var container = context.Render<ModalContainer>();
        var service = new AdminUserManagementService(null!, null!, null!, null!, modals, null!, null!);

        Task<bool> dialog = null!;
        await container.InvokeAsync(() =>
        {
            dialog = changePassword
                ? service.ChangePasswordInModalDialogAsync(new AdminUser { LoginName = "dialog-test" })
                : service.CreateNewInModalDialogAsync();
        }).ConfigureAwait(false);

        container.WaitForAssertion(() => Assert.That(
            container.Find("input#Password").GetAttribute("type"), Is.EqualTo("password")));
        await container.Find(".btn-close").ClickAsync(new()).ConfigureAwait(false);
        Assert.That(await dialog.ConfigureAwait(false), Is.False);
    }
}
