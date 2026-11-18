// <copyright file="BannedWordsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Configuration persistence tests for <see cref="BannedWords"/> and <see cref="BannedWordsPlugIn"/>.
/// </summary>
[TestFixture]
public class BannedWordsPageTests
{
    /// <summary>Verifies Banned Words Filter is off by default and round-trips updated EN/VI lists via <see cref="PlugInSettings"/>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsDefaultOffStateAndPersistsEditableLists()
    {
        var persistence = new InMemoryPersistenceContextProvider();

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(BannedWordsPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (_, activeBefore, savedBefore) = await PlugInSettings.ReadStoredAsync<BannedWordsConfiguration>(
            persistence,
            typeof(BannedWordsPlugIn),
            default).ConfigureAwait(false);
        Assert.That(activeBefore, Is.False);
        Assert.That(savedBefore, Is.Null);

        using (var editCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var stored = (await editCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false))
                .First(r => r.TypeId == typeof(BannedWordsPlugIn).GUID);
            stored.IsActive = true;
            stored.CustomConfiguration = JsonSerializer.Serialize(new BannedWordsConfiguration
            {
                ChatAction = BannedWordChatAction.Block,
                IgnoreVietnameseDiacritics = true,
                EnglishWords = ["badword", "spammer"],
                VietnameseWords = ["từ cấm", "lừa đảo"],
            });
            await editCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, activeAfter, savedAfter) = await PlugInSettings.ReadStoredAsync<BannedWordsConfiguration>(
            persistence,
            typeof(BannedWordsPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(activeAfter, Is.True);
        Assert.That(savedAfter, Is.Not.Null);
        Assert.That(savedAfter!.ChatAction, Is.EqualTo(BannedWordChatAction.Block));
        Assert.That(savedAfter.IgnoreVietnameseDiacritics, Is.True);
        Assert.That(savedAfter.EnglishWords, Is.EqualTo(new[] { "badword", "spammer" }));
        Assert.That(savedAfter.VietnameseWords, Is.EqualTo(new[] { "từ cấm", "lừa đảo" }));
    }
}
