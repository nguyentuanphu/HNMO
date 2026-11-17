// <copyright file="WebsiteMailSettingsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Globalization;
using System.IO;
using System.Net;
using HNMO.GameLogic.PlugIns;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.DataProtection;

/// <summary>Checks protected settings, TLS and both confirmation templates without contacting a mail server.</summary>
[TestFixture]
public class WebsiteMailSettingsTests
{
    private DirectoryInfo _directory = null!;
    private WebsiteMailSettingsStore _store = null!;

    /// <summary>Creates a private unified state file and key ring.</summary>
    [SetUp]
    public void SetUp()
    {
        this._directory = Directory.CreateTempSubdirectory("hnmo-mail-settings-");
        this._store = this.Open();
    }

    /// <summary>Removes this fixture's state and keys.</summary>
    [TearDown]
    public void TearDown() => this._directory.Delete(true);

    /// <summary>Mail starts off and cannot create a submission client.</summary>
    [Test]
    public void StartsDisabled()
    {
        Assert.That(this._store.Read().Enabled, Is.False);
        Assert.Throws<InvalidOperationException>(() => this._store.CreateClient());
    }

    /// <summary>A failed atomic file replacement cannot accidentally enable delivery in the cached unified section.</summary>
    [Test]
    public void FailedSaveLeavesThePreviousSettingsActive()
    {
        var file = Path.Combine(this._directory.FullName, "blocked.json");
        var state = new HnmoStateStore(file);
        Directory.CreateDirectory(file);
        var store = new WebsiteMailSettingsStore(state, DataProtectionProvider.Create(this._directory));
        Assert.Throws<IOException>(() => store.Save(Settings(), "test-secret"));
        Assert.That(store.Read().Enabled, Is.False);
        Assert.That(store.Read().HasPassword, Is.False);
    }

    /// <summary>Only encrypted credentials persist; blank replacements retain them and a new service can decrypt them.</summary>
    [Test]
    public void ProtectsAndRetainsTheSmtpCredential()
    {
        this._store.Save(Settings(), "smtp-test-secret");
        var form = this._store.Read();
        Assert.That(form.ProtectedPassword, Is.Empty);
        Assert.That(form.HasPassword, Is.True);
        Assert.That(File.ReadAllText(this._store.StatePath), Does.Not.Contain("smtp-test-secret"));
        this._store.Save(form, string.Empty);
        var submission = this.Open().CreateClient();
        using var client = submission.Client;
        Assert.That(submission.From, Is.EqualTo("host@example.test"));
        Assert.That(client.EnableSsl, Is.True);
        Assert.That(client.Port, Is.EqualTo(587));
        Assert.That(((NetworkCredential)client.Credentials!).Password, Is.EqualTo("smtp-test-secret"));
        this._store.Save(form with { UserName = string.Empty }, null);
        Assert.That(this._store.Read().HasPassword, Is.False);
    }

    /// <summary>Display-name, multiple-address, newline and malformed inputs cannot be used as sender addresses.</summary>
    /// <param name="address">The invalid address.</param>
    [TestCase("Name <host@example.test>")]
    [TestCase("host@example.test,other@example.test")]
    [TestCase("host@example.test\r\nBcc: other@example.test")]
    [TestCase("not an email")]
    [TestCase("bareword")]
    public void RejectsInvalidAddresses(string address)
    {
        Assert.That(WebsiteMailSettingsStore.IsAddress(address), Is.False);
        Assert.Throws<ArgumentException>(() => this._store.Save(Settings() with { From = address }, null));
        Assert.That(this._store.Read().Enabled, Is.False);
    }

    /// <summary>Each supported language provides a meaningful template with exactly the confirmation code placeholder.</summary>
    /// <param name="language">The supported language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    public void HasBilingualMailTemplates(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        var title = Resources.ResourceManager.GetString(nameof(Resources.WebsiteMailConfirmationSubject), culture);
        var body = Resources.ResourceManager.GetString(nameof(Resources.WebsiteMailConfirmationBody), culture);
        Assert.That(title, Is.Not.Null.And.Not.Empty);
        Assert.That(body, Does.Contain("{0}"));
        Assert.That(string.Format(culture, body!, "test-code"), Does.Contain("test-code").And.Contain("15"));
    }

    private static WebsiteMailSettings Settings() => new() { Enabled = true, Host = "smtp.example.test", From = "host@example.test", UserName = "host" };

    private WebsiteMailSettingsStore Open() => new(
        new HnmoStateStore(Path.Combine(this._directory.FullName, HnmoStateStore.StateFileName)), DataProtectionProvider.Create(this._directory));
}
