// <copyright file="DeveloperHub.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

/// <summary>
/// The HNMO developer's hub (owner's choice, 2026-11-20): a server asks it for a licence (and fetches the licence the
/// developer issues with a one-time secret only it knows), and sends it a weekly report with the bug reports its owner
/// forwarded. A licensed server is known by its signed licence file and its machine id; no player login or e-mail is sent.
/// The weekly answer says whether the licence was withdrawn. Once, at the first setup, it sends the host's system and game,
/// and the hub keeps the IP it came from with it. All of it is sent silently: the host isn't told or reminded (owner,
/// 2026-11-21); the host's name and contact come from a licence request.
/// </summary>
public static class DeveloperHub
{
    /// <summary>The hub's address unless <c>HNMO_DEVELOPER_HUB</c> names another.</summary>
    public const string DefaultUrl = HNMO.Interfaces.DeveloperHubAddress.Default;

    /// <summary>The days between two weekly reports (the hub takes one in six days).</summary>
    public const int WeeklyDays = 7;

    /// <summary>The days more players than allowed may play once the limit was first passed (owner, 2026-11-20).</summary>
    public const int GraceDays = 7;

    private static readonly HttpClient DefaultClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Lock StateLock = new();
    private static int _peakPlayers;
    private static int _errors;
    private static DeveloperHubState? _memoryState;

    /// <summary>Gets the hub's address (empty: sending is off).</summary>
    public static string Url => HNMO.Interfaces.DeveloperHubAddress.Url;

    /// <summary>Gets or sets this folder's licence file as text (set at start and when a licence is saved), if any.</summary>
    public static string? LicenceFile { get; set; }

    /// <summary>Gets a value indicating whether this is one of the HNMO developer's test machines (<c>HNMO_TEST_HOST=1</c>).</summary>
    public static bool IsTestHost => Environment.GetEnvironmentVariable("HNMO_TEST_HOST") is "1" or "true";

    /// <summary>Gets or sets this computer's id (the one a licence is bound to).</summary>
    public static string MachineId { get; set; } = string.Empty;

    /// <summary>Gets or sets the id earlier HNMO versions used for this computer (the computer name's hash).</summary>
    public static string LegacyMachineId { get; set; } = string.Empty;

    /// <summary>Gets or sets the file check's result: ok, modified, unsigned or unknown.</summary>
    public static string Integrity { get; set; } = "unknown";

    /// <summary>Gets or sets the file the hub's state is kept in (<c>save/developer-hub.json</c>); none: in memory only.</summary>
    public static string? StateFile { get; set; }

