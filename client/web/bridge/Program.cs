// HNMO.Web: everything the browser needs from one address.
//  - the web client (WebRoot) and the game's data files (/data, see GameFiles.cs);
//  - /relay, the relay between the browser and the game servers, described below.
//
// Relays game connections between a browser and the game server.
// Browsers can only open WebSockets, while the game servers speak MU's own packet
// protocol over TCP. The browser opens /relay?host=..&port=..&encrypted=0|1, and
// this program connects to that server like the desktop client's ClientLibrary does:
// it encrypts what the browser sends (SimpleModulus + Xor32 for game servers) and
// decrypts what the server sends. Every WebSocket message holds one whole packet.
//
// Only hosts in "AllowedHosts" (appsettings.json) can be reached, so the relay can't
// be used to connect anywhere else.

using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;
using HNMO.Interfaces;
using HNMO.Network;
using HNMO.Network.SimpleModulus;
using HNMO.Network.Xor;
using Pipelines.Sockets.Unofficial;

if (args.Length >= 3 && args[0] == "--pack-icons")
{
    DataPackArchive.BuildItemIconsZip(args[1], args[2]);
    return;
}

if (args.Length >= 3 && args[0] == "--pack")
{
    string? allowlist = null;
    for (var i = 3; i < args.Length - 1; i++)
    {
        if (args[i] == "--allowlist")
        {
            allowlist = args[i + 1];
        }
    }

    DataPackArchive.BuildPacks(args[1], args[2], allowlist);
    return;
}

var builder = WebApplication.CreateBuilder(args);
var launcherOptions = builder.Configuration.GetSection("Launcher").Get<LauncherOptions>() ?? new LauncherOptions();
if (launcherOptions.WebPort == 8080 && builder.Configuration["Urls"] is { } urlsCfg)
{
    var firstUrl = urlsCfg.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    if (firstUrl is not null && Uri.TryCreate(firstUrl.Replace("://*:", "://localhost:").Replace("://0.0.0.0:", "://localhost:"), UriKind.Absolute, out var webUri))
    {
        launcherOptions.WebPort = webUri.Port;
    }
}
var portalOptions = builder.Configuration.GetSection("Portal").Get<PortalOptions>() ?? new PortalOptions();

