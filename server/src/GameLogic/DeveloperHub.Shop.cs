// <copyright file="DeveloperHub.Shop.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

using System.Net.Http;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// The licence shop (owner, 2026-11-28): the host buys or starts a trial on the License page; the hub signs the licence
/// once paid and the server picks it up by itself. Every hub call is a paid function call (owner, 2026-11-28: as few as
/// possible), so the server asks for the licence on a backoff (about 8 calls per purchase), keeps the package list for
/// 6 hours, and the host's "check now" works once in 30 seconds.
/// </summary>
public static partial class DeveloperHub
{
    /// <summary>How long the package list is kept.</summary>
    public static readonly TimeSpan PackageCacheAge = TimeSpan.FromHours(6);

    /// <summary>How often the host may refresh the package list by hand.</summary>
    public static readonly TimeSpan PackageRefreshLimit = TimeSpan.FromMinutes(5);

    /// <summary>How often the hub may be asked for an awaited licence outside the schedule (page opened, button).</summary>
    public static readonly TimeSpan ManualCheckLimit = TimeSpan.FromSeconds(30);

    /// <summary>How long a bank transfer's licence is awaited (the developer confirms the transfer by hand).</summary>
    public static readonly TimeSpan BankTransferWait = TimeSpan.FromDays(3);

    /// <summary>The wait between two asks while a bank transfer is confirmed, when the hub names none.</summary>
    public static readonly TimeSpan BankTransferPoll = TimeSpan.FromHours(1);

    /// <summary>The waits between the scheduled asks for an awaited licence; then the server stops asking.</summary>
    public static readonly IReadOnlyList<TimeSpan> PurchaseBackoff =
    [
        TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(15),
    ];

    private static readonly Lock PackageLock = new();
    private static HubLicenceCatalogue? _packages;
    private static DateTime _packagesFetchedUtc;
    private static DateTime _packagesForcedUtc;