    /// <summary>Gets the HNMO version.</summary>
    public static string Version =>
        typeof(DeveloperHub).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? typeof(DeveloperHub).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>Notes the players in the game now, for the most at once since the last weekly report.</summary>
    /// <param name="count">The players now.</param>
    public static void NotePlayers(int count)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref _peakPlayers);
            if (count <= seen)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _peakPlayers, count, seen) != seen);
    }

    /// <summary>Counts an error of the log (Error and Fatal), for the weekly report.</summary>
    public static void CountError() => Interlocked.Increment(ref _errors);

    /// <summary>Takes the errors counted since the last call.</summary>
    /// <returns>The count.</returns>
    public static int TakeErrors() => Interlocked.Exchange(ref _errors, 0);

    /// <summary>Takes the most players at once since the last call, starting again from the players now.</summary>
    /// <param name="now">The players now.</param>
    /// <returns>The most at once.</returns>
    public static int TakePeak(int now) => Math.Max(now, Interlocked.Exchange(ref _peakPlayers, now));

    /// <summary>Reads the saved state.</summary>
    /// <returns>The state.</returns>
    public static DeveloperHubState ReadState()
    {
        lock (StateLock)
        {
            if (StateFile is null)
            {
                // No file (tests, a server without a save folder): the state lives as long as the program.
                return _memoryState ??= new DeveloperHubState();
            }

            try
            {
                return File.Exists(StateFile)
                    ? JsonSerializer.Deserialize<DeveloperHubState>(File.ReadAllText(StateFile)) ?? new DeveloperHubState()
                    : new DeveloperHubState();
            }
            catch (JsonException)
            {
                return new DeveloperHubState();
            }
        }
    }

    /// <summary>Changes the saved state.</summary>
    /// <param name="change">The change.</param>
    /// <returns>The state after it.</returns>
    public static DeveloperHubState ChangeState(Action<DeveloperHubState> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (StateLock)
        {
            var state = ReadState();
            change(state);
            if (StateFile is { } file)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(state, JsonOptions));
                File.Move(file + ".tmp", file, overwrite: true);
            }

            return state;
        }
    }

    /// <summary>
    /// Whether one more player than allowed may come in now: the first time the limit is passed starts a week of grace
    /// (remembered in the state), during which more may play; after it, not.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, during the grace week.</returns>
    public static bool AllowOverLimit(DateTime nowUtc)
    {
        var started = ReadState().GraceStartedUtc;
        if (started is null)
        {
            ChangeState(s => s.GraceStartedUtc ??= nowUtc);
            return true;
        }

        return nowUtc < started.Value.AddDays(GraceDays);
    }

    /// <summary>Ends the grace (a valid licence was applied).</summary>
    public static void ClearGrace()
    {
        if (ReadState().GraceStartedUtc is not null)
        {
            ChangeState(s => s.GraceStartedUtc = null);
        }
    }

    /// <summary>Gets or sets the ids of licences the hub withdrew.</summary>
    public static bool IsWithdrawn(string? licenceFile) => LicenceIdOf(licenceFile) is { } id && ReadState().WithdrawnLicences.Contains(id);

    /// <summary>One update file returned by <c>POST /api/update-check</c>.</summary>
    /// <param name="Path">Relative target path inside the HNMO folder.</param>
    /// <param name="Sha256">Expected lower-case SHA-256 hex digest.</param>
    /// <param name="Size">Optional file size in bytes.</param>
    /// <param name="Exec">Whether Unix execute permission should be applied.</param>
    public sealed record HubUpdateFile(string Path, string Sha256, long Size = 0, bool Exec = false);

    /// <summary>Result of asking the hub (<c>POST /api/update-check</c>) whether this licensed server may update.</summary>
    /// <param name="Authorized">Whether the hub authorized an update manifest and download token.</param>
    /// <param name="Denied">Whether the hub explicitly denied authorization (HTTP 403 / invalid / withdrawn).</param>
    /// <param name="ManifestVersion">The offered version.</param>
    /// <param name="Notes">Release notes, if any.</param>
    /// <param name="Files">The authorized manifest files returned by the hub.</param>
    /// <param name="DownloadBase">Base URL for content-addressed file downloads.</param>
    /// <param name="Token">Short-lived download bearer token.</param>
    /// <param name="Error">Human-readable failure note when not authorized.</param>
    public sealed record HubUpdateCheckResult(
        bool Authorized,
        bool Denied,
        string? ManifestVersion,
        string? Notes,
        IReadOnlyList<HubUpdateFile> Files,
        string? DownloadBase,
        string? Token,
        string Error);

    /// <summary>
    /// Calls <c>POST /api/update-check</c> with this folder's licence, machine id, and build stamp before any
    /// update download. Never throws and never touches local files when denied, missing, or unreachable.
    /// </summary>
    /// <param name="buildStamp">This folder's build stamp.</param>
    /// <param name="licenceFile">Optional licence text (defaults to <see cref="LicenceFile"/>).</param>
    /// <param name="client">Optional HTTP client (tests).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authorization result.</returns>
    public static async Task<HubUpdateCheckResult> CheckForUpdateAsync(
        string buildStamp,
        string? licenceFile = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var hub = Url;
        var text = licenceFile ?? LicenceFile;
        if (hub.Length == 0 || string.IsNullOrWhiteSpace(text) || MachineId.Length == 0)
        {
            return new HubUpdateCheckResult(false, true, null, null, [], null, null, "No licence or hub configured.");
        }

        var payload = new JsonObject
        {
            ["licenceId"] = LicenceIdOf(text) ?? string.Empty,
            ["machineId"] = MachineId,
            ["legacyMachineId"] = LegacyMachineId,
            ["buildStamp"] = buildStamp,
            ["licence"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)),
        };

        try
        {
            using var response = await (client ?? DefaultClient)
                .PostAsJsonAsync($"{hub}/api/update-check", payload, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new HubUpdateCheckResult(false, true, null, null, [], null, null, "Hub denied update authorization (403).");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new HubUpdateCheckResult(false, false, null, null, [], null, null, "No release offered (404).");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new HubUpdateCheckResult(false, false, null, null, [], null, null, $"Hub returned {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var manifestVersion = root.TryGetProperty("manifestVersion", out var mv) && mv.ValueKind == JsonValueKind.String ? mv.GetString() : null;
            var notes = root.TryGetProperty("notes", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            var downloadBase = root.TryGetProperty("downloadBase", out var db) && db.ValueKind == JsonValueKind.String ? db.GetString() : null;
            var token = root.TryGetProperty("token", out var tok) && tok.ValueKind == JsonValueKind.String ? tok.GetString() : null;
            if (string.IsNullOrWhiteSpace(manifestVersion) || string.IsNullOrWhiteSpace(downloadBase) || string.IsNullOrWhiteSpace(token)
                || !root.TryGetProperty("files", out var filesEl) || filesEl.ValueKind != JsonValueKind.Array)
            {
                return new HubUpdateCheckResult(false, false, null, null, [], null, null, "Invalid update-check response.");
            }

            var files = new List<HubUpdateFile>();
            foreach (var item in filesEl.EnumerateArray())
            {
                var path = item.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                var sha = item.TryGetProperty("sha256", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sha))
                {
                    continue;
                }

                var size = item.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number ? sz.GetInt64() : 0;
                var exec = item.TryGetProperty("exec", out var ex) && ex.ValueKind == JsonValueKind.True;
                files.Add(new HubUpdateFile(path, sha, size, exec));
            }

            if (files.Count == 0)
            {
                return new HubUpdateCheckResult(false, false, null, null, [], null, null, "Update-check returned no files.");
            }

            return new HubUpdateCheckResult(true, false, manifestVersion, notes, files, downloadBase, token, string.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
        {
            return new HubUpdateCheckResult(false, false, null, null, [], null, null, ex.Message);
        }
    }

    /// <summary>The id the hub gave a licence, if it has one.</summary>
    /// <param name="licenceFile">The licence file's text.</param>
    /// <returns>The id.</returns>
    public static string? LicenceIdOf(string? licenceFile)
    {
        if (string.IsNullOrWhiteSpace(licenceFile))
        {
            return null;
        }

        try
        {
            using var file = JsonDocument.Parse(licenceFile);
            var payload = Convert.FromBase64String(file.RootElement.GetProperty("payload").GetString() ?? string.Empty);
            using var body = JsonDocument.Parse(payload);
            return body.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Asks the developer for a licence; the request is remembered so its answer can be fetched later.</summary>
    /// <param name="name">The name (or the server's).</param>
    /// <param name="contact">How to reach the host.</param>
    /// <param name="players">The players asked for.</param>
    /// <param name="message">A note.</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>Whether the hub took it, and its EN/VI answer.</returns>
    public static async Task<(bool Ok, string En, string Vi)> RequestLicenceAsync(string name, string contact, int players, string message, HttpClient? client = null)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var request = new JsonObject
        {
            ["name"] = name,
            ["contact"] = contact,
            ["players"] = players,
            ["message"] = message,
            ["token"] = token,
        };
        var (ok, answer, en, vi) = await PostAsync("api/request", request, client).ConfigureAwait(false);
        if (!ok || answer?["request"]?.GetValue<string>() is not { Length: > 0 } id)
        {
            return (false, en, vi);
        }

        ChangeState(s =>
        {
            s.Request = new DeveloperHubRequest { Id = id, Token = token, SentUtc = DateTime.UtcNow, Status = "open" };

            // The host's name and contact for the reports, taken silently from the request (owner, 2026-11-21).
            s.HostName = name.Trim();
            s.HostContact = contact.Trim();
        });
        return (true, "Your request was sent to the HNMO developer. The licence comes here by itself once issued.", "Đã gửi yêu cầu tới nhà phát triển HNMO. Giấy phép sẽ tự về đây khi được cấp.");
    }

    /// <summary>Asks the hub for the answer to the last licence request.</summary>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>The status (open, issued, declined; null when there is no request or no answer) and the licence file once issued.</returns>
    public static async Task<(string? Status, string? Licence)> FetchLicenceAsync(HttpClient? client = null)
    {
        if (ReadState().Request is not { Status: "open" } request)
        {
            return (null, null);
        }

        var (ok, answer, _, _) = await PostAsync("api/licence", new JsonObject { ["request"] = request.Id, ["token"] = request.Token }, client).ConfigureAwait(false);
        if (!ok || answer?["status"]?.GetValue<string>() is not { } status)
        {
            return (null, null);
        }

        var licence = answer["licence"]?.GetValue<string>();
        if (status != "open")
        {
            ChangeState(s => s.Request?.Status = status);
        }

        return (status, licence);
    }

    /// <summary>Sends the weekly report.</summary>
    /// <param name="weekly">The report: version, system, players, health, features and bug reports.</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>Whether it was taken, whether the licence was withdrawn, and the EN text of a refusal.</returns>
    public static async Task<(bool Ok, bool Withdrawn, string Error)> SendWeeklyAsync(JsonObject weekly, HttpClient? client = null)
    {
        var (ok, answer, en, _) = await PostAsync("api/weekly", weekly, client).ConfigureAwait(false);
        var withdrawn = ok && answer?["revoked"]?.GetValue<bool>() == true;
        var now = DateTime.UtcNow;
        ChangeState(s =>
        {
            s.LastWeeklyTryUtc = now;
            if (ok)
            {
                s.LastWeeklyUtc = now;
            }

            if (withdrawn && LicenceIdOf(LicenceFile) is { } id && !s.WithdrawnLicences.Contains(id))
            {
                s.WithdrawnLicences.Add(id);
            }
        });
        return (ok, withdrawn, ok ? string.Empty : en);
    }

    /// <summary>Sends the first-setup report once (the hub takes a few a day per computer).</summary>
    /// <param name="setup">The report: system and game (see <c>HostSetupReport</c>).</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>Whether it was taken, and the EN text of a refusal.</returns>
    public static async Task<(bool Ok, string Error)> SendSetupAsync(JsonObject setup, HttpClient? client = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var state = ReadState();
        setup["host"] = new JsonObject { ["name"] = state.HostName, ["contact"] = state.HostContact };
        var (ok, _, en, _) = await PostAsync("api/setup", setup, client).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        ChangeState(s =>
        {
            s.SetupTriedUtc = now;
            if (ok)
            {
                s.SetupReportedUtc = now;
            }
        });
        return (ok, ok ? string.Empty : en);
    }

    /// <summary>For tests: forgets the state kept without a file.</summary>
    internal static void ResetMemoryState() => _memoryState = null;

    private static async Task<(bool Ok, JsonNode? Answer, string En, string Vi)> PostAsync(string path, JsonObject message, HttpClient? client)
    {
        var hub = Url;
        if (hub.Length == 0)
        {
            return (false, null, "Sending to the HNMO developer is off on this server.", "Server này đã tắt gửi tới nhà phát triển HNMO.");
        }

        if (MachineId.Length == 0)
        {
            return (false, null, "This computer's id isn't known yet; restart HNMO.", "Chưa biết mã máy này; hãy khởi động lại HNMO.");
        }

        message["machine"] = MachineId;
        if (IsTestHost)
        {
            // One of the HNMO developer's own test machines: the console keeps it apart from real hosts.
            message["test"] = true;
        }

        if (LegacyMachineId.Length > 0 && !string.Equals(LegacyMachineId, MachineId, StringComparison.OrdinalIgnoreCase))
        {
            // Licences issued before the real machine id are bound to this one; the hub sees both as one computer.
            message["legacyMachine"] = LegacyMachineId;
        }

        message["version"] = Version;
        message["licence"] = LicenceFile;
        try
        {
            using var response = await (client ?? DefaultClient).PostAsJsonAsync($"{hub}/{path}", message).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            JsonNode? answer = null;
            try
            {
                answer = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                // not JSON: the status says enough
            }

            if (response.IsSuccessStatusCode)
            {
                return (true, answer, "Sent to the HNMO developer.", "Đã gửi tới nhà phát triển HNMO.");
            }

            if ((int)response.StatusCode == 429)
            {
                return (false, answer, "The HNMO developer's hub takes this once a week (licence requests: 3 a day); try again later.", "Hub của nhà phát triển HNMO chỉ nhận việc này mỗi tuần một lần (xin giấy phép: 3 lần một ngày); hãy thử lại sau.");
            }

            return (false, answer, $"The hub answered {(int)response.StatusCode}.", $"Hub trả lời {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, null, $"The hub can't be reached: {ex.Message}", $"Không kết nối được hub: {ex.Message}");
        }
    }
}
