// Read-only game data for the public website. No API key or account details cross this boundary.
internal static class Website
{
    public static void Map(WebApplication app, PortalOptions options, IHttpClientFactory clients)
    {
        app.MapGet("/website/data", async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                var source = new Uri(new Uri(options.ApiBaseUrl), "api/website");
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