    /// <summary>Gets the hub's licence packages and its payment ways, from the 6-hour cache when it is fresh.</summary>
    /// <param name="force">Asks the hub even when the cache is fresh (at most once in <see cref="PackageRefreshLimit"/>).</param>
    /// <param name="nowUtc">The time now (tests); now when null.</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>The catalogue; not <see cref="HubLicenceCatalogue.Ok"/> when the hub is off or didn't answer.</returns>
    public static async Task<HubLicenceCatalogue> GetPackagesAsync(bool force = false, DateTime? nowUtc = null, HttpClient? client = null)
    {
        var hub = Url;
        if (hub.Length == 0)
        {
            return new HubLicenceCatalogue(false, [], false, false, "Sending to the HNMO developer is off on this server.");
        }

        var now = nowUtc ?? DateTime.UtcNow;
        lock (PackageLock)
        {
            if (_packages is null && ReadState() is { PackagesJson: { } saved, PackagesFetchedUtc: { } savedAt } && ParseCatalogue(saved) is { } restored)
            {
                (_packages, _packagesFetchedUtc) = (restored, savedAt);
            }

            var fresh = _packages is not null && now - _packagesFetchedUtc < PackageCacheAge;
            var mayForce = force && now - _packagesForcedUtc >= PackageRefreshLimit;
            if (fresh && !mayForce)
            {
                return _packages!;
            }

            if (mayForce)
            {
                _packagesForcedUtc = now;
            }
        }

        try
        {
            var address = $"{hub}/api/licence-packages?machine={Uri.EscapeDataString(MachineId)}";
            using var response = await (client ?? DefaultClient).GetAsync(address).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || ParseCatalogue(text) is not { } catalogue)
            {
                return CachedOr(new HubLicenceCatalogue(false, [], false, false, $"The hub answered {(int)response.StatusCode}."));
            }

            lock (PackageLock)
            {
                (_packages, _packagesFetchedUtc) = (catalogue, now);
            }

            ChangeState(s =>
            {
                s.PackagesJson = text.Length <= 200_000 ? text : null;
                s.PackagesFetchedUtc = now;
            });
            return catalogue;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return CachedOr(new HubLicenceCatalogue(false, [], false, false, $"The hub can't be reached: {ex.Message}"));
        }
    }

    /// <summary>Asks the hub for an awaited licence now (page opened, or the host's "I've paid" button), once in 30 seconds.</summary>
    /// <param name="nowUtc">The time now.</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>The new licence file, if it arrived; null also when it is too soon to ask again.</returns>
    public static Task<string?> CheckPurchaseNowAsync(DateTime nowUtc, HttpClient? client = null)
    {
        if (ReadState().Purchase is not { } purchase || (purchase.LastCheckUtc is { } last && nowUtc - last < ManualCheckLimit))
        {
            return Task.FromResult<string?>(null);
        }

        return AskForPurchaseAsync(nowUtc, scheduled: false, client);
    }

    /// <summary>
    /// Whether the hourly round should ask for the answer of an open licence request: hourly in its first 6 hours, every
    /// 6 hours until day 7, then daily (owner, 2026-11-28: few hub calls).
    /// </summary>
    /// <param name="nowUtc">The time now.</param>
    /// <returns>Whether to ask now.</returns>
    public static bool IsRequestCheckDue(DateTime nowUtc)
    {
        if (ReadState().Request is not { Status: "open" } request)
        {
            return false;
        }

        if (request.NotBeforeUtc is { } notBefore && nowUtc < notBefore)
        {
            return false;
        }

        var age = nowUtc - (request.CheckBaseUtc ?? request.SentUtc);
        var every = age < TimeSpan.FromHours(6) ? TimeSpan.FromHours(1) : age < TimeSpan.FromDays(7) ? TimeSpan.FromHours(6) : TimeSpan.FromDays(1);

        // A little early is fine: the hourly round must not slip a whole period.
        return request.LastCheckUtc is not { } last || nowUtc - last >= every - TimeSpan.FromMinutes(5);
    }

    /// <summary>The longer of a wait and the hub's retryAfterSeconds (at most a day).</summary>
    private static TimeSpan AtLeast(TimeSpan wait, int? retryAfterSeconds) =>
        retryAfterSeconds is > 0 and <= 86_400 && TimeSpan.FromSeconds(retryAfterSeconds.Value) > wait ? TimeSpan.FromSeconds(retryAfterSeconds.Value) : wait;

    private static HubLicenceCatalogue CachedOr(HubLicenceCatalogue failure)
    {
        lock (PackageLock)
        {
            return _packages ?? failure;
        }
    }

    private static HubLicenceCatalogue? ParseCatalogue(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is not JsonObject answer)
            {
                return null;
            }

            var packages = (answer["packages"] as JsonArray ?? []).OfType<JsonObject>().Select(ReadPackage).OfType<HubLicencePackage>()
                .OrderBy(p => p.Order).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
            var providers = answer["providers"] as JsonObject;
            return new HubLicenceCatalogue(true, packages, Flag(providers?["stripe"]), Flag(providers?["payos"]), string.Empty, Flag(providers?["bank"]));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Starts a purchase: the hub answers with the payment page to open (stripe, payos), or with the bank transfer to make
    /// (bank: drawn as a VietQR on the License page); the licence comes by itself once paid.
    /// </summary>
    /// <param name="packageId">The package.</param>
    /// <param name="currency">vnd, aud or usd.</param>
    /// <param name="provider">stripe, payos or bank.</param>
    /// <param name="hostName">The host's name for the request, when the server has none yet.</param>
    /// <param name="hostContact">The host's contact for the request, when the server has none yet.</param>
    /// <param name="upgradeTo">For the upgrade add-on: the package to move up to.</param>
    /// <param name="toMachine">For the move and second-machine add-ons: the other computer's id (it gets the licence on its next request).</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>The payment page or bank transfer, or the EN/VI reason it couldn't start.</returns>
    public static async Task<HubCheckout> StartCheckoutAsync(string packageId, string currency, string provider, string hostName, string hostContact, string? upgradeTo = null, string? toMachine = null, HttpClient? client = null)
    {
        var request = await EnsureRequestAsync(hostName, hostContact, client).ConfigureAwait(false);
        if (request is null)
        {
            return new HubCheckout(null, null, "The HNMO developer's hub can't be reached; try again in a minute.", "Không kết nối được hub của nhà phát triển HNMO; thử lại sau một phút.");
        }

        var message = new JsonObject
        {
            ["request"] = request.Id,
            ["token"] = request.Token,
            ["packageId"] = packageId,
            ["currency"] = currency,
            ["provider"] = provider,
        };
        if (!string.IsNullOrWhiteSpace(upgradeTo))
        {
            message["upgradeTo"] = upgradeTo.Trim();
        }

        if (!string.IsNullOrWhiteSpace(toMachine))
        {
            message["toMachine"] = toMachine.Trim().ToLowerInvariant();
        }

        var (ok, answer, en, vi) = await PostAsync("api/licence-checkout", message, client).ConfigureAwait(false);
        var order = answer?["order"] is JsonValue o && o.TryGetValue<string>(out var id) ? id : string.Empty;
        if (ok && provider == "bank" && ReadBankTransfer(answer, order) is { } bank)
        {
            var retry = AtLeast(BankTransferPoll, answer?["retryAfterSeconds"] is JsonValue r && r.TryGetValue<int>(out var seconds) ? seconds : null);
            BeginWaiting(packageId, order, provider, retry, bank);
            return new HubCheckout(null, bank, "Transfer the exact amount with this note. The licence arrives after the HNMO developer confirms the transfer (usually within a few hours).", "Chuyển đúng số tiền với nội dung này. Giấy phép sẽ về sau khi nhà phát triển HNMO xác nhận chuyển khoản (thường trong vài giờ).");
        }

        if (!ok || answer?["url"] is not JsonValue urlValue || !urlValue.TryGetValue<string>(out var url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var page) || page.Scheme != Uri.UriSchemeHttps)
        {
            return new HubCheckout(null, null, ok ? "The hub gave no payment page." : en, ok ? "Hub không trả trang thanh toán." : vi);
        }

        BeginWaiting(packageId, order, provider, null, null);
        return new HubCheckout(page.ToString(), null, "The payment page opens in a new tab. The licence arrives here by itself once paid.", "Trang thanh toán mở trong tab mới. Giấy phép sẽ tự về đây khi thanh toán xong.");
    }

    private static HubBankTransfer? ReadBankTransfer(JsonNode? answer, string order)
    {
        static string Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : string.Empty;
        if (answer?["bank"] is not JsonObject bank || answer["amount"] is not JsonValue a || !a.TryGetValue<long>(out var amount) || amount <= 0)
        {
            return null;
        }

        var bin = Text(bank["bin"]);
        var account = Text(bank["account"]);
        var note = Text(answer["note"]);
        return bin.Length is >= 6 and <= 8 && bin.All(char.IsAsciiDigit) && account.Length is > 0 and <= 19 && account.All(char.IsAsciiLetterOrDigit) && note.Length is > 0 and <= 50
            ? new HubBankTransfer(bin, Text(bank["name"]), account, Text(bank["accountName"]), amount, note, order)
            : null;
    }

    /// <summary>Starts the free trial (once per computer); the hub issues it at once.</summary>
    /// <param name="hostName">The host's name for the request, when the server has none yet.</param>
    /// <param name="hostContact">The host's contact for the request, when the server has none yet.</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>The trial licence file when issued, and the EN/VI answer.</returns>
    public static async Task<(string? Licence, string En, string Vi)> StartTrialAsync(string hostName, string hostContact, HttpClient? client = null)
    {
        var request = await EnsureRequestAsync(hostName, hostContact, client).ConfigureAwait(false);
        if (request is null)
        {
            return (null, "The HNMO developer's hub can't be reached; try again in a minute.", "Không kết nối được hub của nhà phát triển HNMO; thử lại sau một phút.");
        }

        var (ok, answer, en, vi) = await PostAsync("api/licence-trial", new JsonObject { ["request"] = request.Id, ["token"] = request.Token }, client).ConfigureAwait(false);
        KeepAutoErrorKey(answer);
        if (ok && answer?["status"]?.GetValue<string>() == "issued" && answer["licence"] is JsonValue file && file.TryGetValue<string>(out var licence))
        {
            ChangeState(s => s.TrialUsed = true);
            return (licence, "The trial licence is here.", "Giấy phép dùng thử đã về.");
        }

        if (answer?["error"]?.GetValue<string>() is "trial used" or "used")
        {
            ChangeState(s => s.TrialUsed = true);
            return (null, "This computer already had its free trial.", "Máy này đã dùng thử miễn phí rồi.");
        }

        return (null, en, vi);
    }

    /// <summary>
    /// The scheduled ask for an awaited licence: only when it is due (<see cref="PurchaseBackoff"/>, or the hub's
    /// <c>retryAfterSeconds</c>); after the last step the server stops asking (the hourly round and the page still do).
    /// </summary>
    /// <param name="nowUtc">The time now.</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <returns>The new licence file, if it arrived.</returns>
    public static Task<string?> PollPurchaseAsync(DateTime nowUtc, HttpClient? client = null)
    {
        if (ReadState().Purchase is not { } purchase || nowUtc < purchase.NextCheckUtc)
        {
            return Task.FromResult<string?>(null);
        }

        return AskForPurchaseAsync(nowUtc, scheduled: true, client);
    }

    private static async Task<string?> AskForPurchaseAsync(DateTime nowUtc, bool scheduled, HttpClient? client)
    {
        var (status, licence, retryAfter) = await FetchLicenceAnswerAsync(client).ConfigureAwait(false);
        var previous = ReadState().Purchase?.PreviousLicenceId;
        if (status == "issued" && licence is not null && LicenceIdOf(licence) != previous)
        {
            ChangeState(s => s.Purchase = null);
            return licence;
        }

        ChangeState(s =>
        {
            if (s.Purchase is not { } p)
            {
                return;
            }

            p.LastCheckUtc = nowUtc;
            if (!scheduled)
            {
                return;
            }

            if (p.Provider == "bank")
            {
                // The developer confirms a transfer by hand: ask hourly (or as the hub says) for 3 days.
                if (nowUtc - p.StartedUtc >= BankTransferWait)
                {
                    s.Purchase = null;
                }
                else
                {
                    p.NextCheckUtc = nowUtc + AtLeast(BankTransferPoll, retryAfter);
                }

                return;
            }

            p.Attempts++;
            if (p.Attempts >= PurchaseBackoff.Count)
            {
                // The schedule is over; the hourly round asks while the request is open, and the host's button still works.
                s.Purchase = null;
                return;
            }

            // The hub's retryAfterSeconds is a minimum, never a reason to ask sooner than the schedule.
            p.NextCheckUtc = nowUtc + AtLeast(PurchaseBackoff[p.Attempts], retryAfter);
        });
        return null;
    }

    /// <summary>For tests: forgets the package list kept in memory.</summary>
    internal static void ResetShopCache()
    {
        lock (PackageLock)
        {
            (_packages, _packagesFetchedUtc, _packagesForcedUtc) = (null, default, default);
        }
    }

    /// <summary>Stops waiting for a purchase (the host cancelled the payment).</summary>
    public static void StopWaiting() => ChangeState(s => s.Purchase = null);

    private static void BeginWaiting(string packageId, string order, string provider, TimeSpan? firstWait, HubBankTransfer? bank)
    {
        var previous = LicenceIdOf(LicenceFile);
        var now = DateTime.UtcNow;
        ChangeState(s =>
        {
            s.Purchase = new DeveloperHubPurchase
            {
                PackageId = packageId,
                Order = order,
                Provider = provider,
                StartedUtc = now,
                PreviousLicenceId = previous,
                NextCheckUtc = now + (firstWait ?? PurchaseBackoff[0]),
                Bank = bank,
            };

            // The request's answer is asked for on its schedule again, from now.
            if (s.Request is { } request)
            {
                request.CheckBaseUtc = now;
                request.LastCheckUtc = null;
            }
        });
    }

    /// <summary>The licence request purchases and trials are tied to; one is made silently when there is none.</summary>
    private static async Task<DeveloperHubRequest?> EnsureRequestAsync(string hostName, string hostContact, HttpClient? client)
    {
        if (ReadState().Request is { Id.Length: > 0, Token.Length: > 0 } existing)
        {
            return existing;
        }

        var state = ReadState();
        var name = FirstNonEmpty(state.HostName, hostName, "HNMO host");
        var contact = FirstNonEmpty(state.HostContact, hostContact, string.Empty);
        var (ok, _, _) = await RequestLicenceAsync(name, contact, 0, "Licence shop", client).ConfigureAwait(false);
        return ok ? ReadState().Request : null;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static bool Flag(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static HubLicencePackage? ReadPackage(JsonObject p)
    {
        static string Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : string.Empty;
        static int Number(JsonNode? node) => node is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;

        var id = Text(p["id"]);
        var kind = Text(p["kind"]);
        if (id.Length == 0 || kind is not ("plan" or "trial" or "addon"))
        {
            return null;
        }

        var name = p["name"] as JsonObject;
        var description = p["description"] as JsonObject;
        var prices = new Dictionary<string, long>(StringComparer.Ordinal);
        if (p["prices"] is JsonObject priceList)
        {
            foreach (var (currency, amount) in priceList)
            {
                if (currency is "vnd" or "aud" or "usd" && amount is JsonValue a && a.TryGetValue<long>(out var value) && value > 0)
                {
                    prices[currency] = value;
                }
            }
        }

        var seasons = (p["seasons"] as JsonArray ?? []).Select(Text).Where(s => s is "s6" or "s9" or "arena").Distinct().ToList();
        var durationDays = p["durationDays"] is JsonValue d && d.TryGetValue<int>(out var days) && days > 0 ? days : (int?)null;
        return new HubLicencePackage(
            id,
            kind,
            FirstNonEmpty(Text(name?["en"]), id),
            FirstNonEmpty(Text(name?["vi"]), Text(name?["en"]), id),
            Text(description?["en"]),
            FirstNonEmpty(Text(description?["vi"]), Text(description?["en"])),
            Number(p["maxPlayers"]),
            Number(p["addPlayers"]),
            seasons,
            FirstNonEmpty(Text(p["billing"]), "once"),
            durationDays,
            prices,
            Flag(p["highlight"]),
            Number(p["order"]),
            Text(p["addon"]));
    }
}