bool HasArg(string prefix) => args.Any(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

var bridgeRoot = builder.Environment.ContentRootPath;
var configuredSaveDir = Path.GetFullPath(Path.Combine(bridgeRoot, launcherOptions.SavePath));
var settingsFileEnv = Environment.GetEnvironmentVariable(HostSettingsStore.EnvironmentVariableName);
var settingsPath = !string.IsNullOrWhiteSpace(settingsFileEnv)
    ? Path.GetFullPath(settingsFileEnv)
    : Path.Combine(configuredSaveDir, HostSettingsStore.FileName);
HostSettings? hostSettings = null;
if (launcherOptions.Enabled || File.Exists(settingsPath))
{
    var legacyAppSettings = Path.Combine(bridgeRoot, "appsettings.json");
    var legacyConnectionXml = Path.GetFullPath(Path.Combine(bridgeRoot, launcherOptions.ServerPath, "ConnectionSettings.xml"));
    hostSettings = HostSettingsStore.LoadOrMigrate(settingsPath, legacyAppSettings, legacyConnectionXml);

    launcherOptions.SettingsFilePath = settingsPath;
    if (!HasArg("--Launcher:Enabled"))
    {
        launcherOptions.Enabled = hostSettings.LauncherEnabled;
    }

    launcherOptions.PostgresPath = hostSettings.PostgresPath;
    if (!HasArg("--Launcher:SavePath"))
    {
        launcherOptions.SavePath = hostSettings.SavePath;
    }

    launcherOptions.DatabasePort = hostSettings.DatabasePort;
    launcherOptions.DatabasePassword = hostSettings.DatabasePassword;
    launcherOptions.ServerPath = hostSettings.ServerPath;
    launcherOptions.ServerProgram = hostSettings.ServerProgram;
    launcherOptions.AdminUrl = $"http://127.0.0.1:{hostSettings.AdminPort}";
    launcherOptions.ConnectServerPort = hostSettings.ConnectServerPort;
    launcherOptions.SeasonNineEnabled = hostSettings.SeasonNineEnabled;
    launcherOptions.ArenaEnabled = hostSettings.ArenaEnabled;
    launcherOptions.OpenBrowserUrl = hostSettings.OpenBrowserUrl;
    portalOptions.ApiBaseUrl = launcherOptions.AdminUrl;

    var hasCliUrls = HasArg("--urls");
    var hasEnvUrls = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
    if (!hasCliUrls && !hasEnvUrls && !string.IsNullOrWhiteSpace(hostSettings.WebUrls))
    {
        builder.WebHost.UseUrls(hostSettings.WebUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    if (!HasArg("--Launcher:WebPort") && !hasCliUrls && !hasEnvUrls)
    {
        var firstHostUrl = hostSettings.WebUrls?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (firstHostUrl is not null && Uri.TryCreate(firstHostUrl.Replace("://*:", "://localhost:").Replace("://0.0.0.0:", "://localhost:"), UriKind.Absolute, out var hostWebUri))
        {
            launcherOptions.WebPort = hostWebUri.Port;
        }
    }
}

portalOptions.ConnectServerPort = launcherOptions.ConnectServerPort;
if (HasArg("--Launcher:AdminUrl") && !HasArg("--Portal:ApiBaseUrl"))
{
    portalOptions.ApiBaseUrl = launcherOptions.AdminUrl;
}

// What the console window used to show also goes to save/logs/launcher.log (the admin panel's "Server log").
try
{
    builder.Logging.AddProvider(new LauncherFileLoggerProvider(Path.Combine(Path.GetFullPath(Path.Combine(bridgeRoot, launcherOptions.SavePath)), "logs")));
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    // A read-only folder: the console (where there is one) still shows it.
}

builder.Services.AddSingleton(services => new GameLauncher(
    launcherOptions,
    builder.Environment.ContentRootPath,
    services.GetRequiredService<IHostApplicationLifetime>(),
    services.GetRequiredService<ILogger<GameLauncher>>()));
builder.Services.AddHostedService(services => services.GetRequiredService<GameLauncher>());
builder.Services.AddSingleton(services => new ControlMaintenanceService(services.GetRequiredService<GameLauncher>(), launcherOptions, builder.Environment.ContentRootPath, services.GetRequiredService<ILogger<ControlMaintenanceService>>()));
builder.Services.AddHostedService(services => services.GetRequiredService<ControlMaintenanceService>());
builder.Host.ConfigureHostOptions(host => host.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient();
builder.Services.AddHttpForwarder();
// Request rates per visitor address for the account and payment endpoints (VisitorLimits.cs covers the game connections).
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy("per-visitor", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress is { } address && !IPAddress.IsLoopback(address) ? address.ToString() : "local",
        key => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = key == "local" ? 1000 : 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});
var app = builder.Build();
var contentRoot = app.Environment.ContentRootPath;
var webRoot = Path.GetFullPath(Path.Combine(contentRoot, (HasArg("--HNMO:WebRoot") ? app.Configuration["HNMO:WebRoot"] : hostSettings?.WebRoot) ?? app.Configuration["HNMO:WebRoot"] ?? "../public"));
var dataPath = Path.Combine(contentRoot, (HasArg("--HNMO:DataPath") ? app.Configuration["HNMO:DataPath"] : hostSettings?.DataPath) ?? app.Configuration["HNMO:DataPath"] ?? "../../src/bin/Data");
var configuredS9Data = app.Configuration["HNMO:S9DataPath"];
var siblingS9Data = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dataPath)) ?? contentRoot, "Data_S9");
var s9DataPath = !string.IsNullOrWhiteSpace(configuredS9Data)
    ? Path.Combine(contentRoot, configuredS9Data)
    : siblingS9Data;
