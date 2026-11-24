// <copyright file="WebsiteStripeTestCheckoutTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.DataProtection;
using Moq;

/// <summary>Checks signed, test-only delivery against the existing coin wallet and local order state.</summary>
[TestFixture]
public sealed class WebsiteStripeTestCheckoutTests
{
    private const string SigningKey = "whsec_" + "fictional";
    private const string TestKey = "sk_test_" + "fictional";
    private const string Origin = "https://example.test";

    /// <summary>Validates the exact body and five-minute tolerance.</summary>
    [Test]
    public void AcceptsOnlyTheOriginalFreshBody()
    {
        var now = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
        var raw = Encoding.UTF8.GetBytes("{\"livemode\":false}");
        var header = Sign(raw, now);
        Assert.That(StripeTestWebhook.Verify(raw, header, SigningKey, now), Is.True);
        Assert.That(StripeTestWebhook.Verify([.. raw, 32], header, SigningKey, now), Is.False);
        Assert.That(StripeTestWebhook.Verify(raw, header, SigningKey, now.AddSeconds(301)), Is.False);
    }

    /// <summary>Rejects live keys, protects secrets on disk, and handles duplicate and refunded payments.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task SignedTestPaymentMatchesPackageAndCreditsOnceAsync()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var state = HnmoStateStore.Open(Path.Combine(dir.FullName, HnmoStateStore.StateFileName))!;
            HnmoPlayerStateRowStoreRegistry.RegisterBackend(state.FilePath, new InMemoryHnmoPlayerStateRowBackend());
            var keys = new WebsiteStripeTestSettings(state, new EphemeralDataProtectionProvider());
            Assert.That(() => keys.Save(true, Origin, "sk_live_" + "fictional", SigningKey), Throws.ArgumentException);
            keys.Save(true, Origin, TestKey, SigningKey);
            var disk = await File.ReadAllTextAsync(state.FilePath).ConfigureAwait(false);
            Assert.That(disk, Does.Not.Contain(TestKey).And.Not.Contain(SigningKey));

            var package = new TestCoinPackage("test", "Fictional", "Hư cấu", 25_000, 10, CashShopCoinType.WCoinC);
            var store = new WebsiteTestTopUpStore(state, new AdminAuditSettings { IsDemo = true });
            store.SaveSettings(new TestTopUpSettings(false, 3, [package]));
            var database = new InMemoryPersistenceContextProvider();
            var game = new GameConfiguration();
            Guid id;
            using (var context = database.CreateNewPlayerContext(game))
            {
                var player = context.CreateNew<Account>();
                player.LoginName = "buyer";
                id = player.GetId();
                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            var datasource = new Mock<IDataSource<GameConfiguration>>();
            datasource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
            var wallet = new CashShopCoinService(database, datasource.Object);
            var confirmation = new WebsiteTestBankConfirmation(store, database, datasource.Object, wallet);
            var order = store.TryCreate(id, Guid.NewGuid(), package.Id, "stripe", stripeEnabled: true)!;

            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new CheckoutFixture(order.Id)));
            var service = new WebsiteStripeTestCheckout(store, keys, confirmation, factory.Object);
            Assert.That((await service.CreateAsync(order, CancellationToken.None).ConfigureAwait(false))?.Host, Is.EqualTo("checkout.stripe.com"));

            var now = DateTimeOffset.UtcNow;
            async Task<int> DeliverAsync(object envelope)
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(envelope);
                return await service.HandleWebhookAsync(payload, Sign(payload, now), CancellationToken.None).ConfigureAwait(false);
            }

            object Completed(string status, int amount, bool live = false) => new
            {
                id = "evt_Fictional",
                type = "checkout.session.completed",
                livemode = live,
                data = new { @object = new { id = "cs_test_Fictional", payment_intent = "pi_Fictional", payment_status = status, currency = "vnd",
                    amount_total = amount, metadata = new { hnmo_order = order.Id.ToString("N") } } },
            };
            Assert.That(await DeliverAsync(Completed("unpaid", package.PriceVnd)).ConfigureAwait(false), Is.EqualTo(200));
            Assert.That(await DeliverAsync(Completed("paid", package.PriceVnd - 1)).ConfigureAwait(false), Is.EqualTo(200));
            Assert.That(await DeliverAsync(Completed("paid", package.PriceVnd, true)).ConfigureAwait(false), Is.EqualTo(200));
            Assert.That((await wallet.GetSummaryAsync("buyer").ConfigureAwait(false))!.Grants, Is.Empty);
            var replay = await Task.WhenAll(DeliverAsync(Completed("paid", package.PriceVnd)), DeliverAsync(Completed("paid", package.PriceVnd))).ConfigureAwait(false);
            Assert.That(replay, Is.EqualTo(new[] { 200, 200 }));
            Assert.That((await wallet.GetSummaryAsync("buyer").ConfigureAwait(false))!.Grants.Select(g => g.Amount), Is.EqualTo(new[] { package.Coins }));
            Assert.That(await DeliverAsync(new
            {
                id = "evt_Refund",
                type = "charge.refunded",
                livemode = false,
                data = new { @object = new { payment_intent = "pi_Fictional" } },
            }).ConfigureAwait(false), Is.EqualTo(200));
            Assert.That(store.StripeFlags()["pi_Fictional"], Is.EqualTo("charge.refunded"));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    private static string Sign(byte[] raw, DateTimeOffset time)
    {
        var timestamp = time.ToUnixTimeSeconds();
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(SigningKey));
        hmac.AppendData(Encoding.ASCII.GetBytes($"{timestamp}."));
        hmac.AppendData(raw);
        return $"t={timestamp},v1={Convert.ToHexString(hmac.GetHashAndReset())}";
    }

    private sealed class CheckoutFixture(Guid order) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.That(request.RequestUri?.AbsoluteUri, Is.EqualTo("https://api.stripe.com/v1/checkout/sessions"));
            Assert.That(request.Headers.GetValues("Idempotency-Key").Single(), Is.EqualTo("hnmo-test-order-" + order.ToString("N")));
            var form = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Assert.That(form, Does.Contain("unit_amount%5D=25000"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"cs_test_Fictional\",\"url\":\"https://checkout.stripe.com/c/pay/cs_test_Fictional\",\"livemode\":false}"),
            };
        }
    }
}
