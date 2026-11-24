// <copyright file="WebsiteRankingsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>Checks the public ranking and character-ban visibility policy.</summary>
[TestFixture]
public sealed class WebsiteRankingsTests
{
    /// <summary>Both outputs honor account privacy and the ranking excludes banned accounts.</summary>
    [Test]
    public void AppliesCurrentVisibilityAndBanState()
    {
        var optedOut = new HashSet<string> { WebsiteTrackerPrivacyStore.Key("HiddenAccount") };
        Assert.That(new WebsiteConfiguration().PublishBannedCharacters, Is.False);
        Assert.That(WebsiteDetailsController.IsPublicCandidate("Knight", "Owner", AccountState.Normal, optedOut, true), Is.True);
        Assert.That(WebsiteDetailsController.IsPublicCandidate("Knight", "Owner", AccountState.Banned, optedOut, true), Is.False);
        Assert.That(WebsiteDetailsController.IsPublicCandidate("Knight", "Owner", AccountState.TemporarilyBanned, optedOut, true), Is.False);
        Assert.That(WebsiteDetailsController.IsPublicCandidate("Knight", "Owner", AccountState.Banned, optedOut, false), Is.True);
        Assert.That(WebsiteDetailsController.IsPublicCandidate("Knight", "hiddenaccount", AccountState.Normal, optedOut, false), Is.False);
        Assert.That(WebsiteDetailsController.IsPublicCandidate("Owner", "owner", AccountState.Normal, optedOut, true), Is.False);
        Assert.That(WebsiteDetailsController.IsPublicCandidate("Knight", "", AccountState.Normal, optedOut, true), Is.False);
    }
}