// Each season has its own data root: /data/... is the base game (Season 6.3) for everyone, /data/s9/... is
// the Season 9 pack first and the base game for whatever the pack doesn't have. The web game asks under
// the root of the world the player picked, so a Season 9 pack never changes what a Season 6 world shows.
var gameFiles = new GameFiles(dataPath, app.Logger);
app.Lifetime.ApplicationStopping.Register(gameFiles.Dispose);
var s9Root = Path.GetFullPath(s9DataPath);
var s9Files = !string.Equals(s9Root, gameFiles.Root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(s9Root)
    ? new GameFiles(s9Root, app.Logger)
    : null;
if (s9Files is not null)
{
    app.Lifetime.ApplicationStopping.Register(s9Files.Dispose);
}
var vendorRoot = Path.GetFullPath(Path.Combine(contentRoot, (HasArg("--HNMO:VendorPath") ? app.Configuration["HNMO:VendorPath"] : hostSettings?.VendorPath) ?? app.Configuration["HNMO:VendorPath"] ?? "../node_modules"));
var allowedHosts = hostSettings?.RelayAllowedHosts ?? app.Configuration.GetSection("Relay:AllowedHosts").Get<string[]>() ?? ["127.0.0.1", "localhost"];
var allowLoopback = hostSettings?.RelayAllowLoopback ?? app.Configuration.GetValue("Relay:AllowLoopback", true);
var connectTimeout = TimeSpan.FromSeconds(hostSettings?.RelayConnectTimeoutSeconds ?? app.Configuration.GetValue("Relay:ConnectTimeoutSeconds", 10));
// Tells a game server on this computer the browser's address first (a "PROXY" line), for the login history.
var sendClientAddress = app.Configuration.GetValue("Relay:SendClientAddress", true);
const int MaximumPacketSize = 0xFFFF;

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseRateLimiter();

// Password guessing on the website, per account (audit 2.8): a website request names its account (loginName); after
// ten refused passwords in 15 minutes the account's requests are refused for 15 minutes, from every address.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (!HttpMethods.IsPost(context.Request.Method)
        || !(path.StartsWith("/portal/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/website/", StringComparison.OrdinalIgnoreCase))
        || context.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true
        || context.Request.ContentLength is > 65_536)
    {
        await next(context).ConfigureAwait(false);
        return;
    }

    context.Request.EnableBuffering();
    string? login = null;
    try
    {
        using var document = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted).ConfigureAwait(false);
        if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
            && document.RootElement.TryGetProperty("loginName", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            login = name.GetString();
        }
    }
    catch (System.Text.Json.JsonException)
    {
        // Not ours to judge: the endpoint answers it.
    }

    context.Request.Body.Position = 0;
    if (string.IsNullOrWhiteSpace(login))
    {
        await next(context).ConfigureAwait(false);
        return;
    }

    if (AccountGuessGuard.Shared.IsLocked(login))
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.Response.WriteAsJsonAsync(new { error = "locked", message = "Too many wrong passwords for this account. Try again in 15 minutes.", messageVi = "Nhập sai mật khẩu quá nhiều lần. Hãy thử lại sau 15 phút." }).ConfigureAwait(false);
        return;
    }

    await next(context).ConfigureAwait(false);
    if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        if (AccountGuessGuard.Shared.RecordFailure(login))
        {
            app.Logger.LogWarning("Website requests for an account are locked for 15 minutes after too many wrong passwords.");
        }
    }
    else if (context.Response.StatusCode is >= 200 and < 300)
    {
        AccountGuessGuard.Shared.RecordSuccess(login);
    }
});
var visitorLimits = new VisitorLimits(app.Configuration.GetSection("Limits").Get<VisitorLimitOptions>() ?? new VisitorLimitOptions());

// The admin panel at the website's secret admin path (AdminGateway.cs); before everything else.
AdminGateway.Use(app, new AdminGateway(
    portalOptions,
    app.Services.GetRequiredService<Yarp.ReverseProxy.Forwarder.IHttpForwarder>(),
    app.Services.GetRequiredService<IHttpClientFactory>(),
    app.Services.GetRequiredService<ILogger<AdminGateway>>(),
    () => launcherOptions.SeasonNineEnabled ? launcherOptions.SeasonNineAdminUrl : null,
    () => launcherOptions.ArenaEnabled ? launcherOptions.ArenaAdminUrl : null));

