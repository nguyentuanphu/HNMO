// <copyright file="WebsiteGiftCodeIdentityTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>Checks that the actual website redemption endpoint forwards identities into the shared rules.</summary>
[TestFixture]
[NonParallelizable]
public class WebsiteGiftCodeIdentityTests
{
    /// <summary>Different authenticated accounts cannot reuse a limited identity through the website.</summary>
    /// <param name="network">Whether to restrict networks rather than devices.</param>
    /// <param name="expected">The refusal sent to the browser.</param>
    /// <returns>The task.</returns>
    [TestCase(true, "AlreadyUsedNetwork")]
    [TestCase(false, "AlreadyUsedDevice")]
    public async Task WebsiteRedemptionsApplyIdentityLimitsAsync(bool network, string expected)
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-gift-website-identities-{Guid.NewGuid():N}.json");
        try
        {
            var provider = new InMemoryPersistenceContextProvider();
            GameConfiguration configuration;
            using (var setup = provider.CreateNewContext())
            {
                configuration = setup.CreateNew<GameConfiguration>();
                var plugin = setup.CreateNew<PlugInConfiguration>();
                plugin.TypeId = typeof(GiftCodesPlugIn).GUID;
                plugin.IsActive = true;
                plugin.CustomConfiguration = JsonSerializer.Serialize(new GiftCodesConfiguration { FileName = file });
                configuration.PlugInConfigurations.Add(plugin);
                await setup.SaveChangesAsync().ConfigureAwait(false);
            }

            using (var players = provider.CreateNewPlayerContext(configuration))
            {
                foreach (var login in new[] { "ann", "bob" })
                {
                    var account = players.CreateNew<Account>();
                    account.LoginName = login;
                    account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
                    var character = players.CreateNew<Character>();
                    character.Name = login;
                    account.Characters.Add(character);
                }

                await players.SaveChangesAsync().ConfigureAwait(false);
            }

            var store = GiftCodeStore.Open(file);
            lock (store.Lock)
            {
                store.Data.Codes.Add(new GiftCodeDefinition { Id = "gift", Code = "GIFT", OncePerNetwork = network, OncePerDevice = !network });
                store.CodesChanged();
            }

            var source = new Mock<IDataSource<GameConfiguration>>();
            source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(configuration);
            var controller = new WebsiteGiftCodesController(provider, source.Object, Mock.Of<ILogger<WebsiteGiftCodesController>>())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };
            controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
            controller.Request.Headers["X-HNMO-Client-Address"] = "203.0.113.7";
            var device = Guid.NewGuid().ToString();
            using var first = JsonDocument.Parse(((ContentResult)await controller.RedeemAsync(new GiftCodeRedeemRequest("ann", "secret", "ann", "GIFT", device)).ConfigureAwait(false)).Content!);
            Assert.That(first.RootElement.GetProperty("result").GetString(), Is.EqualTo("Ok"));
            if (!network)
            {
                controller.Request.Headers["X-HNMO-Client-Address"] = "203.0.113.8";
            }

            using var second = JsonDocument.Parse(((ContentResult)await controller.RedeemAsync(new GiftCodeRedeemRequest("bob", "secret", "bob", "GIFT", network ? Guid.NewGuid().ToString() : device)).ConfigureAwait(false)).Content!);
            Assert.That(second.RootElement.GetProperty("result").GetString(), Is.EqualTo(expected));
            Assert.That(store.Data.Uses["gift"], Is.EqualTo(1));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
