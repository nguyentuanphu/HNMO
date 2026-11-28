// <copyright file="CultureControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Components;

using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

/// <summary>
/// Tests the redirect used by the admin language selector.
/// </summary>
[TestFixture]
public class CultureControllerTests
{
    /// <summary>
    /// A missing or non-local return address is rejected before setting the preference cookie.
    /// </summary>
    /// <param name="redirectUri">The address supplied by the caller.</param>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("https://example.invalid/")]
    public void InvalidReturnAddressIsRejected(string? redirectUri)
    {
        var controller = CreateController();
        var response = controller.Set("vi", redirectUri);

        Assert.That(response, Is.TypeOf<BadRequestResult>());
        Assert.That(controller.Response.Headers.SetCookie, Is.Empty);
    }

    /// <summary>
    /// A valid local return address retains the language selection flow.
    /// </summary>
    [Test]
    public void LocalReturnAddressKeepsTheSelectedCulture()
    {
        var controller = CreateController();
        var response = controller.Set("vi", "/users");

        Assert.That(response, Is.TypeOf<LocalRedirectResult>().With.Property(nameof(LocalRedirectResult.Url)).EqualTo("/users"));
        Assert.That(controller.Response.Headers.SetCookie.ToString(), Does.Contain(".AspNetCore.Culture="));
    }

    private static CultureController CreateController()
    {
        var url = new Mock<IUrlHelper>();
        url.Setup(helper => helper.IsLocalUrl("/users")).Returns(true);
        return new CultureController
        {
            Url = url.Object,
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }
}
