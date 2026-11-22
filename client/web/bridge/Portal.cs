// The account portal: friends register an account (with free starting coins) and look
// up their cash shop coins, from a web page. HNMO.Web calls the game server's API
// (POST /api/accounts, cash shop grants) with an API key that stays on this computer,
// never in the browser.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

internal sealed class PortalOptions
{
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:8095";

    public string ApiKeyFile { get; set; } = "../save/api-key.txt";

    public string StartingCoinType { get; set; } = "WCoinC";

    public int StartingCoins { get; set; } = 1000;

    public int RegistrationsPerHourPerAddress { get; set; } = 5;

    public int ConnectServerPort { get; set; } = 44406;

    /// <summary>Google's token check; replaceable for tests.</summary>
    public string GoogleTokenInfoUrl { get; set; } = "https://oauth2.googleapis.com/tokeninfo";
}

internal sealed record GoogleSignInRequest(string Credential, string? LoginName, string? Password, string? NewPassword);

internal sealed record RegisterRequest(string LoginName, string Password, string? EMail);

internal sealed class Portal(PortalOptions options, string contentRoot, HttpClient http, ILogger<Portal> logger)
{
    private static readonly TimeSpan RateWindow = TimeSpan.FromHours(1);
    private readonly ConcurrentDictionary<IPAddress, List<DateTime>> _registrations = new();
    private readonly WebsiteSessions _sessions = new(TimeProvider.System);

    private string KeyFile => Path.GetFullPath(Path.Combine(contentRoot, options.ApiKeyFile));

    private string? ApiKey => File.Exists(this.KeyFile) ? File.ReadAllText(this.KeyFile).Trim() : null;

