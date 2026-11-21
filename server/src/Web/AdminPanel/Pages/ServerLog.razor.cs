// <copyright file="ServerLog.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using HNMO.Interfaces;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;

/// <summary>
/// The page "Server log" (owner's choice, 2026-11-21: no console window): the launcher's, the game server's and the
/// database's logs from <c>save/logs</c>, refreshed every few seconds, and "Stop HNMO", which stops the whole folder
/// (game, database and launcher) the way closing the console window did.
/// </summary>
public partial class ServerLog : ComponentBase, IDisposable
{
    /// <summary>The lines shown at most.</summary>
    internal const int MaxLines = 400;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly CancellationTokenSource _stop = new();
    private IReadOnlyList<LogFileEntry> _files = [];
    private string _file = "launcher.log";
    private IReadOnlyList<string> _lines = [];
    private bool _live = true;
    private bool _confirmStop;
    private bool _stopped;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    private static bool IsVietnamese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    /// <summary>Gets the logs folder: next to the host settings, in <c>save/logs</c>.</summary>
    internal static string LogDirectory =>
        Path.Combine(Path.GetDirectoryName(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory, "logs");

    /// <summary>The last lines of a log file of the folder (not a file outside it).</summary>
    /// <param name="directory">The logs folder.</param>
    /// <param name="fileName">The file's name.</param>
    /// <param name="max">The most lines.</param>
    /// <returns>The lines, oldest first.</returns>
    internal static IReadOnlyList<string> Tail(string directory, string fileName, int max)
    {
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..", StringComparison.Ordinal) || !fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return [];
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        const int window = 256 * 1024;
        if (stream.Length > window)
        {
            stream.Seek(-window, SeekOrigin.End);
        }

        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (stream.Length > window && lines.Count > 0)
        {
            lines.RemoveAt(0); // cut in the middle
        }

        return lines.Where(l => l.Length > 0).TakeLast(max).ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._stop.Cancel();
        this._stop.Dispose();
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this.Read();
        _ = this.RefreshLoopAsync();
    }

    private static string L(string en, string vi) => IsVietnamese ? vi : en;

    private void Read()
    {
        try
        {
            this._files = RollingLogWriter.ListLogFiles(LogDirectory).Where(f => !f.Compressed).ToList();
            if (this._files.Count > 0 && this._files.All(f => f.FileName != this._file))
            {
                this._file = this._files[0].FileName;
            }

            this._lines = Tail(LogDirectory, this._file, MaxLines);
        }
        catch (IOException)
        {
            // Being written: the next refresh reads it.
        }
    }

    private async Task RefreshLoopAsync()
    {
        try
        {
            while (!this._stop.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), this._stop.Token).ConfigureAwait(true);
                if (this._live)
                {
                    this.Read();
                    await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The page closed.
        }
    }

    private void Choose(string file)
    {
        this._file = file;
        this.Read();
    }

    private async Task StopHnmoAsync()
    {
        this._confirmStop = false;
        var port = HostSettingsStore.TryLoad(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory))?.WebPort ?? 8080;
        try
        {
            using var response = await Http.PostAsync(new Uri($"http://127.0.0.1:{port}/api/control/shutdown"), null).ConfigureAwait(true);
            if (response.IsSuccessStatusCode)
            {
                this._stopped = true;
                this.ToastService.ShowSuccess(L("HNMO is stopping: the game, the database and the launcher.", "HNMO đang tắt: game, cơ sở dữ liệu và chương trình khởi động."));
                return;
            }

            this.ToastService.ShowError(L($"The launcher answered {(int)response.StatusCode}.", $"Chương trình khởi động trả lời {(int)response.StatusCode}."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            this.ToastService.ShowError(L("The launcher didn't answer (is HNMO started from its folder?).", "Chương trình khởi động không trả lời (HNMO có được mở từ thư mục của nó không?)."));
        }
    }
}
