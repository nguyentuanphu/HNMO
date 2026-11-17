// <copyright file="WebsiteAccountEmailControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.IO;
using System.Net.Mail;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Exercises authentication, mailbox proof, replay prevention and recovery without sending real email.</summary>
[TestFixture]
public class WebsiteAccountEmailControllerTests
{
    private DirectoryInfo _directory = null!;
    private IPersistenceContextProvider _persistence = null!;
    private Account _account = null!;
    private GameConfiguration _configuration = null!;
    private Mock<IDataSource<GameConfiguration>> _source = null!;
    private Mock<ILoginServer> _login = null!;
    private Mock<IWebsiteConfirmationMail> _mail = null!;
    private Mock<TimeProvider> _clock = null!;
    private WebsiteMailSettingsStore _settings = null!;
    private WebsiteEmailChallengeStore _challenges = null!;
    private WebsiteAccountEmailController _controller = null!;
    private string _code = string.Empty;
    private DateTimeOffset _now;
    private bool _failConsumedOnce;

    /// <summary>Creates two accounts, a persistent settings file and a fake mailbox.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._directory = Directory.CreateTempSubdirectory("hnmo-email-api-");
        this._persistence = new InMemoryPersistenceContextProvider();
        using var setup = this._persistence.CreateNewContext();
        this._configuration = setup.CreateNew<GameConfiguration>();
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var players = this._persistence.CreateNewPlayerContext(this._configuration);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
        this._account.EMail = "old@example.test";
        var other = players.CreateNew<Account>();
        other.LoginName = "bob";
        other.PasswordHash = this._account.PasswordHash;
        await players.SaveChangesAsync().ConfigureAwait(false);
        this._source = new Mock<IDataSource<GameConfiguration>>();
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        this._login = new Mock<ILoginServer>();
        this._login.Setup(s => s.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        this._mail = new Mock<IWebsiteConfirmationMail>();
        this._mail.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((address, code, language, cancellationToken) => this._code = code).Returns(Task.CompletedTask);
        this._now = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
        this._clock = new Mock<TimeProvider>();
        this._clock.Setup(c => c.GetUtcNow()).Returns(() => this._now);
        var protection = DataProtectionProvider.Create(this._directory);
        this._settings = new WebsiteMailSettingsStore(new HnmoStateStore(Path.Combine(this._directory.FullName, HnmoStateStore.StateFileName)), protection);
        this._settings.Save(new WebsiteMailSettings { Enabled = true, Host = "smtp.example.test", From = "host@example.test" }, null);
        var rows = new InMemoryHnmoPlayerStateRowBackend();
        var backend = new Mock<IHnmoPlayerStateRowBackend>();
        backend.Setup(b => b.LoadAll(It.IsAny<string>())).Returns((string feature) => rows.LoadAll(feature));
        backend.Setup(b => b.UpsertBatch(It.IsAny<IReadOnlyList<HnmoFeatureRow>>(), It.IsAny<IReadOnlyList<(string Feature, string ScopeId)>>()))
            .Callback<IReadOnlyList<HnmoFeatureRow>, IReadOnlyList<(string Feature, string ScopeId)>>((upserts, deletes) =>
            {
                if (this._failConsumedOnce && upserts.Any(r => r.PayloadJson.Contains("\"consumed\"", StringComparison.Ordinal)))
                {
                    this._failConsumedOnce = false;
                    throw new IOException("Simulated post-save challenge write failure.");
                }

                rows.UpsertBatch(upserts, deletes);
            });
        this._challenges = new WebsiteEmailChallengeStore(backend.Object, protection);
        this._controller = this.CreateController();
    }

    /// <summary>Deletes only this fixture's temporary files.</summary>
    [TearDown]
    public void TearDown() => this._directory.Delete(true);

