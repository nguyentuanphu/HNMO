// The website's "Download HNMO" link (bridge/LatestReleaseCache.cs): the hub's /api/latest is asked once an hour, and
// 5 minutes after a failure, whatever the number of visitors (each hub call is a paid function call).
using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

[TestFixture]
public class LatestReleaseCacheTests
{
    [Test]
    public async Task TheHubIsAskedOnceAnHourAsync()
    {
        var cache = new LatestReleaseCache();
        var calls = 0;
        Task<string?> Fetch()
        {
            calls++;
            return Task.FromResult<string?>("https://downloads.example/HNMO.zip");
        }

        var now = new DateTime(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);
        for (var visitor = 0; visitor < 500; visitor++)
        {
            Assert.That(await cache.GetAsync(Fetch, now.AddSeconds(visitor)), Is.EqualTo("https://downloads.example/HNMO.zip"));
        }

        Assert.That(calls, Is.EqualTo(1));
        await cache.GetAsync(Fetch, now.AddMinutes(61));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task AFailureIsKeptFiveMinutesAsync()
    {
        var cache = new LatestReleaseCache();
        var calls = 0;
        Task<string?> Fail()
        {
            calls++;
            return Task.FromResult<string?>(null);
        }

        var now = new DateTime(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(await cache.GetAsync(Fail, now), Is.Null);
        Assert.That(await cache.GetAsync(Fail, now.AddMinutes(4)), Is.Null);
        Assert.That(calls, Is.EqualTo(1));
        await cache.GetAsync(Fail, now.AddMinutes(6));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task VisitorsAtOnceMakeOneCallAsync()
    {
        var cache = new LatestReleaseCache();
        var calls = 0;
        var gate = new TaskCompletionSource<string?>();
        Task<string?> Slow()
        {
            calls++;
            return gate.Task;
        }

        var now = DateTime.UtcNow;
        var visitors = new Task<string?>[20];
        for (var i = 0; i < visitors.Length; i++)
        {
            visitors[i] = cache.GetAsync(Slow, now);
        }

        gate.SetResult("https://downloads.example/HNMO.zip");
        Assert.That(await Task.WhenAll(visitors), Has.All.EqualTo("https://downloads.example/HNMO.zip"));
        Assert.That(calls, Is.EqualTo(1));
    }
}