app.MapGet("/health", () => "ok");

// The website's "Download HNMO": the newest release's link as the HNMO developer published it on the hub (owner,
// 2026-11-21: no HNMO folder points at where HNMO is built); the home page while none is published.
app.MapGet("/download", async (HttpContext ctx, IHttpClientFactory clients) =>
{
    if (HNMO.Interfaces.DeveloperHubAddress.LatestRelease is { Length: > 0 } latestUrl)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await clients.CreateClient().GetAsync(latestUrl, timeout.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
                if (document.RootElement.TryGetProperty("html_url", out var link) && link.GetString() is { } url
                    && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    return Results.Redirect(uri.ToString());
                }
            }
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            // The hub can't be reached now: the home page.
        }
    }

    return Results.Redirect("/home.html");
});
app.MapGet("/", () => Results.Redirect("/home.html"));

// The game's name players see: set in the admin panel (the "Game name" plug-in), asked from its public
// endpoint and kept for 30 seconds; the default while the admin panel doesn't answer.
const string DefaultGameName = "HenryN Mu Online";
var gameNameSource = (app.Configuration["GameName:AdminUrl"] ?? app.Configuration["Launcher:AdminUrl"] ?? "http://127.0.0.1:8095").TrimEnd('/') + "/api/game-name";
var gameNameClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
var gameInfo = Tuple.Create(DefaultGameName, new Dictionary<string, string>(), DateTime.MinValue); // swapped whole
async Task RefreshGameInfoAsync(HttpContext context)
{
    if (DateTime.UtcNow >= gameInfo.Item3)
    {
        var (name, links) = (gameInfo.Item1, gameInfo.Item2);
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(await gameNameClient.GetStringAsync(gameNameSource, context.RequestAborted));
            var root = document.RootElement;
            if (root.TryGetProperty("name", out var value) && value.GetString() is { Length: > 0 } fetched) name = fetched.Trim();
            links = new Dictionary<string, string>();
            if (root.TryGetProperty("links", out var linkList) && linkList.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var link in linkList.EnumerateObject())
                {
                    // Only https links (the admin panel already checks; this keeps the page safe either way).
                    if (link.Value.GetString() is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps) links[link.Name] = uri.AbsoluteUri;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // The admin panel is still starting or not reachable: keep what's known.
        }

        gameInfo = Tuple.Create(name, links, DateTime.UtcNow.AddSeconds(30));
    }
}

app.MapGet("/game-name.json", async (HttpContext context) =>
{
    await RefreshGameInfoAsync(context).ConfigureAwait(false);
    context.Response.Headers.CacheControl = "no-cache";
    return Results.Json(new { name = gameInfo.Item1, links = gameInfo.Item2 });
});

// The web app manifest: the game can be put on a phone's home screen and opens full screen, landscape,
// with the game's name.
app.MapGet("/manifest.webmanifest", async (HttpContext context) =>
{
    await RefreshGameInfoAsync(context).ConfigureAwait(false);
    context.Response.Headers.CacheControl = "no-cache";
    var name = gameInfo.Item1;
    return Results.Json(
        new
        {
            name,
            short_name = name.Length <= 12 ? name : "HNMO",
            start_url = "/",
            display = "fullscreen",
            orientation = "landscape",
            background_color = "#11110e",
            theme_color = "#11110e",
            icons = new[]
            {
                new { src = "/images/icon-192.png", sizes = "192x192", type = "image/png" },
                new { src = "/images/icon-512.png", sizes = "512x512", type = "image/png" },
            },
        },
        contentType: "application/manifest+json");
});