    /// <summary>Requesting proves nothing yet; only the verified mailbox is saved and a consumed code cannot be replayed.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ConfirmsOnlyAfterDeliveryAndNeverReplaysAsync()
    {
        var sent = (OkObjectResult)await this.SendAsync().ConfigureAwait(false);
        var json = JsonSerializer.Serialize(sent.Value);
        Assert.That(json, Does.Not.Contain(this._code).And.Not.Contain("secret"));
        Assert.That(this._code, Does.Match("^[0-9A-F]{24}$"));
        Assert.That(this._account.EMail, Is.EqualTo("old@example.test"));
        var challenge = this._challenges.Read(this._account.GetId())!;
        Assert.That(JsonSerializer.Serialize(challenge), Does.Not.Contain(this._code).And.Not.Contain("new@example.test").And.Not.Contain(this._account.PasswordHash));
        Assert.That(await this.ConfirmAsync(this._code.ToLowerInvariant()).ConfigureAwait(false), Is.InstanceOf<OkObjectResult>());
        Assert.That(this._account.EMail, Is.EqualTo("new@example.test"));
        Assert.That(await this.ConfirmAsync(this._code).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(this._controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        this._login.Verify(s => s.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Exactly(3));
    }

    /// <summary>Invalid credentials, another account and a banned account cannot confirm this player's code.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ChecksAccountOwnershipAndCredentialsAsync()
    {
        Assert.That(await this._controller.RequestAsync(new("ann", "wrong", "new@example.test")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        await this.SendAsync().ConfigureAwait(false);
        Assert.That(await this._controller.ConfirmAsync(new("bob", "secret", Code: this._code)).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(this._account.EMail, Is.EqualTo("old@example.test"));
        this._account.State = AccountState.Banned;
        Assert.That(await this.ConfirmAsync(this._code).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
    }

    /// <summary>Online accounts are refused and authorization is repeated after acquiring the reservation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RefusesOnlineAndChangedCredentialsAsync()
    {
        this._login.Setup(s => s.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(false);
        Assert.That(await this.SendAsync().ConfigureAwait(false), Is.InstanceOf<ConflictObjectResult>());
        this._mail.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        this._login.Verify(s => s.ReleaseAccountReservationAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        this._login.Setup(s => s.TryReserveAccountAsync("ann", It.IsAny<int>()))
            .Callback(() => this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("replacement", 4)).ReturnsAsync(true);
        Assert.That(await this.SendAsync().ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        this._login.Verify(s => s.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);
    }

    /// <summary>The resend window survives errors, and a replacement invalidates the earlier code.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LimitsResendsAndReplacesThePreviousCodeAsync()
    {
        await this.SendAsync().ConfigureAwait(false);
        var first = this._code;
        Assert.That(((ObjectResult)await this.SendAsync().ConfigureAwait(false)).StatusCode, Is.EqualTo(429));
        this._now = this._now.AddMinutes(1);
        await this.SendAsync().ConfigureAwait(false);
        Assert.That(this._code, Is.Not.EqualTo(first));
        Assert.That(await this.ConfirmAsync(first).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(await this.ConfirmAsync(this._code).ConfigureAwait(false), Is.InstanceOf<OkObjectResult>());
    }

    /// <summary>Expired, locked and password-invalidated challenges cannot change email.</summary>
    /// <param name="kind">The invalidation to simulate without fixed sleeps.</param>
    /// <returns>The test task.</returns>
    [TestCase("expired")]
    [TestCase("attempts")]
    [TestCase("password")]
    public async Task RejectsInvalidatedChallengesAsync(string kind)
    {
        await this.SendAsync().ConfigureAwait(false);
        if (kind == "expired")
        {
            this._now = this._now.AddMinutes(15);
        }
        else if (kind == "attempts")
        {
            for (var i = 0; i < 5; i++)
            {
                Assert.That(await this.ConfirmAsync("wrong").ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
            }
        }
        else
        {
            this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("replacement", 4);
        }

        var password = kind == "password" ? "replacement" : "secret";
        Assert.That(await this._controller.ConfirmAsync(new("ann", password, Code: this._code)).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(this._account.EMail, Is.EqualTo("old@example.test"));
    }

    /// <summary>Failed SMTP delivery does not make a pending confirmation usable or expose SMTP errors.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RefusesFailedDeliveryAndRetainsCooldownAsync()
    {
        this._mail.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new SmtpException("private SMTP diagnostic"));
        var result = (ObjectResult)await this.SendAsync().ConfigureAwait(false);
        Assert.That(result.StatusCode, Is.EqualTo(503));
        Assert.That(JsonSerializer.Serialize(result.Value), Does.Not.Contain("private SMTP diagnostic"));
        Assert.That(this._challenges.Read(this._account.GetId())!.State, Is.EqualTo("failed"));
        Assert.That(((ObjectResult)await this.SendAsync().ConfigureAwait(false)).StatusCode, Is.EqualTo(429));
        this._login.Verify(s => s.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Exactly(2));
    }

    /// <summary>A failed account save retains a retryable challenge and restores the local email value.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RecoversAfterAnAccountSaveRefusalAsync()
    {
        await this.SendAsync().ConfigureAwait(false);
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.GetAccountByLoginNameAsync("ann", "secret", It.IsAny<CancellationToken>())).ReturnsAsync(this._account);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var provider = new Mock<IPersistenceContextProvider>();
        provider.Setup(p => p.CreateNewPlayerContext(this._configuration)).Returns(context.Object);
        this._controller = this.CreateController(provider.Object);
        Assert.That(((ObjectResult)await this.ConfirmAsync(this._code).ConfigureAwait(false)).StatusCode, Is.EqualTo(503));
        Assert.That(this._account.EMail, Is.EqualTo("old@example.test"));
        Assert.That(this._challenges.Read(this._account.GetId())!.State, Is.EqualTo("applying"));
        this._controller = this.CreateController();
        Assert.That(await this.ConfirmAsync(this._code).ConfigureAwait(false), Is.InstanceOf<OkObjectResult>());
    }

    /// <summary>A lost final challenge write is recoverable after the email has already been saved.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RecoversAfterTheEmailWasSavedAsync()
    {
        await this.SendAsync().ConfigureAwait(false);
        this._failConsumedOnce = true;
        Assert.ThrowsAsync<IOException>(async () => await this.ConfirmAsync(this._code).ConfigureAwait(false));
        Assert.That(this._account.EMail, Is.EqualTo("new@example.test"));
        Assert.That(this._challenges.Read(this._account.GetId())!.State, Is.EqualTo("applying"));
        Assert.That(await this.ConfirmAsync(this._code).ConfigureAwait(false), Is.InstanceOf<OkObjectResult>());
        Assert.That(await this.ConfirmAsync(this._code).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
    }

    /// <summary>Disabled mail prevents sending while state remains readable by the owner.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RespectsDisabledMailAsync()
    {
        this._settings.Save(this._settings.Read() with { Enabled = false }, null);
        var state = (OkObjectResult)await this._controller.StateAsync(new("ann", "secret")).ConfigureAwait(false);
        Assert.That(JsonSerializer.Serialize(state.Value), Does.Contain("old@example.test").And.Contain("false").And.Not.Contain("secret"));
        Assert.That(((ObjectResult)await this.SendAsync().ConfigureAwait(false)).StatusCode, Is.EqualTo(503));
    }

    /// <summary>Malformed, injected and unchanged addresses never result in a message.</summary>
    /// <param name="address">The invalid requested mailbox.</param>
    /// <returns>The test task.</returns>
    [TestCase("old@example.test")]
    [TestCase("Name <new@example.test>")]
    [TestCase("new@example.test\r\nBcc: other@example.test")]
    [TestCase("not an email")]
    public async Task RejectsInvalidNewAddressesAsync(string address)
    {
        Assert.That(await this._controller.RequestAsync(new("ann", "secret", address)).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        this._mail.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        this._login.Verify(s => s.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);
    }

    private WebsiteAccountEmailController CreateController(IPersistenceContextProvider? persistence = null) => new(
        persistence ?? this._persistence, this._source.Object, this._login.Object, this._settings, this._challenges, this._mail.Object,
        this._clock.Object, NullLogger<WebsiteAccountEmailController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private ValueTask<IActionResult> SendAsync() => this._controller.RequestAsync(new("ann", "secret", "new@example.test", Language: "vi"));

    private ValueTask<IActionResult> ConfirmAsync(string code) => this._controller.ConfirmAsync(new("ann", "secret", Code: code));
}
