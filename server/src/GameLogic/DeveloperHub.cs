// <copyright file="DeveloperHub.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
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

    /// <summary>
    /// Gets a value indicating whether the hub has been unreachable without any response for at least <see cref="GraceDays"/>.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns><c>true</c> if unreachability has persisted past <see cref="GraceDays"/>.</returns>
    public static bool IsHubUnreachablePastGrace(DateTime nowUtc)
        => ReadState().FirstHubUnreachableUtc is { } firstUnreachable && nowUtc >= firstUnreachable.AddDays(GraceDays);

    /// <summary>Gets a value indicating whether a licence file's licence was withdrawn by the developer.</summary>
    /// <param name="licenceFile">The licence file's text.</param>
    /// <returns><c>true</c>, if withdrawn.</returns>
    public static bool IsWithdrawn(string? licenceFile) => LicenceIdOf(licenceFile) is { } id && ReadState().WithdrawnLicences.Contains(id);

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
            var now = DateTime.UtcNow;
            ChangeState(s =>
            {
                s.LastHubReachUtc = now;
                s.FirstHubUnreachableUtc = null;
            });

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
            var now = DateTime.UtcNow;
            ChangeState(s => s.FirstHubUnreachableUtc ??= now);
            return (false, null, $"The hub can't be reached: {ex.Message}", $"Không kết nối được hub: {ex.Message}");
        }
    }
}
