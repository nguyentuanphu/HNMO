// <copyright file="WebsiteStripeTestCheckout.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

internal sealed class WebsiteStripeTestCheckout(
    WebsiteTestTopUpStore store,
    WebsiteStripeTestSettings settings,
    WebsiteTestBankConfirmation confirmation,
    IHttpClientFactory clients)
{
    public bool Enabled => store.CanRun && settings.CanCheckout;

    public async Task<Uri?> CreateAsync(TestTopUpOrder order, CancellationToken cancellationToken)
    {
        if (!store.CanRun || order.Provider != "stripe" || settings.ReadCredentials() is not { } keys)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keys.ApiKey);
        request.Headers.Add("Idempotency-Key", "hnmo-test-order-" + order.Id.ToString("N"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["payment_method_types[0]"] = "card",
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = "vnd",
            ["line_items[0][price_data][unit_amount]"] = order.Package.PriceVnd.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = order.Package.EnglishName,
            ["metadata[hnmo_order]"] = order.Id.ToString("N"),
            ["payment_intent_data[metadata][hnmo_order]"] = order.Id.ToString("N"),
            ["success_url"] = keys.Origin + "/topup.html?stripe-test=returned",
            ["cancel_url"] = keys.Origin + "/topup.html?stripe-test=cancelled",
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var client = clients.CreateClient();
        using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
        var session = document.RootElement;
        var sessionId = StripeTestWebhook.String(session, "id");
        if (!IsTest(session) || !StripeTestWebhook.LooksLikeId(sessionId, "cs_test_")
            || !Uri.TryCreate(StripeTestWebhook.String(session, "url"), UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps || url.Host != "checkout.stripe.com" || !url.IsDefaultPort || url.UserInfo.Length > 0)
        {
            return null;
        }

        return store.BindStripeSession(order.Id, sessionId) ? url : null;
    }

    public async Task<int> HandleWebhookAsync(byte[] raw, string signature, CancellationToken cancellationToken)
    {
        if (!store.CanRun || settings.ReadCredentials(allowDisabled: true) is not { } keys)
        {
            return 404;
        }

        if (!StripeTestWebhook.Verify(raw, signature, keys.WebhookKey, DateTimeOffset.UtcNow))
        {
            return 400;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return 400;
        }

        using (document)
        {
            var envelope = document.RootElement;
            if (!IsTest(envelope) || !StripeTestWebhook.LooksLikeId(StripeTestWebhook.String(envelope, "id"), "evt_")
                || !envelope.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("object", out var payment) || payment.ValueKind != JsonValueKind.Object)
            {
                return 200;
            }

            var kind = StripeTestWebhook.String(envelope, "type");
            var intent = StripeTestWebhook.String(payment, "payment_intent");
            if (!StripeTestWebhook.LooksLikeId(intent, "pi_"))
            {
                return 200;
            }

            if (kind is "charge.refunded" or "charge.dispute.created")
            {
                await confirmation.FlagStripeAsync(intent, kind, cancellationToken).ConfigureAwait(false);
                return 200;
            }

            if (kind is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded")
                || StripeTestWebhook.String(payment, "payment_status") != "paid"
                || StripeTestWebhook.String(payment, "currency") != "vnd"
                || !payment.TryGetProperty("amount_total", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetInt32(out var total)
                || !payment.TryGetProperty("metadata", out var metadata)
                || !Guid.TryParseExact(StripeTestWebhook.String(metadata, "hnmo_order"), "N", out var orderId)
                || store.Find(orderId) is not { Provider: "stripe" } order || total != order.Package.PriceVnd)
            {
                return 200;
            }

            if (order.StripeSessionId is null)
            {
                return 503; // Checkout creation has not yet committed the session ID.
            }

            return order.StripeSessionId == StripeTestWebhook.String(payment, "id")
                && !await confirmation.SettleStripeAsync(orderId, intent, cancellationToken).ConfigureAwait(false) ? 503 : 200;
        }
    }

    private static bool IsTest(JsonElement element) => element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("livemode", out var live) && live.ValueKind == JsonValueKind.False;
}
