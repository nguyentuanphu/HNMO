// <copyright file="RequestSizeLimitTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Reflection;
using HNMO.Web.AdminPanel;
using HNMO.Web.AdminPanel.API;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Anonymous endpoints which read a body have a small size limit, and a refused body answers with its status.</summary>
[TestFixture]
public class RequestSizeLimitTests
{
    /// <summary>The anonymous POST endpoints refuse bodies over a few KB.</summary>
    /// <param name="controller">The controller type.</param>
    /// <param name="action">The action name.</param>
    [TestCase(typeof(WebsiteBuildCalculatorController), "CalculateAsync")]
    [TestCase(typeof(WebsiteEventAlertsController), "SubscribeAsync")]
    [TestCase(typeof(WebsiteEventAlertsController), "UnsubscribeAsync")]
    public void AnonymousPostHasASizeLimit(Type controller, string action)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;
        var limit = method.GetCustomAttribute<RequestSizeLimitAttribute>();

        Assert.That(limit, Is.Not.Null);
    }

    /// <summary>A body which is too large answers 413, not 500.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RefusedBodyAnswersWithItsStatusAsync()
    {
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        app.UseBadRequestStatus();
        app.Run(_ => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));
        var context = new DefaultHttpContext();

        await app.Build()(context).ConfigureAwait(false);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
    }
}