    public static void Map(WebApplication app, Portal portal)
    {
        // Handlers that take only the HttpContext are cast to Func<..., Task<IResult>>, so their answer is sent:
        // as a plain RequestDelegate the result would be dropped (ASP0016).
        app.Use((context, next) => portal._sessions.InvokeAsync(context, next));
        app.MapPost("/portal/session", (Func<HttpContext, Task<IResult>>)portal.SignInSessionAsync).RequireRateLimiting("per-visitor");
        app.MapGet("/portal/session", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { authenticated = portal._sessions.TryGet(context.Request.Cookies[WebsiteSessions.CookieName], out _) });
        }).RequireRateLimiting("per-visitor");
        app.MapDelete("/portal/session", (HttpContext context) =>
        {
            if (!WebsiteSessions.IsSameOrigin(context)) return Results.StatusCode(403);
            portal._sessions.Remove(context.Request.Cookies[WebsiteSessions.CookieName]);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { authenticated = false });
        }).RequireRateLimiting("per-visitor");
        app.MapGet("/portal/status", (Func<HttpContext, Task<IResult>>)portal.StatusAsync);
        app.MapPost("/portal/account/overview", (Func<HttpContext, Task<IResult>>)(context => portal.AccountAsync(context, "overview"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/password", (Func<HttpContext, Task<IResult>>)(context => portal.AccountAsync(context, "password"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/vip/state", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/vip/state", "Account VIP"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/vip/purchase", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/vip/purchase", "Account VIP"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/vip/daily", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/vip/daily", "Account VIP"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/email/state", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/email/state", "Account email"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/email/request", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/email/request", "Account email"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/email/confirm", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/email/confirm", "Account email"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/missions/state", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/missions/state", "Account missions"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/account/missions/claim", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/missions/claim", "Account missions"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/gift-box/state", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/gift-box/state", "Gift box"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/gift-box/collect", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardSignedJsonAsync(context, "api/website/account/gift-box/collect", "Gift box"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/register", portal.RegisterWithoutAddressAsync).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/setup", (Func<HttpContext, Task<IResult>>)portal.SetupAsync);
        app.MapGet("/website/settings", (Func<HttpContext, Task<IResult>>)portal.WebsiteSettingsAsync);
        app.MapPost("/portal/login-ticket", (Func<HttpContext, Task<IResult>>)portal.LoginTicketAsync).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/google", portal.GoogleAsync).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/google-ticket", (Func<HttpContext, Task<IResult>>)portal.GoogleTicketAsync).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/gift-codes/characters", (Func<HttpContext, Task<IResult>>)(context => portal.GiftCodesAsync(context, "characters"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/gift-codes/redeem", (Func<HttpContext, Task<IResult>>)(context => portal.GiftCodesAsync(context, "redeem"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/resets/account", (Func<HttpContext, Task<IResult>>)(context => portal.ResetsAsync(context, "account"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/resets/action", (Func<HttpContext, Task<IResult>>)(context => portal.ResetsAsync(context, "action"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/safety/account", (Func<HttpContext, Task<IResult>>)(context => portal.SafetyAsync(context, "account"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/safety/pin", (Func<HttpContext, Task<IResult>>)(context => portal.SafetyAsync(context, "pin"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/safety/item", (Func<HttpContext, Task<IResult>>)(context => portal.SafetyAsync(context, "item"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/topup-milestones/account", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardTopUpMilestonesAsync(context, "account"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/topup-milestones/claim", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardTopUpMilestonesAsync(context, "claim"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/lucky-number/account", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardLuckyNumberAsync(context, "account"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/lucky-number/pick", (Func<HttpContext, Task<IResult>>)(context => portal.ForwardLuckyNumberAsync(context, "pick"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/market/account", (Func<HttpContext, Task<IResult>>)(context => portal.MarketAsync(context, "account"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/market/buy", (Func<HttpContext, Task<IResult>>)(context => portal.MarketAsync(context, "buy"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/market/cancel", (Func<HttpContext, Task<IResult>>)(context => portal.MarketAsync(context, "cancel"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/escrow/account", (Func<HttpContext, Task<IResult>>)(context => portal.EscrowAsync(context, "account"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/escrow/cancel", (Func<HttpContext, Task<IResult>>)(context => portal.EscrowAsync(context, "cancel"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/escrow/direct/receipt", (Func<HttpContext, Task<IResult>>)(context => portal.EscrowAsync(context, "direct/receipt"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/jewel-bank/account", (Func<HttpContext, Task<IResult>>)(context => portal.JewelBankAsync(context, "account"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/jewel-bank/withdraw", (Func<HttpContext, Task<IResult>>)(context => portal.JewelBankAsync(context, "withdraw"))).RequireRateLimiting("per-visitor");
        app.MapPost("/portal/jewel-bank/clear-master", (Func<HttpContext, Task<IResult>>)(context => portal.JewelBankAsync(context, "clear-master"))).RequireRateLimiting("per-visitor");
    }

    private string StartingCoinsText => options.StartingCoins > 0 ? $"{options.StartingCoins} {options.StartingCoinType}" : string.Empty;

    private async Task<IResult> AccountAsync(HttpContext context, string action)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) => body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.String } value
            && value.GetString() is { } text && text.Length <= maximum ? text : string.Empty;
        var page = body?.GetValueOrDefault("page") is { ValueKind: JsonValueKind.Number } number && number.TryGetInt32(out var requestedPage) ? requestedPage : 0;
        object payload = action == "password"
            ? new { loginName = Field("loginName", 10), password = Field("password", 20), newPassword = Field("newPassword", 20) }
            : new { loginName = Field("loginName", 10), password = Field("password", 20), page };
        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/website/account/{action}", payload), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest or HttpStatusCode.Conflict && text.StartsWith('{'))
            {
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    private async Task<IResult> SignInSessionAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!WebsiteSessions.IsSameOrigin(context)) return Results.StatusCode(403);
        if (context.Request.Cookies[WebsiteSessions.CookieName] is { } previous)
        {
            this._sessions.Remove(previous);
        }
        if (string.IsNullOrEmpty(this.ApiKey)) return Results.Json(new { error = "setup" }, statusCode: 503);
        RegisterRequest? request;
        try { request = await context.Request.ReadFromJsonAsync<RegisterRequest>(context.RequestAborted).ConfigureAwait(false); }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return Results.StatusCode(400); }
        if (request is null || request.LoginName is not { Length: > 0 and <= 10 } || request.Password is not { Length: > 0 and <= 20 }) return Results.StatusCode(400);
        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, "api/website/account/overview", new { request.LoginName, request.Password, page = 0 }), context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return Results.StatusCode(401);
            if (!response.IsSuccessStatusCode) return Results.Json(new { error = "server" }, statusCode: 502);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            using var parsed = JsonDocument.Parse(text);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object || !parsed.RootElement.TryGetProperty("loginName", out var identity)
                || !string.Equals(identity.GetString(), request.LoginName, StringComparison.OrdinalIgnoreCase)) return Results.StatusCode(502);
            var token = this._sessions.Create(request.LoginName, request.Password);
            if (token is null) return Results.StatusCode(503);
            this._sessions.Remove(context.Request.Cookies[WebsiteSessions.CookieName]);
            WebsiteSessions.SetCookie(context, token);
            return Results.Content(text, "application/json");
        }
        catch (Exception error) when (error is HttpRequestException or JsonException) { return Results.Json(new { error = "server" }, statusCode: 502); }
    }

    private HttpRequestMessage ApiRequest(HttpMethod method, string path, object? body = null, string? apiKey = null)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(options.ApiBaseUrl), path));
        request.Headers.Add("X-Api-Key", apiKey ?? this.ApiKey);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<IResult> StatusAsync(HttpContext context) =>
        Results.Json(new { ready = await this.IsKeyAcceptedAsync(context.RequestAborted).ConfigureAwait(false), startingCoins = this.StartingCoinsText });

    // Ready means the game server accepts the saved key, not just that one is saved.
    private async Task<bool> IsKeyAcceptedAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return false;
        }

        try
        {
            using var check = await http.SendAsync(this.ApiRequest(HttpMethod.Get, "api/status"), cancellationToken).ConfigureAwait(false);
            return check.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private bool TryCountRegistration(IPAddress address)
    {
        var now = DateTime.UtcNow;
        var times = this._registrations.GetOrAdd(address, _ => []);
        lock (times)
        {
            times.RemoveAll(time => now - time > RateWindow);
            if (times.Count >= options.RegistrationsPerHourPerAddress)
            {
                return false;
            }

            times.Add(now);
            return true;
        }
    }

    // The website's own form: an address typed there is not checked, and Google sign-in finds accounts by their
    // address, so whoever typed someone else's address could block or take their Google sign-in. An address is
    // only set through the account page's confirmation code, or by Google sign-in (which Google has verified).
    private Task<IResult> RegisterWithoutAddressAsync(HttpContext context, RegisterRequest request) =>
        this.RegisterAsync(context, request with { EMail = null });

    private async Task<IResult> RegisterAsync(HttpContext context, RegisterRequest request)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "Registration isn't set up yet. Ask the host to finish the setup (README.txt)." }, statusCode: 503);
        }

        var address = context.Connection.RemoteIpAddress ?? IPAddress.None;
        if (!this.TryCountRegistration(address))
        {
            return Results.Json(new { error = "Too many registrations from your address. Try again in an hour." }, statusCode: 429);
        }

        using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, "api/accounts", new { loginName = request.LoginName, password = request.Password, eMail = request.EMail }), context.RequestAborted).ConfigureAwait(false);
        switch (response.StatusCode)
        {
            case HttpStatusCode.Created:
                break;
            case HttpStatusCode.Conflict:
                return Results.Json(new { error = "That account name is taken." }, statusCode: 409);
            case HttpStatusCode.BadRequest:
                return Results.Json(new { error = "The account name needs 3 to 10 characters and the password 3 to 20 (letters, digits and symbols, no spaces in the name)." }, statusCode: 400);
            default:
                logger.LogWarning("Registration failed: {Status} {Body}", response.StatusCode, await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false));
                return Results.Json(new { error = "The game server didn't accept the registration. Ask the host to check the API key." }, statusCode: 502);
        }

        if (options.StartingCoins > 0)
        {
            var grant = new { coinType = options.StartingCoinType, amount = options.StartingCoins, reason = "Welcome coins", reference = $"welcome-{request.LoginName}" };
            using var granted = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/accounts/{Uri.EscapeDataString(request.LoginName)}/cash-shop/grants", grant), context.RequestAborted).ConfigureAwait(false);
            if (!granted.IsSuccessStatusCode)
            {
                logger.LogWarning("Welcome coins for {Account} failed: {Status}", request.LoginName, granted.StatusCode);
            }
        }

        logger.LogInformation("Registered account {Account} from {Address}.", request.LoginName, address);
        return Results.Json(new { created = true, coins = this.StartingCoinsText });
    }

    // The host pastes the API key here once; only from this computer.
    private async Task<IResult> SetupAsync(HttpContext context)
    {
        if (!HostOnly.IsHostRequest(context))
        {
            return Results.Json(new { error = "The setup only works on the host computer (http://localhost:8080/account.html)." }, statusCode: 403);
        }

        var body = await context.Request.ReadFromJsonAsync<Dictionary<string, string>>(context.RequestAborted).ConfigureAwait(false);
        var key = body?.GetValueOrDefault("apiKey")?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return Results.Json(new { error = "Paste the API key." }, statusCode: 400);
        }

        // Only a key the game server accepts replaces the saved one.
        using var check = await http.SendAsync(this.ApiRequest(HttpMethod.Get, "api/status", apiKey: key), context.RequestAborted).ConfigureAwait(false);
        if (!check.IsSuccessStatusCode)
        {
            return Results.Json(new { error = $"The game server refused this key ({(int)check.StatusCode}). Create a key with the role Administrator and paste it again." }, statusCode: 400);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(this.KeyFile)!);
        await File.WriteAllTextAsync(this.KeyFile, key, context.RequestAborted).ConfigureAwait(false);
        return Results.Json(new { saved = true });
    }

    // What the website's pages may know (the Google client id and whether website login is required); the admin path never leaves the server.
    private async Task<IResult> WebsiteSettingsAsync(HttpContext context)
    {
        var (clientId, requireWebsiteLogin, theme) = await this.PublicSettingsAsync(context.RequestAborted).ConfigureAwait(false);
        context.Response.Headers.CacheControl = "no-cache";
        return Results.Json(new
        {
            googleClientId = clientId ?? string.Empty,
            requireWebsiteLogin,
            theme,
            connectServerPort = options.ConnectServerPort > 0 ? options.ConnectServerPort : 44406,
        });
    }

    private async Task<(string? GoogleClientId, bool RequireWebsiteLogin, string Theme)> PublicSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(500));
            using var response = await http.GetAsync(new Uri(new Uri(options.ApiBaseUrl), "api/website/settings/public"), timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, false, "classic");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false));
            var root = document.RootElement;
            var googleClientId = root.TryGetProperty("googleClientId", out var value) && value.GetString() is { Length: > 0 } id ? id : null;
            var requireWebsiteLogin = root.TryGetProperty("requireWebsiteLogin", out var req) && req.ValueKind == JsonValueKind.True;
            var theme = root.TryGetProperty("theme", out var appearance) && appearance.ValueKind == JsonValueKind.String ? appearance.GetString() : null;
            return (googleClientId, requireWebsiteLogin, theme ?? "classic");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException || (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return (null, false, "classic");
        }
    }

    private Task<string?> GoogleClientIdAsync(CancellationToken cancellationToken) =>
        this.PublicSettingsAsync(cancellationToken).ContinueWith(t => t.Result.GoogleClientId, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    // Issues a short-lived single-use login ticket for a player signed in on the website.
    private async Task<IResult> LoginTicketAsync(HttpContext context)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.String } element && element.GetString() is { } value && value.Length <= maximum ? value : string.Empty;

        var request = new { loginName = Field("loginName", 10), password = Field("password", 20) };
        try
        {
            using var apiRequest = this.ApiRequest(HttpMethod.Post, "api/website/login-ticket", request);
            if (context.Connection.RemoteIpAddress?.ToString() is { Length: > 0 } clientIp)
            {
                apiRequest.Headers.TryAddWithoutValidation("X-HNMO-Client-Ip", clientIp);
            }

            if (Field("deviceId", 128) is { Length: > 0 } deviceId)
            {
                apiRequest.Headers.TryAddWithoutValidation("X-HNMO-Device-Id", deviceId);
            }

            using var response = await http.SendAsync(apiRequest, context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                var status = (text.Contains("\"error\":\"blocked\"", StringComparison.Ordinal) && response.StatusCode == HttpStatusCode.OK)
                    ? (int)HttpStatusCode.Forbidden
                    : (int)response.StatusCode;
                return Results.Content(text, "application/json", statusCode: status);
            }

            logger.LogWarning("Website login ticket failed: {Status}", response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    // Issues a single-use login ticket for a Google-signed-in account after verifying its Google ID token.
    private async Task<IResult> GoogleTicketAsync(HttpContext context)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        GoogleSignInRequest? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<GoogleSignInRequest>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        var clientId = await this.GoogleClientIdAsync(context.RequestAborted).ConfigureAwait(false);
        if (clientId is null)
        {
            return Results.Json(new { error = "google" }, statusCode: 503);
        }

        var email = body is not null ? await this.VerifyGoogleTokenAsync(body.Credential, clientId, context.RequestAborted).ConfigureAwait(false) : null;
        if (email is null)
        {
            return Results.Json(new { error = "login" }, statusCode: 401);
        }

        using var found = await http.SendAsync(this.ApiRequest(HttpMethod.Get, $"api/accounts/by-email?email={Uri.EscapeDataString(email)}"), context.RequestAborted).ConfigureAwait(false);
        if (!found.IsSuccessStatusCode)
        {
            return Results.Json(new { error = "login" }, statusCode: (int)found.StatusCode);
        }

        using var document = JsonDocument.Parse(await found.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false));
        var loginName = document.RootElement.GetProperty("loginName").GetString();
        if (string.IsNullOrEmpty(loginName))
        {
            return Results.Json(new { error = "login" }, statusCode: 404);
        }

        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, "api/website/login-ticket/verified", new { loginName }), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    // "Sign in with Google": Google's ID token proves the e-mail address. The game account with that
    // address is found (and may get a new game password), or created with a name and password the player
    // chooses. The game itself still logs in with the account name and password.
    private async Task<IResult> GoogleAsync(HttpContext context, GoogleSignInRequest request)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "Accounts aren't set up yet. Ask the host to finish the setup (README.txt)." }, statusCode: 503);
        }

        var clientId = await this.GoogleClientIdAsync(context.RequestAborted).ConfigureAwait(false);
        if (clientId is null)
        {
            return Results.Json(new { error = "Google sign-in isn't set up on this server." }, statusCode: 503);
        }

        var email = await this.VerifyGoogleTokenAsync(request.Credential, clientId, context.RequestAborted).ConfigureAwait(false);
        if (email is null)
        {
            return Results.Json(new { error = "Google didn't confirm the sign-in. Try again." }, statusCode: 401);
        }

        using var found = await http.SendAsync(this.ApiRequest(HttpMethod.Get, $"api/accounts/by-email?email={Uri.EscapeDataString(email)}"), context.RequestAborted).ConfigureAwait(false);
        if (found.StatusCode == HttpStatusCode.Conflict)
        {
            return Results.Json(new { error = "Several game accounts use this address. Ask the host." }, statusCode: 409);
        }

        if (found.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(await found.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false));
            var loginName = document.RootElement.GetProperty("loginName").GetString()!;
            var passwordChanged = false;
            if (!string.IsNullOrEmpty(request.NewPassword))
            {
                using var changed = await http.SendAsync(this.ApiRequest(HttpMethod.Put, $"api/accounts/{Uri.EscapeDataString(loginName)}/password", new { password = request.NewPassword, reason = "New game password through Google sign-in on the website" }), context.RequestAborted).ConfigureAwait(false);
                if (!changed.IsSuccessStatusCode)
                {
                    return Results.Json(new { error = "The password needs 3 to 20 letters, digits or symbols." }, statusCode: 400);
                }

                passwordChanged = true;
                logger.LogInformation("Account {Account} set a new password through Google sign-in.", loginName);
            }

            return Results.Json(new { loginName, email, passwordChanged });
        }

        if (found.StatusCode != HttpStatusCode.NotFound)
        {
            return Results.Json(new { error = "The game server didn't answer. Is it running?" }, statusCode: 502);
        }

        if (string.IsNullOrWhiteSpace(request.LoginName) || string.IsNullOrEmpty(request.Password))
        {
            // No account with this address yet: the page asks for a name and a password, then sends them.
            return Results.Json(new { needsAccount = true, email });
        }

        return await this.RegisterAsync(context, new RegisterRequest(request.LoginName.Trim(), request.Password, email)).ConfigureAwait(false);
    }

    // Asks Google whether the ID token is genuine, for this site, unexpired and for a verified address.
    private async Task<string?> VerifyGoogleTokenAsync(string credential, string clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credential) || credential.Length > 4096)
        {
            return null;
        }

        try
        {
            using var response = await http.GetAsync($"{options.GoogleTokenInfoUrl}?id_token={Uri.EscapeDataString(credential)}", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var root = document.RootElement;
            string Text(string name) => root.TryGetProperty(name, out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText() : string.Empty;
            var issuer = Text("iss");
            var expires = long.TryParse(Text("exp"), out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : DateTimeOffset.MinValue;
            if (Text("aud") != clientId
                || issuer is not ("accounts.google.com" or "https://accounts.google.com")
                || !string.Equals(Text("email_verified"), "true", StringComparison.OrdinalIgnoreCase)
                || expires < DateTimeOffset.UtcNow
                || Text("email") is not { Length: > 3 } email)
            {
                return null;
            }

            return email;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Google's token check failed: {Message}", error.Message);
            return null;
        }
    }

    // Gift codes on the account page: the account's name and password are checked by the game server (with the key
    // this computer keeps), which lists the characters or enters the code. Only the fields it needs are passed on.
    private async Task<IResult> GiftCodesAsync(HttpContext context, string action)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, string?>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, string?>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) => (body?.GetValueOrDefault(name) ?? string.Empty) is var value && value.Length <= maximum ? value : string.Empty;
        object request = action == "redeem"
            ? new { loginName = Field("loginName", 10), password = Field("password", 20), character = Field("character", 10), code = Field("code", 40), deviceId = Field("deviceId", 36) }
            : new { loginName = Field("loginName", 10), password = Field("password", 20) };
        try
        {
            using var proxyRequest = this.ApiRequest(HttpMethod.Post, $"api/website/gift-codes/{action}", request);
            // Replace, rather than copy, any browser-supplied forwarding headers.
            proxyRequest.Headers.TryAddWithoutValidation("X-HNMO-Client-Address", context.Connection.RemoteIpAddress?.ToString());
            using var response = await http.SendAsync(proxyRequest, context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("Gift codes on the website failed: {Status}", response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    // Resets on the account page: the account's name and password are checked by the game server (with the key this
    // computer keeps), which lists the characters with their resets or does a reset, relife, stat change or the delegated
    // reset switch (changes only while the account is offline). Only the fields it needs are passed on.
    private async Task<IResult> ResetsAsync(HttpContext context, string action)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.String } element && element.GetString() is { } value && value.Length <= maximum ? value : string.Empty;
        int Points(string name) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.Number } element && element.TryGetInt32(out var value) ? Math.Clamp(value, 0, 1_000_000) : 0;
        object request = action == "action"
            ? new
            {
                loginName = Field("loginName", 10), password = Field("password", 20), character = Field("character", 10), action = Field("action", 20),
                str = Points("str"), agi = Points("agi"), vit = Points("vit"), ene = Points("ene"), cmd = Points("cmd"),
            }
            : new { loginName = Field("loginName", 10), password = Field("password", 20) };
        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/website/resets/{action}", request), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("Resets on the website failed: {Status}", response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    // Account safety: the visitor's address goes with each call for the login history (the game server can't see it).
    private async Task<IResult> SafetyAsync(HttpContext context, string action)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.String } element && element.GetString() is { } value && value.Length <= maximum ? value : string.Empty;
        var address = context.Connection.RemoteIpAddress is { } remote ? (remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote).ToString() : string.Empty;
        object request = action switch
        {
            "pin" => new { loginName = Field("loginName", 10), password = Field("password", 20), oldPin = Field("oldPin", 4), newPin = Field("newPin", 4), address },
            "item" => new
            {
                loginName = Field("loginName", 10), password = Field("password", 20),
                itemId = Guid.TryParse(Field("itemId", 40), out var id) ? id : Guid.Empty,
                unlock = body?.GetValueOrDefault("unlock") is { ValueKind: JsonValueKind.True },
                pin = Field("pin", 4), address,
            },
            _ => new { loginName = Field("loginName", 10), password = Field("password", 20), address },
        };
        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/website/account-safety/{action}", request), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("Account safety on the website failed: {Status}", response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    private Task<IResult> ForwardTopUpMilestonesAsync(HttpContext context, string action) =>
        this.ForwardSignedJsonAsync(context, $"api/website/topup-milestones/{action}", "Top-up milestones");

    private async Task<IResult> ForwardSignedJsonAsync(HttpContext context, string apiPath, string label)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        JsonElement body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<JsonElement>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, apiPath, body), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("{Feature} on the website failed: {Status}", label, response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    private async Task<IResult> ForwardLuckyNumberAsync(HttpContext context, string action)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Str(string key, int max) =>
            body?.GetValueOrDefault(key) is { ValueKind: JsonValueKind.String } el && el.GetString() is { } s && s.Length <= max ? s : string.Empty;
        var number = body?.GetValueOrDefault("number") is { ValueKind: JsonValueKind.Number } numEl && numEl.TryGetInt32(out var n) ? n : 0;
        object payload = action == "pick"
            ? new { loginName = Str("loginName", 10), password = Str("password", 20), number }
            : new { loginName = Str("loginName", 10), password = Str("password", 20) };

        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/website/lucky-number/{action}", payload), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("Lucky number portal call {Action} failed: {Status}", action, response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    private async Task<IResult> MarketAsync(HttpContext context, string action)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.String } element && element.GetString() is { } value && value.Length <= maximum ? value : string.Empty;
        object request = action == "account"
            ? new { loginName = Field("loginName", 10), password = Field("password", 20) }
            : new { loginName = Field("loginName", 10), password = Field("password", 20), listingId = Field("listingId", 40) };
        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/website/market/{action}", request), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("Item market on the website failed: {Status}", response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    private async Task<IResult> EscrowAsync(HttpContext context, string action)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.String } element && element.GetString() is { } value && value.Length <= maximum ? value : string.Empty;
        object request = action switch
        {
            "account" => new { loginName = Field("loginName", 10), password = Field("password", 20) },
            "direct/receipt" => new
            {
                loginName = Field("loginName", 10),
                password = Field("password", 20),
                contractId = Field("contractId", 40),
                receiptMime = Field("receiptMime", 40),
                receiptBase64 = Field("receiptBase64", 800_000),
                note = Field("note", 200),
            },
            _ => new { loginName = Field("loginName", 10), password = Field("password", 20), contractId = Field("contractId", 40) },
        };
        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/website/escrow/{action}", request), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("Escrow website portal call {Action} failed: {Status}", action, response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }

    private async Task<IResult> JewelBankAsync(HttpContext context, string action)
    {
        if (string.IsNullOrEmpty(this.ApiKey))
        {
            return Results.Json(new { error = "setup" }, statusCode: 503);
        }

        Dictionary<string, JsonElement>? body;
        try
        {
            body = await context.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return Results.Json(new { error = "request" }, statusCode: 400);
        }

        string Field(string name, int maximum) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.String } element && element.GetString() is { } value && value.Length <= maximum ? value : string.Empty;
        int IntField(string name) =>
            body?.GetValueOrDefault(name) is { ValueKind: JsonValueKind.Number } element && element.TryGetInt32(out var val) ? val : 1;

        object request = action switch
        {
            "withdraw" => new { loginName = Field("loginName", 10), password = Field("password", 20), jewelKey = Field("jewelKey", 24), quantity = IntField("quantity") },
            "clear-master" => new { loginName = Field("loginName", 10), password = Field("password", 20), character = Field("character", 20) },
            _ => new { loginName = Field("loginName", 10), password = Field("password", 20) },
        };
        try
        {
            using var response = await http.SendAsync(this.ApiRequest(HttpMethod.Post, $"api/website/jewel-bank/{action}", request), context.RequestAborted).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest && text.StartsWith('{'))
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(text, "application/json", statusCode: (int)response.StatusCode);
            }

            logger.LogWarning("Jewel Bank on the website failed: {Status}", response.StatusCode);
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "server" }, statusCode: 502);
        }
    }
}
