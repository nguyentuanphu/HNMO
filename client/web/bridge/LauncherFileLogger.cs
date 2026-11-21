// <copyright file="LauncherFileLogger.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using HNMO.Interfaces;

/// <summary>
/// Writes what the launcher says (what the console window used to show) to <c>save/logs/launcher.log</c>, so it can be
/// read in the admin panel's "Server log" page and in the control page once there is no console window (owner's
/// choice, 2026-11-21).
/// </summary>
internal sealed class LauncherFileLoggerProvider : ILoggerProvider
{
    /// <summary>The times are Sydney's, as every HNMO log.</summary>
    private static readonly TimeZoneInfo Sydney = ResolveSydney();

    private readonly RollingLogWriter _writer;
    private readonly object _gate = new();

    /// <summary>Initializes a new instance of the <see cref="LauncherFileLoggerProvider"/> class.</summary>
    /// <param name="logDirectory">The logs folder (<c>save/logs</c>).</param>
    public LauncherFileLoggerProvider(string logDirectory)
    {
        this._writer = new RollingLogWriter(logDirectory, "launcher.log");
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose() => this._writer.Dispose();

    private static TimeZoneInfo ResolveSydney()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    private void Write(string line)
    {
        lock (this._gate)
        {
            this._writer.WriteLine(line);
        }
    }

    private sealed class FileLogger(LauncherFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information
            || (logLevel >= LogLevel.Warning && category.StartsWith("Microsoft", StringComparison.Ordinal));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!this.IsEnabled(logLevel) || (category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) && logLevel < LogLevel.Warning)
                || (category.StartsWith("System.Net.Http", StringComparison.Ordinal) && logLevel < LogLevel.Warning))
            {
                return;
            }

            var line = $"{TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Sydney):yyyy-MM-dd HH:mm:ss} [{logLevel}] {category}: {formatter(state, exception)}";
            provider.Write(exception is null ? line : $"{line}{Environment.NewLine}{exception}");
        }
    }
}
