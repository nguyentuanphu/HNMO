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
using HNMO.Network;
using HNMO.Network.SimpleModulus;
using HNMO.Network.Xor;
using Pipelines.Sockets.Unofficial;

var builder = WebApplication.CreateBuilder(args);
var launcherOptions = builder.Configuration.GetSection("Launcher").Get<LauncherOptions>() ?? new LauncherOptions();
builder.Services.AddHostedService(services => new GameLauncher(launcherOptions, builder.Environment.ContentRootPath, services.GetRequiredService<ILogger<GameLauncher>>()));
builder.Host.ConfigureHostOptions(host => host.ShutdownTimeout = TimeSpan.FromSeconds(30));
var portalOptions = builder.Configuration.GetSection("Portal").Get<PortalOptions>() ?? new PortalOptions();
builder.Services.AddHttpClient();
var app = builder.Build();
var contentRoot = app.Environment.ContentRootPath;
var webRoot = Path.GetFullPath(Path.Combine(contentRoot, app.Configuration["HNMO:WebRoot"] ?? "../public"));
var gameFiles = new GameFiles(Path.Combine(contentRoot, app.Configuration["HNMO:DataPath"] ?? "../../src/bin/Data"));
var vendorRoot = Path.GetFullPath(Path.Combine(contentRoot, app.Configuration["HNMO:VendorPath"] ?? "../node_modules"));
var allowedHosts = app.Configuration.GetSection("Relay:AllowedHosts").Get<string[]>() ?? ["127.0.0.1", "localhost"];
var allowLoopback = app.Configuration.GetValue("Relay:AllowLoopback", true);
var connectTimeout = TimeSpan.FromSeconds(app.Configuration.GetValue("Relay:ConnectTimeoutSeconds", 10));
const int MaximumPacketSize = 0xFFFF;

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

app.MapGet("/health", () => "ok");

// The game's name players see: set in the admin panel (the "Game name" plug-in), asked from its public
// endpoint and kept for 30 seconds; the default while the admin panel doesn't answer.
const string DefaultGameName = "HenryN Mu Online";
var gameNameSource = (app.Configuration["GameName:AdminUrl"] ?? app.Configuration["Launcher:AdminUrl"] ?? "http://127.0.0.1:8095").TrimEnd('/') + "/api/game-name";
var gameNameClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
var gameInfo = Tuple.Create(DefaultGameName, new Dictionary<string, string>(), DateTime.MinValue); // swapped whole
app.MapGet("/game-name.json", async (HttpContext context) =>
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

    context.Response.Headers.CacheControl = "no-cache";
    return Results.Json(new { name = gameInfo.Item1, links = gameInfo.Item2 });
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
app.MapPost("/topup/casso", (HttpContext context) => ForwardTopUpAsync(context, "casso"));
app.MapPost("/topup/sepay", (HttpContext context) => ForwardTopUpAsync(context, "sepay"));

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
app.MapGet("/data/{**path}", async (HttpContext context, string path) =>
{
    var local = Path.GetFullPath(Path.Combine(webData, path));
    if (local.StartsWith(webData + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(local))
    {
        context.Response.ContentType = contentTypes.TryGetContentType(local, out var type) ? type : "application/octet-stream";
        await context.Response.SendFileAsync(local, context.RequestAborted).ConfigureAwait(false);
        return;
    }

    await gameFiles.ServeAsync(context, path).ConfigureAwait(false);
});
Portal.Map(app, new Portal(portalOptions, contentRoot, app.Services.GetRequiredService<IHttpClientFactory>().CreateClient(), app.Services.GetRequiredService<ILogger<Portal>>()));
app.Logger.LogInformation("Web client: {WebRoot}. Game data: {DataRoot}.", webRoot, gameFiles.Root);

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

    var endPoint = await ResolveAllowedAsync(host, port).ConfigureAwait(false);
    if (endPoint is null)
    {
        app.Logger.LogWarning("Refused relay to {Host}:{Port}, it isn't an allowed host.", host, port);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
    await RelayAsync(socket, endPoint, encrypted, context.RequestAborted).ConfigureAwait(false);
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

async Task RelayAsync(WebSocket socket, IPEndPoint endPoint, bool encrypted, CancellationToken cancellationToken)
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
