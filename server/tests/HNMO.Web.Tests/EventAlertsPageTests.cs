// <copyright file="EventAlertsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.EventAlerts;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Configuration and website API tests for <see cref="EventAlerts"/> and <see cref="WebsiteEventAlertsController"/>.
/// </summary>
[TestFixture]
public class EventAlertsPageTests
{
    /// <summary>Verifies public controller never leaks secrets and supports subscribe/unsubscribe.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsConfigExposesPublicKeyWithoutSecretsAndManagesSubscriptionsAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"event-alerts-web-{Guid.NewGuid():N}.json");
        try
        {
            var persistence = new InMemoryPersistenceContextProvider();
            using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
            {
                var row = plugCtx.CreateNew<PlugInConfiguration>();
                row.TypeId = typeof(EventAlertsPlugIn).GUID;
                row.IsActive = false;
                await plugCtx.SaveChangesAsync().ConfigureAwait(false);
            }

            var controller = new WebsiteEventAlertsController(persistence)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            var offRes = await controller.GetAsync().ConfigureAwait(false) as OkObjectResult;
            Assert.That(offRes, Is.Not.Null);
            var offJson = JsonSerializer.Serialize(offRes!.Value);
            Assert.That(offJson, Does.Contain("\"off\":true"));

            var (pub, priv) = WebPushCrypto.GenerateVapidKeyPair();
            const string secretWebhook = "https://discord.com/api/webhooks/999/SECRET_TOKEN";
            using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
            {
                var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                    .First(r => r.TypeId == typeof(EventAlertsPlugIn).GUID);
                stored.IsActive = true;
                stored.CustomConfiguration = JsonSerializer.Serialize(new EventAlertsConfiguration
                {
                    Enabled = true,
                    FileName = tempFile,
                    LeadTimeMinutes = 7,
                    VapidPublicKey = pub,
                    VapidPrivateKey = priv,
                    DiscordWebhookUrl = secretWebhook,
                });
                await editCtx.SaveChangesAsync().ConfigureAwait(false);
            }

            var onRes = await controller.GetAsync().ConfigureAwait(false) as OkObjectResult;
            var onJson = JsonSerializer.Serialize(onRes!.Value);
            Assert.That(onJson, Does.Contain("\"off\":false"));
            Assert.That(onJson, Does.Contain(pub));
            Assert.That(onJson, Does.Not.Contain(priv));
            Assert.That(onJson, Does.Not.Contain("SECRET_TOKEN"));

            var (uaPub, _) = WebPushCrypto.GenerateVapidKeyPair();
            var subRes = await controller.SubscribeAsync(new WebsiteEventAlertsController.SubscribeAlertRequest(
                "https://push.example/sub-1",
                uaPub,
                "YWFhYWFhYWFhYWFhYWFhYQ",
                "vi",
                ["world_boss"])).ConfigureAwait(false) as OkObjectResult;
            Assert.That(subRes, Is.Not.Null);
            Assert.That(JsonSerializer.Serialize(subRes!.Value), Does.Contain("\"subscriberCount\":1"));

            var unsubRes = await controller.UnsubscribeAsync(
                new WebsiteEventAlertsController.UnsubscribeAlertRequest("https://push.example/sub-1"))
                .ConfigureAwait(false) as OkObjectResult;
            Assert.That(JsonSerializer.Serialize(unsubRes!.Value), Does.Contain("\"removed\":true"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
