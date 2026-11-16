// Read-only game data for the public website. No API key or account details cross this boundary.
internal static class Website
{
    public static void Map(WebApplication app, PortalOptions options, IHttpClientFactory clients)
    {
        Forward(app, "/website/data", "api/website", options, clients);
        Forward(app, "/website/details", "api/website/details", options, clients);
        Forward(app, "/website/missions", "api/website/missions", options, clients);
        Forward(app, "/website/world-bosses", "api/website/world-bosses", options, clients);
        Forward(app, "/website/onboarding", "api/website/onboarding", options, clients);
        Forward(app, "/website/guilds", "api/website/guilds", options, clients);
        Forward(app, "/website/community", "api/website/community", options, clients);
        Forward(app, "/website/top-races", "api/website/top-races", options, clients);
        Forward(app, "/website/gift-codes", "api/website/gift-codes", options, clients);
        Forward(app, "/website/resets", "api/website/resets", options, clients);
        Forward(app, "/website/season-plan", "api/website/season-plan", options, clients);
    }

    private static void Forward(WebApplication app, string route, string apiPath, PortalOptions options, IHttpClientFactory clients)
    {
        app.MapGet(route, async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                var source = new Uri(new Uri(options.ApiBaseUrl), apiPath);
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
    }
}
