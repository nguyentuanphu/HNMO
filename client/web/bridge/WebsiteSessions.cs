using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Short-lived, bounded website sessions. Credentials never leave bridge memory through this API.</summary>
internal sealed class WebsiteSessions(TimeProvider clock, int capacity = 10000)
{
    internal const string CookieName = "hnmo.website.session";
    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _entries = new(StringComparer.Ordinal);

    /// <summary>Creates a new random session after the caller has verified the account with the server.</summary>
    internal string? Create(string loginName, string password)
    {
        lock (this._gate)
        {
            var now = clock.GetUtcNow();
            foreach (var key in this._entries.Where(pair => pair.Value.IdleUntil <= now || pair.Value.Until <= now).Select(pair => pair.Key).ToArray()) this._entries.Remove(key);
            if (this._entries.Count >= capacity) return null;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            this._entries.Add(Hash(token), new Session(loginName, password, now.AddHours(8), now.AddHours(1)));
            return token;
        }
    }

    /// <summary>Resolves and refreshes a valid session, without extending its absolute lifetime.</summary>
    internal bool TryGet(string? token, [NotNullWhen(true)] out Session? session)
    {
        session = null;
        if (token is not { Length: 64 } || !token.All(Uri.IsHexDigit)) return false;
        lock (this._gate)
        {
            var key = Hash(token);
            var now = clock.GetUtcNow();
            if (!this._entries.TryGetValue(key, out var found)) return false;
            if (found.IdleUntil <= now || found.Until <= now) { this._entries.Remove(key); return false; }
            found.IdleUntil = now.AddHours(1);
            session = found;
            return true;
        }
    }

    /// <summary>Revokes an opaque session.</summary>
    internal void Remove(string? token)
    {
        if (token is not { Length: 64 }) return;
        lock (this._gate) this._entries.Remove(Hash(token));
    }

    /// <summary>Only same-origin JSON requests explicitly opting into cookie authentication may mutate account data.</summary>
    internal static bool IsSameOrigin(HttpContext context)
    {
        if (context.Request.Headers["X-HNMO-Session"] != "1") return false;
        var site = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (site.Length > 0 && site != "same-origin" && site != "none") return false;
        var origin = context.Request.Headers.Origin.ToString();
        return origin.Length == 0 || (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && string.Equals(uri.GetLeftPart(UriPartial.Authority), $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsAccountPath(PathString path) => path.StartsWithSegments("/portal/account") || path.StartsWithSegments("/portal/gift-box")
        || path.StartsWithSegments("/portal/gift-codes") || path.StartsWithSegments("/portal/resets") || path.StartsWithSegments("/portal/safety")
        || path.StartsWithSegments("/portal/topup-milestones") || path.StartsWithSegments("/portal/market") || path.StartsWithSegments("/portal/jewel-bank")
        || path.StartsWithSegments("/portal/lucky-number") || path.StartsWithSegments("/portal/escrow") || path == "/portal/login-ticket";

    /// <summary>Adapts existing password-verified handlers; the server still rechecks password, bans and ownership on every operation.</summary>
    internal async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (!IsAccountPath(context.Request.Path) || context.Request.Headers["X-HNMO-Session"] != "1") { await next().ConfigureAwait(false); return; }
        context.Response.Headers.CacheControl = "no-store";
        if (!IsSameOrigin(context)) { context.Response.StatusCode = 403; return; }
        var token = context.Request.Cookies[CookieName];
        if (!this.TryGet(token, out var session)) { context.Response.StatusCode = 401; return; }
        // Parse a bounded body before adding the account-bound identity. Caller fields cannot select another account.
        const int MaximumBodyLength = 65536;
        if (context.Request.ContentLength > MaximumBodyLength) { context.Response.StatusCode = 413; return; }
        var original = context.Request.Body;
        var originalLength = context.Request.ContentLength;
        using var bounded = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await original.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) > 0)
        {
            if (bounded.Length + read > MaximumBodyLength) { context.Response.StatusCode = 413; return; }
            await bounded.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted).ConfigureAwait(false);
        }

        Dictionary<string, JsonElement>? body;
        try { body = bounded.Length == 0 ? [] : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bounded.ToArray()); }
        catch (JsonException) { context.Response.StatusCode = 400; return; }
        if (body is null) { context.Response.StatusCode = 400; return; }
        foreach (var key in body.Keys.Where(key => key.Equals("loginName", StringComparison.OrdinalIgnoreCase) || key.Equals("password", StringComparison.OrdinalIgnoreCase)).ToArray()) body.Remove(key);
        body["loginName"] = JsonSerializer.SerializeToElement(session.LoginName);
        body["password"] = JsonSerializer.SerializeToElement(session.Password);
        using var injected = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(body));
        context.Request.Body = injected;
        context.Request.ContentLength = injected.Length;
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == 401) { this.Remove(token); DeleteCookie(context); }
            return Task.CompletedTask;
        });
        try { await next().ConfigureAwait(false); }
        finally { context.Request.Body = original; context.Request.ContentLength = originalLength; }
    }

    internal static void SetCookie(HttpContext context, string token) => context.Response.Cookies.Append(CookieName, token,
        new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(8), IsEssential = true });

    internal static void DeleteCookie(HttpContext context) => context.Response.Cookies.Delete(CookieName,
        new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/" });

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    // Deliberately not a record: diagnostic ToString must never print its credential fields.
    internal sealed class Session(string loginName, string password, DateTimeOffset until, DateTimeOffset idleUntil)
    {
        internal string LoginName { get; } = loginName;
        internal string Password { get; } = password;
        internal DateTimeOffset Until { get; } = until;
        internal DateTimeOffset IdleUntil { get; set; } = idleUntil;
    }
}
