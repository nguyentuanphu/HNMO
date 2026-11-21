using System.Net;

/// <summary>
/// Decides whether a request really comes from the person sitting at the host computer. The control page,
/// its API and the first-time setup can stop the servers, restore backups and change settings, so "the
/// connection comes from this computer" alone isn't enough:
/// a reverse proxy or tunnel on the same computer makes every visitor look local (it adds forwarding headers
/// and the public host name); a web page opened on the host can send requests to localhost (another origin);
/// and a page can point its own domain at 127.0.0.1 (the host name isn't a local one then).
/// </summary>
internal static class HostOnly
{
    private static readonly string[] ForwardingHeaders =
    [
        "X-Forwarded-For", "X-Forwarded-Host", "X-Forwarded-Proto", "Forwarded", "X-Real-IP", "CF-Connecting-IP", "True-Client-IP", "Via",
    ];

    public static bool IsHostRequest(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is { } remote && !IPAddress.IsLoopback(remote))
        {
            return false;
        }

        if (!IsLocalName(context.Request.Host.Host))
        {
            return false;
        }

        foreach (var header in ForwardingHeaders)
        {
            if (context.Request.Headers.ContainsKey(header))
            {
                return false;
            }
        }

        // Browsers say where a request was started from. Another site (or a page opened from a file) never
        // gets to use the host's pages; a request without these headers comes from a tool on the host itself.
        // (Another port of this computer, like the admin panel, may link to these pages, but not change anything.)
        var reads = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (context.Request.Headers.TryGetValue("Sec-Fetch-Site", out var site)
            && (site.ToString() == "cross-site" || (site.ToString() == "same-site" && !reads)))
        {
            return false;
        }

        if (context.Request.Headers.TryGetValue("Origin", out var origin) && origin.ToString() is { Length: > 0 } originText)
        {
            if (!Uri.TryCreate(originText, UriKind.Absolute, out var originUri)
                || !IsLocalName(originUri.Host)
                || originUri.Port != (context.Request.Host.Port ?? (context.Request.IsHttps ? 443 : 80)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLocalName(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
    }
}
