// The admin panel through the public website: https://site/<admin path>/… goes to the admin panel,
// which only listens on this computer. The path is set in the admin panel (plug-in "Website") and read
// every 30 seconds; the admin panel works under it (X-HNMO-Website-Prefix) and stays closed there while
// it has no admin user. Visitors can't send that header themselves: it's removed from every request.
// One path for all the panels (owner, 2026-11-21): <admin path>/s9/… is Season 9's panel and <admin path>/arena/… the
// Arena's, when those programs run; they share the admin users, so one sign-in opens all of them.
using System.Net;
using Yarp.ReverseProxy.Forwarder;

internal sealed class AdminGateway(PortalOptions options, IHttpForwarder forwarder, IHttpClientFactory clients, ILogger<AdminGateway> logger, Func<string?>? seasonNineUrl = null, Func<string?>? arenaUrl = null)
{
    public const string PrefixHeader = "X-HNMO-Website-Prefix";
    private const string DefaultPath = "/adminpanel";

    private readonly HttpMessageInvoker _invoker = new(new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
    });

    private readonly HostKeepingTransformer _transformer = new();

    /// <summary>Gets the gateway the web server uses (for the host's control page).</summary>
    public static AdminGateway? Current { get; private set; }

    // The current admin path ("/adminpanel"), or "" when the website's admin access is off.
    private volatile string _path = DefaultPath;
    private DateTime _nextRefresh = DateTime.MinValue;

    public static void Use(WebApplication app, AdminGateway gateway)
    {
        Current = gateway;
        app.Use(async (context, next) =>
        {
            context.Request.Headers.Remove(PrefixHeader);
            var path = await gateway.PathAsync(context.RequestAborted).ConfigureAwait(false);
            if (path.Length == 0 || !context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase, out var rest))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!rest.HasValue)
            {
                // "/adminpanel" → "/adminpanel/", so that the admin panel's relative links resolve under it.
                context.Response.Redirect(path + "/" + context.Request.QueryString);
                return;
            }

            foreach (var (segment, url) in new[] { ("/s9", gateway.SeasonNineUrl), ("/arena", gateway.ArenaUrl) })
            {
                if (!rest.StartsWithSegments(segment, StringComparison.OrdinalIgnoreCase, out var inner))
                {
                    continue;
                }

                if (url is null)
                {
                    break;
                }

                if (!inner.HasValue)
                {
                    context.Response.Redirect(path + segment + "/" + context.Request.QueryString);
                    return;
                }

                await gateway.ForwardAsync(context, path + segment, inner, url).ConfigureAwait(false);
                return;
            }

            await gateway.ForwardAsync(context, path, rest, gateway.MainUrl).ConfigureAwait(false);
        });
    }

    // Season 6.3's panel.
    private string MainUrl => options.ApiBaseUrl;

    // Season 9's panel, when it runs.
    private string? SeasonNineUrl => seasonNineUrl?.Invoke();

    // The Arena's panel, when it runs.
    private string? ArenaUrl => arenaUrl?.Invoke();

    private async Task ForwardAsync(HttpContext context, string path, PathString rest, string target)
    {
        context.Request.Path = rest;
        context.Request.Headers[PrefixHeader] = path;
        var error = await forwarder.SendAsync(context, target, this._invoker, ForwarderRequestConfig.Empty, this._transformer).ConfigureAwait(false);
        if (error != ForwarderError.None && !context.Response.HasStarted)
        {
            logger.LogWarning("The admin panel didn't answer through the website: {Error}", error);
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("The admin panel isn't running.", context.RequestAborted).ConfigureAwait(false);
        }
    }

    // The website's admin path ("/admin-..."), or "" when the website's admin access is off.
    public async Task<string> PathAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < this._nextRefresh)
        {
            return this._path;
        }

        this._nextRefresh = DateTime.UtcNow.AddSeconds(30);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await clients.CreateClient().GetAsync(new Uri(new Uri(options.ApiBaseUrl), "api/website/settings/host"), timeout.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
                var configured = document.RootElement.TryGetProperty("adminPath", out var value) ? value.GetString()?.Trim().Trim('/') ?? string.Empty : DefaultPath.Trim('/');
                this._path = configured.Length == 0 ? string.Empty : "/" + configured;
            }
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            // The admin panel is starting or stopped: keep the last known path (forwarding answers 502 meanwhile).
        }

        return this._path;
    }

    // Keeps the visitor's host name, so that the admin panel's redirects and links point back to the website.
    private sealed class HostKeepingTransformer : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken).ConfigureAwait(false);
            proxyRequest.Headers.Host = httpContext.Request.Host.Value;
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", httpContext.Connection.RemoteIpAddress?.ToString());
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", httpContext.Request.Scheme);
        }
    }
}
