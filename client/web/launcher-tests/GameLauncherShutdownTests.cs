// Unit tests for GameLauncher shutdown cancellation (silent-mode / control shutdown).
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

[TestFixture]
public class GameLauncherShutdownTests
{
    private sealed class StubLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    [Test]
    public async Task RequestShutdownStopCancelsStartupAndMarksLauncherStopped()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-launcher-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var options = new LauncherOptions
            {
                Enabled = true,
                SavePath = Path.Combine(tempDir, "save"),
                ServerPath = Path.Combine(tempDir, "server"),
                PostgresPath = Path.Combine(tempDir, "postgres", "bin"),
            };

            var launcher = new GameLauncher(options, tempDir, new StubLifetime(), NullLogger<GameLauncher>.Instance);
            launcher.RequestShutdownStop();
            await launcher.StopAsync(CancellationToken.None).ConfigureAwait(false);

            // Second call remains idempotent without throwing.
            launcher.RequestShutdownStop();
            Assert.That(launcher.IsLaunching, Is.False);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
