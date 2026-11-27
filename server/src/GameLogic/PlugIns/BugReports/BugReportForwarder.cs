// <copyright file="BugReportForwarder.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Sends a bug report to the HNMO developer's Discord channel (the webhook link the developer gives each server's owner).
/// Only the report and its technical details go: no login, e-mail, IP or password; the character only when the owner
/// allows it. The server is named by its license.
/// </summary>
public static class BugReportForwarder
{
    private static readonly HttpClient DefaultClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>Builds the Discord message of a report.</summary>
    /// <param name="report">The report.</param>
    /// <param name="licensee">Whom the server's license is for.</param>
    /// <param name="shareCharacter">Whether to name the character.</param>
    /// <returns>The message (Discord webhook JSON).</returns>
    public static JsonObject BuildMessage(BugReport report, string? licensee, bool shareCharacter)
    {
        ArgumentNullException.ThrowIfNull(report);
        var fields = new JsonArray
        {
            Field("Server", string.IsNullOrWhiteSpace(licensee) ? "unlicensed" : licensee),
            Field("Game server", report.Server),
            Field("Place", report.Place),
            Field("Status", report.Status.ToString()),
        };
        if (shareCharacter && report.Character.Length > 0)
        {
            fields.Add(Field("Character", report.Character));
        }

        if (report.Info.Length > 0)
        {
            fields.Add(Field("Details", Cut(report.Info, 1000), inline: false));
        }

        if (!string.IsNullOrWhiteSpace(report.Reply))
        {
            fields.Add(Field("Owner's answer", Cut(report.Reply, 1000), inline: false));
        }

        return new JsonObject
        {
            ["username"] = "HNMO bug reports",
            ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() },
            ["embeds"] = new JsonArray
            {
                new JsonObject
                {
                    ["title"] = Cut($"[{report.Category}] {report.Id} · {licensee ?? "unlicensed"}", 250),
                    ["description"] = Cut(report.Text, 3500),
                    ["timestamp"] = report.CreatedUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    ["fields"] = fields,
                },
            },
        };
    }

    /// <summary>Checks that a link is a Discord webhook (nothing else is posted to).</summary>
    /// <param name="url">The link.</param>
    /// <returns><c>true</c>, if it is.</returns>
    public static bool IsDiscordWebhook(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host is "discord.com" or "discordapp.com" or "ptb.discord.com" or "canary.discord.com")
        && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);

    /// <summary>Posts a report to the developer's Discord.</summary>
    /// <param name="webhookUrl">The webhook link.</param>
    /// <param name="report">The report.</param>
    /// <param name="licensee">Whom the server's license is for.</param>
    /// <param name="shareCharacter">Whether to name the character.</param>
    /// <param name="client">The HTTP client (tests); a shared one by default.</param>
    /// <returns>Whether Discord took it, and an error.</returns>
    public static async Task<(bool Ok, string? Error)> SendAsync(string webhookUrl, BugReport report, string? licensee, bool shareCharacter, HttpClient? client = null)
    {
        if (!IsDiscordWebhook(webhookUrl))
        {
            return (false, "The developer's link isn't a Discord webhook link.");
        }

        try
        {
            using var response = await (client ?? DefaultClient).PostAsJsonAsync(webhookUrl.Trim(), BuildMessage(report, licensee, shareCharacter)).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Discord answered {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, ex.Message);
        }
    }

    private static JsonObject Field(string name, string value, bool inline = true) =>
        new() { ["name"] = name, ["value"] = string.IsNullOrWhiteSpace(value) ? "-" : Cut(value, 1000), ["inline"] = inline };

    private static string Cut(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..(max - 1)] + "…";
}
