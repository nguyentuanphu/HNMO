// <copyright file="DeveloperHub.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Nodes;

/// <summary>
/// The HNMO developer's hub (owner's choice, 2026-11-20): it takes the bug reports a server's owner sends on and
/// licence requests, and files them as issues in the developer's private repository. A licensed server is known by its
/// signed licence file, sent along; a server without one may send a few a day. No login, e-mail or IP is sent.
/// </summary>
public static class DeveloperHub
{
    /// <summary>The hub's address unless <c>HNMO_DEVELOPER_HUB</c> names another.</summary>
    public const string DefaultUrl = "https://hnmobug.netlify.app";

    private static readonly HttpClient DefaultClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Gets the hub's address (empty: sending is off).</summary>
    public static string Url => (Environment.GetEnvironmentVariable("HNMO_DEVELOPER_HUB") ?? DefaultUrl).Trim().TrimEnd('/');

    /// <summary>Gets or sets this folder's licence file as text (set at start and when a licence is saved), if any.</summary>
    public static string? LicenceFile { get; set; }

    /// <summary>Gets or sets this computer's id (the one a licence is bound to).</summary>
    public static string MachineId { get; set; } = string.Empty;

    /// <summary>Gets the HNMO version.</summary>
    public static string Version =>
        typeof(DeveloperHub).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? typeof(DeveloperHub).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>Sends a message to the hub.</summary>
    /// <param name="message">The message: a bug report or a licence request (<c>kind</c> says which).</param>
    /// <param name="client">The HTTP client (tests).</param>
    /// <param name="url">The hub's address (tests); <see cref="Url"/> by default.</param>
    /// <returns>Whether the hub took it, and its EN/VI answer.</returns>
    public static async Task<(bool Ok, string En, string Vi)> SendAsync(JsonObject message, HttpClient? client = null, string? url = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        var hub = (url ?? Url).TrimEnd('/');
        if (hub.Length == 0)
        {
            return (false, "Sending to the HNMO developer is off on this server.", "Server này đã tắt gửi tới nhà phát triển HNMO.");
        }

        if (MachineId.Length == 0)
        {
            return (false, "This computer's id isn't known yet; restart HNMO.", "Chưa biết mã máy này; hãy khởi động lại HNMO.");
        }

        message["machine"] = MachineId;
        message["version"] = Version;
        message["licence"] = LicenceFile;
        try
        {
            using var response = await (client ?? DefaultClient).PostAsJsonAsync($"{hub}/api/report", message).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Sent to the HNMO developer.", "Đã gửi tới nhà phát triển HNMO.");
            }

            if ((int)response.StatusCode == 429)
            {
                return (false, "This server sent its reports for today; more tomorrow (Sydney time).", "Server này đã gửi hết lượt hôm nay; ngày mai (giờ Sydney) gửi tiếp.");
            }

            return (false, $"The hub answered {(int)response.StatusCode}.", $"Hub trả lời {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, $"The hub can't be reached: {ex.Message}", $"Không kết nối được hub: {ex.Message}");
        }
    }
}
