// <copyright file="BugReportsPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.News;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// HNMO's bug reports (owner's choice, 2026-11-20): a player sends a report from the game (the 🐞 window; up to 5 a
/// Sydney day), the server's owner reads it on the admin panel's "Bug reports" page, sets its status, answers it (the
/// player sees the answer in the window and when entering the game), may reward a confirmed bug with coins (the owner
/// chooses) and may send it to the HNMO developer's Discord.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.BugReportsPlugIn_Name), Description = nameof(Properties.PlugInResources.BugReportsPlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("5E9C1A47-2D83-4B6F-9A05-C7E3B1D84F29")]
public sealed class BugReportsPlugIn : IFeaturePlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<BugReportsConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>The name in the coins history.</summary>
    public const string GrantedBy = "HNMO bug reports";

    private static readonly TimeZoneInfo Sydney = NewsStore.ResolveSydneyZone();

    private readonly object _storeGate = new();
    private BugReportStore? _store;
    private string? _storeFile;

    /// <inheritdoc />
    public BugReportsConfiguration? Configuration { get; set; }

    private BugReportsConfiguration Settings => this.Configuration ?? new BugReportsConfiguration();

    private BugReportStore Store
    {
        get
        {
            // One store per file (a blank file name keeps the reports in one temporary file of this plug-in).
            lock (this._storeGate)
            {
                var file = this.Settings.FileName ?? string.Empty;
                if (this._store is null || this._storeFile != file)
                {
                    this._store = BugReportStore.Open(file);
                    this._storeFile = file;
                }

                return this._store;
            }
        }
    }

    /// <summary>Gets the active plug-in of a game context.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in, or <c>null</c>.</returns>
    public static BugReportsPlugIn? Get(IGameContext? gameContext) => gameContext?.FeaturePlugIns.GetPlugIn<BugReportsPlugIn>();

    /// <summary>The time in Sydney as text.</summary>
    /// <param name="utc">The time (UTC).</param>
    /// <returns>The text.</returns>
    public static string SydneyText(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Sydney).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>What the developer's hub gets of a report: no login, e-mail or IP; the character only when allowed.</summary>
    /// <param name="report">The report.</param>
    /// <param name="shareCharacter">Whether to name the character.</param>
    /// <returns>The message.</returns>
    public static System.Text.Json.Nodes.JsonObject HubMessage(BugReport report, bool shareCharacter)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new System.Text.Json.Nodes.JsonObject
        {
            ["kind"] = "bug",
            ["id"] = report.Id,
            ["category"] = report.Category,
            ["text"] = report.Text,
            ["server"] = report.Server,
            ["place"] = report.Place,
            ["status"] = report.Status.ToString(),
            ["info"] = report.Info,
            ["reply"] = report.Reply ?? string.Empty,
            ["character"] = shareCharacter ? report.Character : string.Empty,
            ["created"] = SydneyText(report.CreatedUtc) + " Sydney",
        };
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new BugReportsConfiguration();

    /// <summary>Sends a player's report.</summary>
    /// <param name="player">The player.</param>
    /// <param name="category">The category.</param>
    /// <param name="text">What the player wrote.</param>
    /// <param name="info">What the game added.</param>
    /// <returns>The EN and VI message, and whether it was sent.</returns>
    public (string En, string Vi, bool Sent) Submit(Player player, string? category, string? text, string? info)
    {
        if (player.Account?.LoginName is not { Length: > 0 } login || player.SelectedCharacter is not { } character)
        {
            return ("Enter the game first.", "Hãy vào game trước.", false);
        }

        var serverId = (player.GameContext as IGameServerContext)?.Id ?? 0;
        var report = new BugReport
        {
            Login = login,
            Character = character.Name ?? string.Empty,
            Server = $"{HnmoSeasonInstance.Current} server {serverId}",
            Place = player.CurrentMap is { } map ? $"{map.Definition.Name.ValueInNeutralLanguage} ({player.Position.X}, {player.Position.Y})" : string.Empty,
            Category = (category ?? "other").Trim().ToLowerInvariant(),
            Text = text ?? string.Empty,
            Info = Clean(info),
        };
        var settings = this.Settings;
        var (added, en, vi) = this.Store.Mutate(data =>
        {
            var result = BugReportEngine.Submit(data, report, settings, DateTime.UtcNow, Sydney);
            return (result.Report is not null, result);
        });
        if (added is not null)
        {
            player.Logger.LogInformation("Bug report {Id} from {Login}/{Character} ({Category}).", added.Id, login, added.Character, added.Category);
        }

        return (en, vi, added is not null);
    }

    /// <summary>The window's state for a player: reports left today, the categories and the player's reports.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The values to put in the JSON.</returns>
    public object StateOf(Player player)
    {
        var login = player.Account?.LoginName ?? string.Empty;
        var settings = this.Settings;
        return this.Store.Read(data => new
        {
            left = BugReportEngine.LeftToday(data, login, settings, DateTime.UtcNow, Sydney),
            maxLength = settings.MaxTextLength,
            categories = BugReportEngine.Categories,
            reward = settings.RewardConfirmedBugs && settings.RewardAmount > 0 ? $"{settings.RewardAmount} {settings.RewardCoinType}" : null,
            mine = BugReportEngine.Mine(data, login).Select(r => new
            {
                id = r.Id,
                category = r.Category,
                text = r.Text.Length > 200 ? r.Text[..200] + "…" : r.Text,
                status = r.Status.ToString(),
                duplicateOf = r.DuplicateOf,
                reply = r.Reply,
                sent = SydneyText(r.CreatedUtc),
                reward = r.RewardGiven,
            }).ToList(),
        });
    }

    /// <summary>Sets a report's status (admin panel); a confirmed or fixed bug gives its reward once, if the owner chose one.</summary>
    /// <param name="gameContexts">The game servers of this program (the first gives the coins; the player is told on whichever they play).</param>
    /// <param name="id">The report.</param>
    /// <param name="status">The status.</param>
    /// <param name="duplicateOf">For a duplicate: the report it repeats.</param>
    /// <returns>Whether it was set.</returns>
    public async ValueTask<bool> SetStatusAsync(IReadOnlyList<IGameContext> gameContexts, string id, BugReportStatus status, string? duplicateOf = null)
    {
        ArgumentNullException.ThrowIfNull(gameContexts);
        var settings = this.Settings;
        var report = this.Store.Mutate(data =>
        {
            var r = BugReportEngine.SetStatus(data, id, status, duplicateOf);
            return (r is not null, r);
        });
        if (report is null)
        {
            return false;
        }

        if (status is BugReportStatus.Confirmed or BugReportStatus.Fixed && settings.RewardConfirmedBugs && settings.RewardAmount > 0 && report.RewardGiven == 0
            && gameContexts.FirstOrDefault() is { } gameContext)
        {
            var grant = new CoinGrant(report.Login, settings.RewardCoinType, settings.RewardAmount, $"Bug report {report.Id}", $"hnmo-bug-{report.Id}-{report.CreatedUtc.Ticks}");
            if (await MissionCoinGranter.GrantAsync(gameContext, grant, GrantedBy).ConfigureAwait(false))
            {
                this.Store.Mutate(data =>
                {
                    var saved = BugReportEngine.Find(data, report.Id);
                    saved?.RewardGiven = settings.RewardAmount;
                    return (saved is not null, true);
                });
            }
        }

        await this.TellAsync(gameContexts, report.Login).ConfigureAwait(false);
        return true;
    }

    /// <summary>Answers a report (admin panel); the player sees it at once when online.</summary>
    /// <param name="gameContexts">The game servers of this program.</param>
    /// <param name="id">The report.</param>
    /// <param name="text">The answer.</param>
    /// <returns>Whether it was saved.</returns>
    public async ValueTask<bool> ReplyAsync(IReadOnlyList<IGameContext> gameContexts, string id, string text)
    {
        var report = this.Store.Mutate(data =>
        {
            var r = BugReportEngine.Reply(data, id, text, DateTime.UtcNow);
            return (r is not null, r);
        });
        if (report is null)
        {
            return false;
        }

        await this.TellAsync(gameContexts, report.Login).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Puts a report in the queue for the HNMO developer (admin panel): it goes with the server's next weekly report (the
    /// hub takes one a week). A Discord link, if set, gets a notice at once.
    /// </summary>
    /// <param name="id">The report.</param>
    /// <param name="licensee">Whom the server's license is for.</param>
    /// <returns>Whether it was queued, and an error.</returns>
    public async Task<(bool Ok, string? Error)> ForwardAsync(string id, string? licensee)
    {
        var settings = this.Settings;
        var (report, alreadyQueued) = this.Store.Mutate(data =>
        {
            var found = BugReportEngine.Find(data, id);
            if (found is null || found.QueuedUtc is not null)
            {
                return (false, (found, found is not null));
            }

            found.QueuedUtc = DateTime.UtcNow;
            return (true, (found, false));
        });
        if (report is null)
        {
            return (false, "There's no such report.");
        }

        if (alreadyQueued)
        {
            return (false, "It is already waiting for the weekly report.");
        }

        if (BugReportForwarder.IsDiscordWebhook(settings.DeveloperWebhookUrl))
        {
            await BugReportForwarder.SendAsync(settings.DeveloperWebhookUrl, report, licensee, settings.ShareCharacterNames).ConfigureAwait(false);
        }

        return (true, null);
    }

    /// <summary>
    /// The reports and automatic error groups queued for the HNMO developer and not sent yet (the weekly report takes
    /// them), at most 50 of each.
    /// </summary>
    /// <returns>The hub's messages, with their ids.</returns>
    public List<(string Id, System.Text.Json.Nodes.JsonObject Message)> QueuedForDeveloper()
    {
        var share = this.Settings.ShareCharacterNames;
        return this.Store.Read(data => data.Reports
            .Where(r => r.QueuedUtc is not null && r.ForwardedUtc is null)
            .Take(50)
            .Select(r => (r.Id, HubMessage(r, share)))
            .Concat(data.AutoGroups
                .Where(g => g.QueuedUtc is not null && g.ForwardedUtc is null)
                .Take(50)
                .Select(g => (g.Id, AutoErrorEngine.HubMessage(g, share, SydneyText))))
            .ToList());
    }

    /// <summary>Marks queued reports and automatic error groups as sent (the weekly report was taken).</summary>
    /// <param name="ids">The reports and groups.</param>
    public void MarkSentToDeveloper(IReadOnlyCollection<string> ids)
    {
        var now = DateTime.UtcNow;
        this.Store.Mutate(data =>
        {
            var changed = false;
            foreach (var report in data.Reports.Where(r => ids.Contains(r.Id)))
            {
                report.ForwardedUtc = now;
                changed = true;
            }

            foreach (var group in data.AutoGroups.Where(g => ids.Contains(g.Id)))
            {
                group.ForwardedUtc = now;
                changed = true;
            }

            return (changed, true);
        });
    }

    /// <summary>
    /// Keeps an automatic error report of the web client (HNMO extra; owner 2026-11-27: they go to this server's owner
    /// only). The login is kept for the caps and the count of accounts only.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="fingerprint">The fingerprint the client computed.</param>
    /// <param name="sample">The report.</param>
    /// <returns>What happened.</returns>
    public AutoErrorEngine.Outcome SubmitAutomatic(Player player, string? fingerprint, AutoErrorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (player.Account?.LoginName is not { Length: > 0 } login)
        {
            return AutoErrorEngine.Outcome.Invalid;
        }

        sample.Character = player.SelectedCharacter?.Name ?? string.Empty;
        if (player.CurrentMap is { } map)
        {
            // The server's name of the map, not the client's number.
            sample.Map = map.Definition.Name.ValueInNeutralLanguage;
        }

        var settings = this.Settings;
        var (outcome, group) = this.Store.Mutate(data =>
        {
            var result = AutoErrorEngine.Submit(data, login, fingerprint, sample, settings, DateTime.UtcNow, Sydney);
            return (result.Group is not null, result);
        });
        if (outcome == AutoErrorEngine.Outcome.NewGroup && group is not null)
        {
            player.Logger.LogInformation("Automatic error report {Id} ({Kind}): {Message}", group.Id, group.Kind, group.Message);
        }

        return outcome;
    }

    /// <summary>The groups of automatic error reports, most reports first (admin panel).</summary>
    /// <returns>The groups.</returns>
    public List<AutoErrorGroup> AutoGroups() => this.Store.Read(AutoErrorEngine.ByCount);

    /// <summary>Marks a group of automatic error reports fixed, ignored or open (admin panel).</summary>
    /// <param name="id">The group.</param>
    /// <param name="status">The status.</param>
    /// <returns>Whether it was set.</returns>
    public bool SetAutoStatus(string id, AutoErrorStatus status) =>
        this.Store.Mutate(data =>
        {
            var group = AutoErrorEngine.SetStatus(data, id, status);
            return (group is not null, group is not null);
        });

    /// <summary>
    /// Puts a group of automatic error reports in the queue for the HNMO developer (admin panel): it goes with the
    /// server's next weekly report, without any login or IP.
    /// </summary>
    /// <param name="id">The group.</param>
    /// <returns>Whether it was queued, and an error.</returns>
    public (bool Ok, string? Error) ForwardAuto(string id)
    {
        var (found, alreadyQueued) = this.Store.Mutate(data =>
        {
            var group = AutoErrorEngine.Find(data, id);
            if (group is null || group.QueuedUtc is not null)
            {
                return (false, (group is not null, group is not null));
            }

            group.QueuedUtc = DateTime.UtcNow;
            return (true, (true, false));
        });
        return !found ? (false, "There's no such error.") : alreadyQueued ? (false, "It is already waiting for the weekly report.") : (true, null);
    }

    /// <summary>All reports, newest first (admin panel).</summary>
    /// <returns>The reports.</returns>
    public List<BugReport> All() => this.Store.Read(data => data.Reports.AsEnumerable().Reverse().ToList());

    /// <summary>A player entering the game sees news about their reports.</summary>
    /// <param name="player">The player.</param>
    /// <param name="previousState">The previous state.</param>
    /// <param name="currentState">The current state.</param>
    /// <returns>The task.</returns>
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.Account?.LoginName is { Length: > 0 } login)
        {
            await this.TellPlayerAsync(player, login).ConfigureAwait(false);
        }
    }

    private static string Clean(string? info)
    {
        if (string.IsNullOrEmpty(info))
        {
            return string.Empty;
        }

        // The game adds plain details; nothing which looks like a password or a ticket is kept.
        var lines = info.Split('\n').Where(l => !l.Contains("password", StringComparison.OrdinalIgnoreCase) && !l.Contains("ticket", StringComparison.OrdinalIgnoreCase));
        return string.Join('\n', lines).Trim();
    }

    private static string StatusText(BugReport report, bool vi) => report.Status switch
    {
        BugReportStatus.Confirmed => vi ? "đã được xác nhận" : "was confirmed",
        BugReportStatus.Fixed => vi ? "đã được sửa" : "is fixed",
        BugReportStatus.NotABug => vi ? "không phải lỗi" : "isn't a bug",
        BugReportStatus.Duplicate => vi ? $"trùng với {report.DuplicateOf}" : $"repeats {report.DuplicateOf}",
        _ => vi ? "đang chờ xem" : "is waiting",
    };

    private async ValueTask TellAsync(IReadOnlyList<IGameContext> gameContexts, string login)
    {
        foreach (var gameContext in gameContexts)
        {
            foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
            {
                if (string.Equals(player.Account?.LoginName, login, StringComparison.OrdinalIgnoreCase) && player.PlayerState.CurrentState == PlayerState.EnteredWorld)
                {
                    await this.TellPlayerAsync(player, login).ConfigureAwait(false);
                    return;
                }
            }
        }
    }

    private async ValueTask TellPlayerAsync(Player player, string login)
    {
        var news = this.Store.Mutate(data =>
        {
            var unseen = BugReportEngine.TakeUnseen(data, login);
            return (unseen.Count > 0, unseen);
        });
        var vi = player.Culture.TwoLetterISOLanguageName == "vi";
        foreach (var report in news)
        {
            var text = vi ? $"Báo lỗi {report.Id} {StatusText(report, true)}." : $"Bug report {report.Id} {StatusText(report, false)}.";
            if (report.RewardGiven > 0)
            {
                text += vi ? $" Thưởng: {report.RewardGiven} xu." : $" Reward: {report.RewardGiven} coins.";
            }

            if (!string.IsNullOrWhiteSpace(report.Reply))
            {
                text += vi ? $" Trả lời: {report.Reply}" : $" Answer: {report.Reply}";
            }

            await player.ShowBlueMessageAsync(text).ConfigureAwait(false);
        }
    }
}