// Top-ups by bank transfer: the top-up page's information and the payment services' webhooks (Casso,
// SePay) go to the admin panel, which only listens on this computer. Only these three paths pass, with
// the body and the webhooks' key headers; the admin panel checks the keys.
var topUpBase = (app.Configuration["GameName:AdminUrl"] ?? app.Configuration["Launcher:AdminUrl"] ?? "http://127.0.0.1:8095").TrimEnd('/') + "/api/topup/";
var topUpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
async Task ForwardTopUpAsync(HttpContext context, string action)
{
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), topUpBase + action);
    if (HttpMethods.IsPost(context.Request.Method))
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        if (body.Length > 64 * 1024)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        foreach (var header in new[] { "secure-token", "Authorization" })
        {
            if (context.Request.Headers.TryGetValue(header, out var value)) request.Headers.TryAddWithoutValidation(header, value.ToString());
        }
    }

    try
    {
        using var response = await topUpClient.SendAsync(request, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.WriteAsync(await response.Content.ReadAsStringAsync(context.RequestAborted), context.RequestAborted);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        // The admin panel isn't up: the services send the webhook again later.
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    }
}

app.MapGet("/topup/info", (HttpContext context) => ForwardTopUpAsync(context, "info"));
app.MapPost("/topup/casso", (HttpContext context) => ForwardTopUpAsync(context, "casso")).RequireRateLimiting("per-visitor");
app.MapPost("/topup/sepay", (HttpContext context) => ForwardTopUpAsync(context, "sepay")).RequireRateLimiting("per-visitor");

// The game page lets the performance report (Settings, "Performance report") sample the page's own
// scripts with the JS Self-Profiling API, which a page may only do when its response allows it.
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Document-Policy"] = "js-profiling";
            return Task.CompletedTask;
        });
    }

    await next(context).ConfigureAwait(false);
});

// The web client and its libraries, then the game's data files.
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) });
if (Directory.Exists(Path.Combine(vendorRoot, "three")))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        RequestPath = "/vendor/three",
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(vendorRoot, "three")),
    });
}

// The web client's own data (public/data: HNMO's skills list, their sounds) comes before the game's
// files of the same path; a matched endpoint skips the static files above, so it's looked up here.
var webData = Path.Combine(webRoot, "data");
var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
async Task<bool> TryServeWebDataAsync(HttpContext context, string path)
{
    var local = Path.GetFullPath(Path.Combine(webData, path));
    if (local.StartsWith(webData + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(local))
    {
        context.Response.ContentType = contentTypes.TryGetContentType(local, out var type) ? type : "application/octet-stream";
        await context.Response.SendFileAsync(local, context.RequestAborted).ConfigureAwait(false);
        return true;
    }

    return false;
}

// Which seasons' data this folder has: the website and the server list show a Season 9 world as playable
// only with its pack (without one it would show the base game's looks).
app.MapGet("/data/seasons.json", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-cache";
    // Each season's connect server: Season 9's program (when it runs) answers 100 ports above Season 6.3's, the Arena's 200.
    return Results.Json(new
    {
        s6 = true,
        s9 = s9Files is not null,
        connect = new
        {
            s6 = launcherOptions.ConnectServerPort,
            s9 = launcherOptions.SeasonNineEnabled ? launcherOptions.SeasonNineConnectServerPort : (int?)null,
            arena = launcherOptions.ArenaEnabled ? launcherOptions.ArenaConnectServerPort : (int?)null,
        },
    });
});
app.MapGet("/data/s9/{**path}", async (HttpContext context, string path) =>
{
    // The season's own file first; then HNMO's own web data and the base game, as for every world.
    if (s9Files is not null && SeasonData.IsSeasonFolder(path) && await s9Files.TryServeAsync(context, path).ConfigureAwait(false))
    {
        return;
    }

    if (await TryServeWebDataAsync(context, path).ConfigureAwait(false) || await gameFiles.TryServeAsync(context, path).ConfigureAwait(false))
    {
        return;
    }

    // A Season 9 object's texture found nowhere in its own folder: Season 9 objects that are items on display use the
    // item's texture from Item/ (SeasonData.ItemFolderTextureFor).
    if (SeasonData.ItemFolderTextureFor(path) is { } itemTexture
        && ((s9Files is not null && await s9Files.TryServeAsync(context, itemTexture).ConfigureAwait(false))
            || await gameFiles.TryServeAsync(context, itemTexture).ConfigureAwait(false)))
    {
        return;
    }

    await gameFiles.ServeAsync(context, path).ConfigureAwait(false);
});
app.MapGet("/data/{**path}", async (HttpContext context, string path) =>
{
    if (await TryServeWebDataAsync(context, path).ConfigureAwait(false) || await gameFiles.TryServeAsync(context, path).ConfigureAwait(false))
    {
        return;
    }

    // A map only Season 9 has, played by a Season 6.3 world: its files come from the Season 9 pack. Every file the
    // base game has was answered above, so Season 6.3 keeps its own looks.
    if (s9Files is not null && SeasonData.IsSeasonNineOnlyMapFile(path) && await s9Files.TryServeAsync(context, path).ConfigureAwait(false))
    {
        return;
    }

    await gameFiles.ServeAsync(context, path).ConfigureAwait(false);
});
Website.Map(app, portalOptions, app.Services.GetRequiredService<IHttpClientFactory>(), launcherOptions.SeasonNineEnabled ? launcherOptions.SeasonNineAdminUrl : null, launcherOptions.ArenaEnabled ? launcherOptions.ArenaAdminUrl : null);
Portal.Map(app, new Portal(portalOptions, contentRoot, app.Services.GetRequiredService<IHttpClientFactory>().CreateClient(), app.Services.GetRequiredService<ILogger<Portal>>()));
ControlEndpoints.Map(app, app.Services.GetRequiredService<GameLauncher>(), app.Services.GetRequiredService<ControlMaintenanceService>(), launcherOptions, contentRoot, webRoot, app.Services.GetRequiredService<IHttpClientFactory>().CreateClient(), app.Logger, portalOptions);
app.Logger.LogInformation(
    "Web client: {WebRoot}. Game data: {DataRoot} ({Mode}).",
    webRoot,
    gameFiles.Root,
    gameFiles.UsingPacks ? $"{gameFiles.PackCount} packs, {gameFiles.PackedEntryCount} entries" : "loose files");

