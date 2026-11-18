// <copyright file="NewsPlugInTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using Moq;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.News;
using HNMO.GameLogic.Views;

/// <summary>
/// Unit tests for <see cref="NewsStore"/>, <see cref="NewsStateWriter"/>, and <see cref="NewsPlugIn"/>.
/// </summary>
[TestFixture]
public class NewsPlugInTests
{
    /// <summary>Verifies pinned sorting, scheduled-post filtering, URL sanitization, and delete.</summary>
    [Test]
    public void StoreSortsPinnedFirstSanitizesImagesAndHidesFuturePosts()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-news-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new NewsStore(tempFile);
            var now = DateTime.UtcNow;

            var oldRegular = store.Upsert(new NewsPost
            {
                TitleEn = "Old update",
                TitleVi = "Cap nhat cu",
                BodyEn = "Older notes",
                PublishAtUtc = now.AddHours(-3),
                IsPinned = false,
                ImageUrl = "/images/ok.png",
            });

            var pinnedOlder = store.Upsert(new NewsPost
            {
                TitleEn = "Pinned maintenance",
                TitleVi = "Bao tri ghim",
                BodyEn = "Sydney schedule",
                PublishAtUtc = now.AddHours(-5),
                IsPinned = true,
                ImageUrl = "javascript:alert(1)",
            });

            var scheduledFuture = store.Upsert(new NewsPost
            {
                TitleEn = "Future event",
                BodyEn = "Not yet visible",
                PublishAtUtc = now.AddHours(4),
                IsPinned = false,
            });

            Assert.That(pinnedOlder.ImageUrl, Is.EqualTo(string.Empty));
            Assert.That(oldRegular.ImageUrl, Is.EqualTo("/images/ok.png"));

            var all = store.ListAll();
            Assert.That(all.Select(p => p.Id), Is.EqualTo(new[] { pinnedOlder.Id, scheduledFuture.Id, oldRegular.Id }));

            var published = store.ListPublished(now);
            Assert.That(published.Select(p => p.Id), Is.EqualTo(new[] { pinnedOlder.Id, oldRegular.Id }));

            Assert.That(store.Delete(oldRegular.Id), Is.True);
            Assert.That(store.Get(oldRegular.Id), Is.Null);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>Verifies entering world pushes latest published notice packet JSON via <see cref="IHnmoNewsViewPlugIn"/>.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task EnteringWorldAndBroadcastInvokeNewsViewPlugIn()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-news-plugin-{Guid.NewGuid():N}.json");
        try
        {
            var store = NewsStore.Open(tempFile);
            var saved = store.Upsert(new NewsPost
            {
                TitleEn = "Weekend Double EXP",
                TitleVi = "Cuoi tuan x2 EXP",
                BodyEn = "Starts 20:00 Sydney",
                BodyVi = "Bat dau 20:00 gio Sydney",
                PublishAtUtc = DateTime.UtcNow.AddMinutes(-5),
                IsPinned = true,
            });

            var player = await PlayerTestHelper.CreatePlayerAsync();
            var viewMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IHnmoNewsViewPlugIn>()!);
            string? capturedLoginJson = null;
            viewMock
                .Setup(v => v.ShowNewsNoticeAsync(It.IsAny<string>()))
                .Callback<string>(json => capturedLoginJson = json)
                .Returns(ValueTask.CompletedTask);

            var plugIn = new NewsPlugIn
            {
                Configuration = new NewsConfiguration
                {
                    FileName = tempFile,
                    ChatNoticeOnEnterWorld = true,
                },
            };

            await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen);
            await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated);
            await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection);
            await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld);

            await plugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld);

            Assert.That(capturedLoginJson, Is.Not.Null);
            using var doc = JsonDocument.Parse(capturedLoginJson!);
            Assert.That(doc.RootElement.GetProperty("fresh").GetBoolean(), Is.False);
            Assert.That(doc.RootElement.GetProperty("latest").GetProperty("id").GetString(), Is.EqualTo(saved.Id));

            var contextMock = new Mock<IGameContext>();
            contextMock
                .Setup(c => c.ForEachPlayerAsync(It.IsAny<Func<Player, Task>>()))
                .Returns<Func<Player, Task>>(action => new ValueTask(action(player)));

            await NewsPlugIn.BroadcastPostAsync(contextMock.Object, saved);
            using var broadcastDoc = JsonDocument.Parse(capturedLoginJson!);
            Assert.That(broadcastDoc.RootElement.GetProperty("fresh").GetBoolean(), Is.True);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
