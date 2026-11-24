// Read-only game data for the public website. No API key or account details cross this boundary.
internal static class Website
{
    public static void Map(WebApplication app, PortalOptions options, IHttpClientFactory clients, string? seasonNineApiBaseUrl = null, string? arenaApiBaseUrl = null)
    {
        // The HNMO Arena's leaderboards come from the Arena's own admin panel.
        if (!string.IsNullOrWhiteSpace(arenaApiBaseUrl))
        {
            Forward(app, "/website/arena", "api/website/arena", options, clients, arenaApiBaseUrl);
        }
        else
        {
            app.MapGet("/website/arena", () => Results.Json(new { enabled = false }));
        }

        // Each season's server program has its own market: Season 9's comes from its own admin panel.
        if (!string.IsNullOrWhiteSpace(seasonNineApiBaseUrl))
        {
            Forward(app, "/website/market/s9", "api/website/market", options, clients, seasonNineApiBaseUrl);
        }
        else
        {
            app.MapGet("/website/market/s9", () => Results.NotFound());
        }

        Forward(app, "/website/server-guide", "api/website/server-guide", options, clients, rateLimited: true);
        Forward(app, "/website/tracker", "api/website/tracker", options, clients, rateLimited: true);
        Forward(app, "/website/class-guide", "api/website/class-guide", options, clients, rateLimited: true, integerQueryKeys: ["serverId", "classId"]);
        Forward(app, "/website/data", "api/website", options, clients);
        Forward(app, "/website/details", "api/website/details", options, clients);
        Forward(app, "/website/missions", "api/website/missions", options, clients);
        Forward(app, "/website/timetable", "api/website/timetable", options, clients);
        Forward(app, "/website/event-guide", "api/website/event-guide", options, clients, rateLimited: true, integerQueryKeys: ["serverId"]);
        Forward(app, "/website/world-bosses", "api/website/world-bosses", options, clients);
        Forward(app, "/website/onboarding", "api/website/onboarding", options, clients);
        Forward(app, "/website/guilds", "api/website/guilds", options, clients);
        Forward(app, "/website/community", "api/website/community", options, clients);
        Forward(app, "/website/top-races", "api/website/top-races", options, clients);
        Forward(app, "/website/gift-codes", "api/website/gift-codes", options, clients);
        Forward(app, "/website/lucky-number", "api/website/lucky-number", options, clients);
        Forward(app, "/website/marriage", "api/website/marriage", options, clients);
        Forward(app, "/website/resets", "api/website/resets", options, clients);
        Forward(app, "/website/topup-milestones", "api/website/topup-milestones", options, clients);
        Forward(app, "/website/market", "api/website/market", options, clients);
        Forward(app, "/website/escrow", "api/website/escrow", options, clients);
        Forward(app, "/website/build-calculator", "api/website/build-calculator", options, clients);
        ForwardPost(app, "/website/build-calculator", "api/website/build-calculator", options, clients);
        app.MapPost("/website/escrow/direct/webhook/{contractId}", async (string contractId, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
                var target = new Uri(new Uri(options.ApiBaseUrl), $"api/website/escrow/direct/webhook/{Uri.EscapeDataString(contractId)}");
                using var req = new HttpRequestMessage(HttpMethod.Post, target)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                };
                foreach (var hdr in new[] { "X-HNMO-Signature", "X-HNMO-Timestamp", "X-HNMO-Nonce" })
                {
                    if (context.Request.Headers.TryGetValue(hdr, out var vals))
                    {
                        req.Headers.TryAddWithoutValidation(hdr, vals.ToArray());
                    }
                }

                using var response = await clients.CreateClient().SendAsync(req, timeout.Token).ConfigureAwait(false);
                var reply = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                return Results.Content(reply, "application/json", statusCode: (int)response.StatusCode);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });
        Forward(app, "/website/jewel-bank", "api/website/jewel-bank", options, clients);
        Forward(app, "/website/catchup", "api/website/catchup", options, clients);
        Forward(app, "/website/season-plan", "api/website/season-plan", options, clients);
        Forward(app, "/website/leftovers", "api/website/leftovers", options, clients);
        Forward(app, "/website/mounts", "api/website/mounts", options, clients);
        Forward(app, "/website/party-spots", "api/website/party-spots", options, clients);
        Forward(app, "/website/event-alerts", "api/website/event-alerts", options, clients);
        ForwardPost(app, "/website/event-alerts/subscribe", "api/website/event-alerts/subscribe", options, clients);
        ForwardPost(app, "/website/event-alerts/unsubscribe", "api/website/event-alerts/unsubscribe", options, clients);
        Forward(app, "/website/news", "api/website/news", options, clients);
        Forward(app, "/website/items", "api/website/items", options, clients);
    }

    private static void ForwardPost(WebApplication app, string route, string apiPath, PortalOptions options, IHttpClientFactory clients)
    {
        app.MapPost(route, async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
                var target = new Uri(new Uri(options.ApiBaseUrl), apiPath);
                using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                using var response = await clients.CreateClient().PostAsync(target, content, timeout.Token).ConfigureAwait(false);
                var reply = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                return Results.Content(reply, "application/json", statusCode: (int)response.StatusCode);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });
    }

    private static void Forward(WebApplication app, string route, string apiPath, PortalOptions options, IHttpClientFactory clients, string? apiBaseUrl = null, bool rateLimited = false, string[]? integerQueryKeys = null)
    {
        var endpoint = app.MapGet(route, async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                var query = QueryString.Empty;
                foreach (var key in integerQueryKeys ?? [])
                {
                    if (context.Request.Query.TryGetValue(key, out var input))
                    {
                        if (input.Count != 1 || !int.TryParse(input[0], out var value))
                        {
                            return Results.BadRequest();
                        }

                        query = query.Add(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                }

                var source = new Uri(new Uri(apiBaseUrl ?? options.ApiBaseUrl), apiPath + query);
                using var response = await clients.CreateClient().GetAsync(source, timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                return Results.Content(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false), "application/json");
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });
        if (rateLimited)
        {
            endpoint.RequireRateLimiting("per-visitor");
        }
    }
}
