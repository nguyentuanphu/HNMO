// <copyright file="LicenceShopTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// The licence shop of the server (owner, 2026-11-28): the hub's packages kept 6 hours, purchases tied to a request made
/// silently, the backoff that keeps hub calls few (about 8 a purchase), bank transfers, and the automatic error key.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class LicenceShopTest
{
    private const string Hub = "http://127.0.0.1:57843";
    private string? _oldHub;
    private string? _oldKey;
    private string _oldMachine = string.Empty;
    private string? _oldState;
    private DirectoryInfo _dir = null!;

    /// <summary>A stub hub and a state file of its own.</summary>
    [SetUp]
    public void SetUp()
    {
        this._oldHub = Environment.GetEnvironmentVariable("HNMO_DEVELOPER_HUB");
        this._oldKey = Environment.GetEnvironmentVariable("HNMO_AUTO_ERROR_FORWARD_KEY");
        this._oldMachine = DeveloperHub.MachineId;
        this._oldState = DeveloperHub.StateFile;
        this._dir = Directory.CreateTempSubdirectory();
        Environment.SetEnvironmentVariable("HNMO_DEVELOPER_HUB", Hub);
        Environment.SetEnvironmentVariable("HNMO_AUTO_ERROR_FORWARD_KEY", null);
        DeveloperHub.MachineId = "1234567890abcdef";
        DeveloperHub.StateFile = Path.Combine(this._dir.FullName, "developer-hub.json");
        DeveloperHub.ResetMemoryState();
        DeveloperHub.ResetShopCache();
    }

    /// <summary>Gives the test run its settings back.</summary>
    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("HNMO_DEVELOPER_HUB", this._oldHub);
        Environment.SetEnvironmentVariable("HNMO_AUTO_ERROR_FORWARD_KEY", this._oldKey);
        DeveloperHub.MachineId = this._oldMachine;
        DeveloperHub.StateFile = this._oldState;
        DeveloperHub.ResetMemoryState();
        DeveloperHub.ResetShopCache();
        this._dir.Delete(recursive: true);
    }

    /// <summary>The packages are read once and kept 6 hours; a forced refresh asks again at most every 5 minutes.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PackagesAreKeptSixHoursAsync()
    {
        using var hub = new StubHub();
        using var client = new HttpClient(hub);
        var now = new DateTime(2026, 11, 28, 1, 0, 0, DateTimeKind.Utc);
        var first = await DeveloperHub.GetPackagesAsync(nowUtc: now, client: client).ConfigureAwait(false);
        Assert.That(first.Ok, Is.True);
        Assert.That(first.Packages.Select(p => p.Id), Is.EqualTo(new[] { "trial-14", "standard-year", "players-100" }));
        Assert.That((first.Stripe, first.PayOs, first.Bank), Is.EqualTo((true, false, true)));
        var standard = first.Packages[1];
        Assert.That(standard.NameVi, Is.EqualTo("Tiêu chuẩn"));
        Assert.That(standard.Prices["vnd"], Is.EqualTo(4_990_000));
        Assert.That(standard.Seasons, Is.EqualTo(new[] { "s6", "s9" }));
        Assert.That(first.Packages[2].Addon, Is.EqualTo("players"));

        await DeveloperHub.GetPackagesAsync(nowUtc: now.AddHours(5), client: client).ConfigureAwait(false);
        Assert.That(hub.Count("/api/licence-packages"), Is.EqualTo(1), "fresh for 6 hours");
        await DeveloperHub.GetPackagesAsync(force: true, nowUtc: now.AddHours(5), client: client).ConfigureAwait(false);
        await DeveloperHub.GetPackagesAsync(force: true, nowUtc: now.AddHours(5).AddMinutes(1), client: client).ConfigureAwait(false);
        Assert.That(hub.Count("/api/licence-packages"), Is.EqualTo(2), "one forced refresh in 5 minutes");

        DeveloperHub.ResetShopCache();
        hub.Fail = true;
        var restored = await DeveloperHub.GetPackagesAsync(nowUtc: now.AddHours(5).AddMinutes(2), client: client).ConfigureAwait(false);
        Assert.That(restored.Packages, Has.Count.EqualTo(3), "a restart reads the list kept in the state file");
        Assert.That(hub.Count("/api/licence-packages"), Is.EqualTo(2));
    }

    /// <summary>
    /// A card purchase makes the licence request silently, then asks on the backoff (20 s, 40 s, 1, 2, 4, 8, 15 min) and
    /// stops; the hub's retryAfterSeconds is a minimum; a licence with a new id ends the wait.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CardPurchaseAsksOnTheBackoffAsync()
    {
        using var hub = new StubHub();
        using var client = new HttpClient(hub);
        var checkout = await DeveloperHub.StartCheckoutAsync("standard-year", "aud", "stripe", string.Empty, string.Empty, client: client).ConfigureAwait(false);
        Assert.That(checkout.Url, Is.EqualTo("https://checkout.example/pay/1"));
        Assert.That(hub.Count("/api/request"), Is.EqualTo(1), "a request is made silently");
        Assert.That(hub.Last("/api/request")!["name"]!.GetValue<string>(), Is.EqualTo("HNMO host"));
        var sent = hub.Last("/api/licence-checkout")!;
        Assert.That((sent["packageId"]!.GetValue<string>(), sent["currency"]!.GetValue<string>(), sent["provider"]!.GetValue<string>()), Is.EqualTo(("standard-year", "aud", "stripe")));
        Assert.That(sent["token"]!.GetValue<string>(), Is.EqualTo(DeveloperHub.ReadState().Request!.Token));

        var start = DeveloperHub.ReadState().Purchase!.StartedUtc;
        Assert.That(await DeveloperHub.PollPurchaseAsync(start.AddSeconds(10), client).ConfigureAwait(false), Is.Null);
        Assert.That(hub.Count("/api/licence"), Is.Zero, "not before the first step");

        var at = start;
        foreach (var step in DeveloperHub.PurchaseBackoff)
        {
            at += step;
            await DeveloperHub.PollPurchaseAsync(at, client).ConfigureAwait(false);
            await DeveloperHub.PollPurchaseAsync(at.AddSeconds(5), client).ConfigureAwait(false);
        }

        Assert.That(hub.Count("/api/licence"), Is.EqualTo(DeveloperHub.PurchaseBackoff.Count), "one ask per step, about 8 a purchase");
        Assert.That(DeveloperHub.ReadState().Purchase, Is.Null, "then it stops");

        // The hub's retryAfterSeconds is a minimum.
        DeveloperHub.ResetMemoryState();
        File.Delete(DeveloperHub.StateFile!);
        hub.RetryAfter = 600;
        await DeveloperHub.StartCheckoutAsync("standard-year", "aud", "stripe", "Minh", "minh@example.test", client: client).ConfigureAwait(false);
        var purchase = DeveloperHub.ReadState().Purchase!;
        await DeveloperHub.PollPurchaseAsync(purchase.NextCheckUtc, client).ConfigureAwait(false);
        Assert.That(DeveloperHub.ReadState().Purchase!.NextCheckUtc - purchase.NextCheckUtc, Is.EqualTo(TimeSpan.FromSeconds(600)));

        hub.Licence = "{\"payload\":\"" + Convert.ToBase64String("{\"id\":\"L-new\"}"u8.ToArray()) + "\",\"signature\":\"\"}";
        var due = DeveloperHub.ReadState().Purchase!.NextCheckUtc;
        Assert.That(await DeveloperHub.PollPurchaseAsync(due, client).ConfigureAwait(false), Is.EqualTo(hub.Licence));
        Assert.That(DeveloperHub.ReadState().Purchase, Is.Null);
    }

    /// <summary>Opening the page or the host's button asks at most once in 30 seconds.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CheckNowIsLimitedAsync()
    {
        using var hub = new StubHub();
        using var client = new HttpClient(hub);
        await DeveloperHub.StartCheckoutAsync("standard-year", "aud", "stripe", "Minh", string.Empty, client: client).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        await DeveloperHub.CheckPurchaseNowAsync(now, client).ConfigureAwait(false);
        await DeveloperHub.CheckPurchaseNowAsync(now.AddSeconds(10), client).ConfigureAwait(false);
        await DeveloperHub.CheckPurchaseNowAsync(now.AddSeconds(31), client).ConfigureAwait(false);
        Assert.That(hub.Count("/api/licence"), Is.EqualTo(2));
    }

    /// <summary>A bank transfer gives the VietQR data and waits hourly (or as the hub says) instead of the short backoff.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task BankTransferWaitsHourlyAsync()
    {
        using var hub = new StubHub();
        using var client = new HttpClient(hub);
        hub.RetryAfter = 3600;
        var checkout = await DeveloperHub.StartCheckoutAsync("standard-year", "vnd", "bank", "Minh", string.Empty, client: client).ConfigureAwait(false);
        Assert.That(checkout.Bank, Is.EqualTo(new HubBankTransfer("970436", string.Empty, "0011001234567", "NGUYEN VAN A", 4_990_000, "HNMO O1A2B3", "O1A2B3")));
        var purchase = DeveloperHub.ReadState().Purchase!;
        Assert.That(purchase.Bank, Is.EqualTo(checkout.Bank), "shown again when the page is opened later");
        Assert.That(purchase.NextCheckUtc - purchase.StartedUtc, Is.EqualTo(TimeSpan.FromHours(1)));
        await DeveloperHub.PollPurchaseAsync(purchase.NextCheckUtc, client).ConfigureAwait(false);
        Assert.That(DeveloperHub.ReadState().Purchase!.NextCheckUtc - purchase.NextCheckUtc, Is.EqualTo(TimeSpan.FromHours(1)));
        await DeveloperHub.PollPurchaseAsync(purchase.StartedUtc + DeveloperHub.BankTransferWait, client).ConfigureAwait(false);
        Assert.That(DeveloperHub.ReadState().Purchase, Is.Null, "after 3 days the host's button and the hourly round remain");
    }

    /// <summary>An open request is asked about hourly for 6 hours, every 6 hours until day 7, then daily; never before the hub's retryAfterSeconds.</summary>
    [Test]
    public void OpenRequestIsAskedLessOverTime()
    {
        var sent = new DateTime(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);
        DeveloperHub.ChangeState(s => s.Request = new DeveloperHubRequest { Id = "r", Token = "t", SentUtc = sent, Status = "open" });
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddMinutes(1)), Is.True);
        DeveloperHub.ChangeState(s => s.Request!.LastCheckUtc = sent.AddHours(2));
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddHours(2.5)), Is.False);
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddHours(3)), Is.True);
        DeveloperHub.ChangeState(s => s.Request!.LastCheckUtc = sent.AddHours(10));
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddHours(13)), Is.False, "every 6 hours after the first 6");
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddHours(16)), Is.True);
        DeveloperHub.ChangeState(s => s.Request!.LastCheckUtc = sent.AddDays(8));
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddDays(8).AddHours(12)), Is.False, "daily after a week");
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddDays(9)), Is.True);
        DeveloperHub.ChangeState(s => s.Request!.NotBeforeUtc = sent.AddDays(10));
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddDays(9.5)), Is.False, "the hub's retryAfterSeconds is a minimum");
        DeveloperHub.ChangeState(s => s.Request!.Status = "issued");
        Assert.That(DeveloperHub.IsRequestCheckDue(sent.AddDays(11)), Is.False);
    }

    /// <summary>The hub gives the automatic error key with the setup report or the licence answer; the server keeps it.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AutomaticErrorKeyComesFromTheHubAsync()
    {
        using var hub = new StubHub();
        using var client = new HttpClient(hub);
        Assert.That(DeveloperHub.CanForwardAutomaticErrors, Is.False);
        await DeveloperHub.SendSetupAsync(new JsonObject(), client).ConfigureAwait(false);
        Assert.That(hub.Last("/api/setup")!["needAutoErrorKey"]!.GetValue<bool>(), Is.True);
        Assert.That(DeveloperHub.ReadState().AutoErrorKey, Is.EqualTo(StubHub.Key));
        Assert.That(DeveloperHub.CanForwardAutomaticErrors, Is.True, "no host setting needed");

        await DeveloperHub.SendSetupAsync(new JsonObject(), client).ConfigureAwait(false);
        Assert.That(hub.Last("/api/setup")!.ContainsKey("needAutoErrorKey"), Is.False, "asked only while missing");
        Environment.SetEnvironmentVariable("HNMO_AUTO_ERROR_FORWARD_KEY", "short");
        Assert.That(DeveloperHub.CanForwardAutomaticErrors, Is.True, "a broken host setting falls back to the hub's key");
    }

    /// <summary>The trial is issued at once; a second one is refused and remembered.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TrialOnceAsync()
    {
        using var hub = new StubHub();
        using var client = new HttpClient(hub);
        var (licence, _, _) = await DeveloperHub.StartTrialAsync("Minh", string.Empty, client).ConfigureAwait(false);
        Assert.That(licence, Is.EqualTo("trial-licence"));
        hub.TrialUsed = true;
        var (again, en, _) = await DeveloperHub.StartTrialAsync("Minh", string.Empty, client).ConfigureAwait(false);
        Assert.That(again, Is.Null);
        Assert.That(en, Does.Contain("already"));
        Assert.That(DeveloperHub.ReadState().TrialUsed, Is.True);
    }

    /// <summary>The VietQR string is the web client's, byte for byte (fixtures made with client/web/src/vietqr.ts).</summary>
    /// <param name="bin">The BIN.</param>
    /// <param name="account">The account.</param>
    /// <param name="name">The holder.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="note">The note.</param>
    /// <param name="expected">What vietqr.ts gives.</param>
    [TestCase("970436", "0011001234567", "NGUYEN VAN A", 1490000, "HNMO L-ab12cd34", "00020101021238570010A00000072701270006970436011300110012345670208QRIBFTTA5303704540714900005802VN5912NGUYEN VAN A62190815HNMO L-ab12cd346304EDA5")]
    [TestCase("970422", "123456789", "Công ty Đồng Hành", 0, "", "00020101021138530010A0000007270123000697042201091234567890208QRIBFTTA53037045802VN5917Cong ty Dong Hanh6304656F")]
    [TestCase(" 970415 ", " 1012345678 ", "Trần   Thị Bích Ngọc rất dài tên quá mức cho phép", 99999.9, "Thanh toán giấy phép HNMO gói Pro năm — mã đơn 2026-11-28-ABCDEF", "00020101021238540010A00000072701240006970415011010123456780208QRIBFTTA53037045405999995802VN5925Tran Thi Bich Ngoc rat da62540850Thanh toan giay phep HNMO goi Pro nam ma don 2026-630441F2")]
    public void VietQrMatchesTheWebClient(string bin, string account, string name, double amount, string note, string expected)
    {
        Assert.That(VietQr.BuildPayload(bin, account, name, (decimal)amount, note), Is.EqualTo(expected));
    }

    private sealed class StubHub : HttpMessageHandler
    {
        public const string Key = "hub-given-key-0123456789abcdefghijkl";

        private readonly List<(string Path, JsonObject? Body)> _calls = [];

        public bool Fail { get; set; }

        public int? RetryAfter { get; set; }

        public string? Licence { get; set; }

        public bool TrialUsed { get; set; }

        public int Count(string path) => this._calls.Count(c => c.Path == path);

        public JsonObject? Last(string path) => this._calls.LastOrDefault(c => c.Path == path).Body;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.That(request.RequestUri!.GetLeftPart(UriPartial.Authority), Is.EqualTo(Hub));
            var path = request.RequestUri.AbsolutePath;
            var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)) as JsonObject;
            this._calls.Add((path, body));
            if (this.Fail)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
            }

            var retry = this.RetryAfter is { } r ? $",\"retryAfterSeconds\":{r}" : string.Empty;
            var answer = path switch
            {
                "/api/licence-packages" => """
                    {"packages":[
                      {"id":"players-100","kind":"addon","addon":"players","name":{"en":"+100 players","vi":"+100 người"},"description":{"en":"More players"},"addPlayers":100,"billing":"once","prices":{"vnd":990000},"order":30},
                      {"id":"standard-year","kind":"plan","name":{"en":"Standard","vi":"Tiêu chuẩn"},"description":{"en":"200 players"},"maxPlayers":200,"seasons":["s6","s9","bogus"],"billing":"year","durationDays":365,"prices":{"vnd":4990000,"aud":29900,"eur":1},"highlight":true,"order":20},
                      {"id":"trial-14","kind":"trial","name":{"en":"Trial"},"description":{"en":"14 days"},"maxPlayers":100,"seasons":["s6","s9","arena"],"billing":"once","durationDays":14,"prices":{},"order":10},
                      {"id":"","kind":"plan"},{"id":"x","kind":"other"}
                    ],"providers":{"stripe":true,"payos":false,"bank":true}}
                    """,
                "/api/request" => """{"ok":true,"request":"2026-11-28-abcd1234"}""",
                "/api/licence-checkout" when body?["provider"]?.GetValue<string>() == "bank" =>
                    $$"""{"order":"O1A2B3","bank":{"bin":"970436","account":"0011001234567","accountName":"NGUYEN VAN A"},"amount":4990000,"note":"HNMO O1A2B3"{{retry}}}""",
                "/api/licence-checkout" => """{"order":"O9","url":"https://checkout.example/pay/1"}""",
                "/api/licence" => this.Licence is { } licence
                    ? new JsonObject { ["status"] = "issued", ["licence"] = licence }.ToJsonString()
                    : $$"""{"status":"open","licence":null{{retry}}}""",
                "/api/licence-trial" => this.TrialUsed ? """{"error":"trial used"}""" : """{"status":"issued","licence":"trial-licence"}""",
                "/api/setup" => $$"""{"ok":true,"autoErrorKey":"{{Key}}"}""",
                _ => "{}",
            };
            var status = path == "/api/licence-trial" && this.TrialUsed ? HttpStatusCode.Conflict : HttpStatusCode.OK;
            return new HttpResponseMessage(status) { Content = new StringContent(answer) };
        }
    }
}
