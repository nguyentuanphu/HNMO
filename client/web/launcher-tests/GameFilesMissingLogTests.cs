// A file the web game asks for that the packs don't hold is answered 404 and noted once per path at Debug level:
// the game looks in a theme's folder before the shared one, beside a model before the Effect folder, and for
// optional files, so these misses are normal and must not fill the launcher log with warnings.
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

[TestFixture]
public class GameFilesMissingLogTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        this._root = Path.Combine(Path.GetTempPath(), $"hnmo-gamefiles-{Guid.NewGuid():N}");
        var data = Path.Combine(this._root, "data");
        Directory.CreateDirectory(Path.Combine(data, "Effect"));
        File.WriteAllText(Path.Combine(data, "Effect", "flare.ktx2"), "texture");
        DataPackArchive.BuildPacks(data, Path.Combine(this._root, "served", "packs"), null);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._root))
        {
            Directory.Delete(this._root, recursive: true);
        }
    }

    [Test]
    public async Task AMissingFileIsNotedOnceAtDebugLevelAndNeverAsAWarning()
    {
        var logger = new ListLogger();
        using var files = new GameFiles(Path.Combine(this._root, "served"), logger);
        Assert.That(files.UsingPacks, Is.True);

        for (var i = 0; i < 3; i++)
        {
            var context = new DefaultHttpContext();
            await files.ServeAsync(context, "Skill/flare.ktx2");
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        }

        var found = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await files.ServeAsync(found, "Effect/flare.ktx2");

        Assert.That(found.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK), "the file the game asks for next is served");
        Assert.That(logger.Entries.Where(e => e.Level >= LogLevel.Warning), Is.Empty);
        Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Debug && e.Message.Contains("Skill/flare.ktx2")), Is.EqualTo(1));
        Assert.That(files.ReportedMissing, Is.EquivalentTo(new[] { "Skill/flare.ktx2" }));
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => this.Entries.Add((logLevel, formatter(state, exception)));
    }
}
