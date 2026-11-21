// <copyright file="HnmoUpdate.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using HNMO.Interfaces;
using Microsoft.AspNetCore.Components;

/// <summary>
/// The HNMO update page (owner's choices, 2026-11-21): the running and the offered version, and "Update", which the
/// launcher does (it downloads the changed files, backs up the save game, swaps the files and goes back when the new
/// version doesn't start). The page asks the launcher on this computer, so it works from afar too.
/// </summary>
public partial class HnmoUpdate : ComponentBase, IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CancellationTokenSource _stop = new();
    private Status? _status;
    private string? _error;
    private bool _confirm;

    /// <summary>Gets or sets the launcher's address (tests).</summary>
    internal static Func<string>? LauncherUrlForTests { get; set; }

    private static bool IsVietnamese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    private static string LauncherUrl => LauncherUrlForTests?.Invoke()
        ?? $"http://127.0.0.1:{HostSettingsStore.TryLoad(HostSettingsStore.ResolveDefaultPath(AppContext.BaseDirectory))?.WebPort ?? 8080}";

    /// <inheritdoc />
    public void Dispose()
    {
        this._stop.Cancel();
        this._stop.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.RefreshAsync(true).ConfigureAwait(true);
        _ = this.PollAsync();
    }

    private static string L(string en, string vi) => IsVietnamese ? vi : en;

    private async Task PollAsync()
    {
        try
        {
            while (!this._stop.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), this._stop.Token).ConfigureAwait(true);
                if (this._status?.State is "downloading" or "installing")
                {
                    await this.RefreshAsync(false).ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // left the page
        }
    }

    private async Task RefreshAsync(bool check)
    {
        try
        {
            this._status = await Http.GetFromJsonAsync<Status>(new Uri($"{LauncherUrl}/api/control/update{(check ? "?check=1" : string.Empty)}"), this._stop.Token).ConfigureAwait(true);
            this._error = null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // While installing, the launcher is away for a moment: keep the last state.
            if (this._status?.State != "installing")
            {
                this._error = L("The launcher doesn't answer (is HNMO started from its folder?).", "Chương trình khởi động không trả lời (HNMO có được mở từ thư mục của nó không?).");
            }
        }

        await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(true);
    }

    private async Task StartAsync()
    {
        this._confirm = false;
        try
        {
            using var response = await Http.PostAsync(new Uri($"{LauncherUrl}/api/control/update"), null, this._stop.Token).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                this._error = await response.Content.ReadAsStringAsync(this._stop.Token).ConfigureAwait(true);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            this._error = ex.Message;
        }

        await this.RefreshAsync(false).ConfigureAwait(true);
    }

    /// <summary>The launcher's answer (Updater.cs UpdateStatus).</summary>
    /// <param name="Current">The running version.</param>
    /// <param name="Latest">The offered one.</param>
    /// <param name="Notes">Its notes.</param>
    /// <param name="Available">Whether it is newer.</param>
    /// <param name="State">idle, downloading, installing, done, failed, rolled-back.</param>
    /// <param name="Percent">The progress.</param>
    /// <param name="Message">The last result.</param>
    /// <param name="DownloadBytes">What the update downloads.</param>
    /// <param name="ChangedFiles">How many files change.</param>
    internal sealed record Status(string Current, string? Latest, string? Notes, bool Available, string State, int Percent, string? Message, long DownloadBytes, int ChangedFiles);
}
