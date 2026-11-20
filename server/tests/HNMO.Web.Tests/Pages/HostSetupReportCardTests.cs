// <copyright file="HostSetupReportCardTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using Bunit;
using HNMO.GameLogic;
using HNMO.Web.AdminPanel.Components;

/// <summary>
/// The setup page's card about the first-setup report (owner, 2026-11-21): it says plainly what goes to the HNMO
/// developer, keeps the host's optional name and contact, and sends the report again when they change after it went.
/// </summary>
[TestFixture]
[NonParallelizable]
public class HostSetupReportCardTests
{
    private BunitContext _context = null!;
    private string? _previousStateFile;
    private string _stateFile = null!;
    private CultureInfo _previousUiCulture = null!;

    /// <summary>A state file of its own and English text.</summary>
    [SetUp]
    public void SetUp()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._previousStateFile = DeveloperHub.StateFile;
        this._stateFile = Path.Combine(Path.GetTempPath(), $"hnmo-hub-{Guid.NewGuid():N}.json");
        DeveloperHub.StateFile = this._stateFile;
        this._context = new BunitContext();
    }

    /// <summary>Puts things back.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        DeveloperHub.StateFile = this._previousStateFile;
        File.Delete(this._stateFile);
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>The notice names what is sent, the IP included, and that player accounts never are.</summary>
    [Test]
    public void TheNoticeSaysWhatIsSent()
    {
        var cut = this._context.Render<HostSetupReportCard>();
        var text = cut.Find("#host-setup-report").TextContent;
        Assert.That(text, Does.Contain("HNMO developer"));
        Assert.That(text, Does.Contain("IP address"));
        Assert.That(text, Does.Contain("Never player accounts"));
        Assert.That(cut.Find("#host-setup-status").TextContent, Does.Contain("Not sent yet"));
    }

    /// <summary>A name and contact saved after the report went: kept, and the report goes again.</summary>
    [Test]
    public void ChangedDetailsAfterTheReportSendItAgain()
    {
        DeveloperHub.ChangeState(s => s.SetupReportedUtc = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc));
        var cut = this._context.Render<HostSetupReportCard>();
        Assert.That(cut.Find("#host-setup-status").TextContent, Does.Contain("Sent on 2026-11-21 11:00 (Sydney time)"));

        cut.Find("#host-setup-name").Change("Minh");
        cut.Find("#host-setup-contact").Change("zalo 0900");
        cut.Find("#host-setup-save").Click();

        var state = DeveloperHub.ReadState();
        Assert.That((state.HostName, state.HostContact), Is.EqualTo(("Minh", "zalo 0900")));
        Assert.That(state.SetupReportedUtc, Is.Null, "sent again");
        Assert.That(cut.Find("#host-setup-status").TextContent, Does.Contain("sent within the hour"));
    }
}