app.Map("/relay", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var host = context.Request.Query["host"].ToString();
    var encrypted = context.Request.Query["encrypted"] == "1";
    if (!int.TryParse(context.Request.Query["port"], out var port) || port is <= 0 or > 65535)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    // Game servers only: never the database or the admin panels (audit 2.8).
    if (RelayPorts.IsRefused(port, launcherOptions.DatabasePort, new Uri(launcherOptions.AdminUrl).Port, launcherOptions.WebPort))
    {
        app.Logger.LogWarning("Refused relay to port {Port}: it isn't a game server.", port);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    var endPoint = await ResolveAllowedAsync(host, port).ConfigureAwait(false);
    if (endPoint is null)
    {
        app.Logger.LogWarning("Refused relay to {Host}:{Port}, it isn't an allowed host.", host, port);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    using var place = visitorLimits.TryOpen(context.Connection.RemoteIpAddress);
    if (place is null)
    {
        app.Logger.LogWarning("Refused a game connection from {Address}: too many connections from it.", context.Connection.RemoteIpAddress);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
    var browser = context.Connection.RemoteIpAddress is { } remoteAddress ? new IPEndPoint(remoteAddress, context.Connection.RemotePort) : null;
    await RelayAsync(socket, endPoint, encrypted, browser, context.RequestAborted).ConfigureAwait(false);
});

app.Run();

async Task<IPEndPoint?> ResolveAllowedAsync(string host, int port)
{
    if (string.IsNullOrWhiteSpace(host))
    {
        return null;
    }

    var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
    var allowedByName = allowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
    var address = addresses.FirstOrDefault(a => allowedByName || (allowLoopback && IPAddress.IsLoopback(a)) || allowedHosts.Contains(a.ToString()));
    return address is null ? null : new IPEndPoint(address, port);
}

async Task RelayAsync(WebSocket socket, IPEndPoint endPoint, bool encrypted, IPEndPoint? browser, CancellationToken cancellationToken)
{
    SocketConnection pipe;
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(connectTimeout);
        pipe = await SocketConnection.ConnectAsync(endPoint).WaitAsync(timeout.Token).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        app.Logger.LogInformation("Couldn't connect to {EndPoint}: {Message}", endPoint, ex.Message);
        await CloseQuietlyAsync(socket, WebSocketCloseStatus.EndpointUnavailable, "connect failed").ConfigureAwait(false);
        return;
    }

    if (sendClientAddress && browser is not null && IPAddress.IsLoopback(endPoint.Address))
    {
        try
        {
            await pipe.Output.WriteAsync(ProxyHeader.Line(browser, endPoint), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    var encryptor = encrypted ? new PipelinedXor32Encryptor(new PipelinedSimpleModulusEncryptor(pipe.Output, PipelinedSimpleModulusEncryptor.DefaultClientKey).Writer) : null;
    var decryptor = encrypted ? new PipelinedSimpleModulusDecryptor(pipe.Input, PipelinedSimpleModulusDecryptor.DefaultClientKey) : null;
    var connection = new Connection(pipe, decryptor, encryptor, NullLogger<Connection>.Instance);
    var sendLock = new SemaphoreSlim(1, 1);

    connection.PacketReceived += packet => SendToBrowserAsync(packet.ToArray());
    connection.Disconnected += () => CloseQuietlyAsync(socket, WebSocketCloseStatus.NormalClosure, "server closed");

    async ValueTask SendToBrowserAsync(byte[] bytes)
    {
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            sendLock.Release();
        }
    }

    var receiving = connection.BeginReceiveAsync();
    try
    {
        await ForwardBrowserPacketsAsync(socket, connection, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or InvalidOperationException)
    {
        app.Logger.LogDebug("Relay to {EndPoint} ended: {Message}", endPoint, ex.Message);
    }
    finally
    {
        await connection.DisconnectAsync().ConfigureAwait(false);
        await receiving.ConfigureAwait(false);
        await CloseQuietlyAsync(socket, WebSocketCloseStatus.NormalClosure, "relay closed").ConfigureAwait(false);
    }
}

async Task ForwardBrowserPacketsAsync(WebSocket socket, Connection connection, CancellationToken cancellationToken)
{
    var buffer = new byte[MaximumPacketSize];
    var budget = visitorLimits.NewBucket();
    while (socket.State == WebSocketState.Open && connection.Connected)
    {
        var length = 0;
        ValueWebSocketReceiveResult result;
        do
        {
            if (length >= buffer.Length)
            {
                throw new InvalidOperationException("Packet too large.");
            }

            result = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            length += result.Count;
        }
        while (!result.EndOfMessage);

        if (!IsWholePacket(buffer.AsSpan(0, length)))
        {
            throw new InvalidOperationException("The browser sent a malformed packet.");
        }

        // A flood of packets (far more than playing sends) ends the connection.
        if (!budget.TryTake())
        {
            app.Logger.LogWarning("Closed a game connection: it sent packets too fast (more than {Rate} a second).", visitorLimits.Options.PacketsPerSecond);
            throw new InvalidOperationException("The browser sent packets too fast.");
        }

        using (await connection.OutputLock.LockAsync().ConfigureAwait(false))
        {
            buffer.AsSpan(0, length).CopyTo(connection.Output.GetSpan(length));
            connection.Output.Advance(length);
            await connection.Output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

static bool IsWholePacket(ReadOnlySpan<byte> packet)
{
    if (packet.Length < 3)
    {
        return false;
    }

    return packet[0] switch
    {
        0xC1 or 0xC3 => packet[1] == packet.Length,
        0xC2 or 0xC4 => packet.Length >= 4 && ((packet[1] << 8) | packet[2]) == packet.Length,
        _ => false,
    };
}

static async ValueTask CloseQuietlyAsync(WebSocket socket, WebSocketCloseStatus status, string reason)
{
    try
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await socket.CloseAsync(status, reason, CancellationToken.None).ConfigureAwait(false);
        }
    }
    catch (WebSocketException)
    {
        // The browser is already gone.
    }
}
