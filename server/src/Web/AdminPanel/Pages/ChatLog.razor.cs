// <copyright file="ChatLog.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Chat;
using HNMO.GameLogic.Views;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// The chat log for game masters: the last chat lines of the game servers of this process, newest first,
/// with a search and a live refresh. Operators and administrators can mute the sender of a line.
/// </summary>
public partial class ChatLog : IDisposable
{
    /// <summary>
    /// The most lines which the page shows.
    /// </summary>
    private const int MaximumShown = 500;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private static readonly TimeZoneInfo SydneyTime = FindSydneyTime();

    private readonly CancellationTokenSource _disposeCts = new();
    private IReadOnlyList<ChatLogEntry> _entries = [];
    private string _search = string.Empty;
    private bool _liveRefresh = true;
    private long _shownLastId = -1;
    private bool _canMute;
    private string? _status;
    private bool _statusIsError;

    /// <summary>
    /// Gets or sets the chat log; the one of this process by default.
    /// </summary>
    [Parameter]
    public HNMO.GameLogic.PlugIns.Chat.ChatLog Log { get; set; } = HNMO.GameLogic.PlugIns.Chat.ChatLog.Shared;

    [Inject]
    private AdminPolicyGuard PolicyGuard { get; set; } = null!;

    [Inject]
    private AdminOperationExecutor Executor { get; set; } = null!;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private ILogger<ChatLog> Logger { get; set; } = null!;

    private string Search
    {
        get => this._search;
        set
        {
            this._search = value;
            this.Refresh();
        }
    }

    private bool LiveRefresh
    {
        get => this._liveRefresh;
        set
        {
            this._liveRefresh = value;
            if (value)
            {
                this.Refresh();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._disposeCts.Cancel();
        this._disposeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Formats a time in Sydney time.
    /// </summary>
    /// <param name="timeUtc">The time (UTC).</param>
    /// <returns>The formatted time.</returns>
    internal static string FormatTime(DateTime timeUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(timeUtc, DateTimeKind.Utc), SydneyTime);
        return local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(true);
        this._canMute = await this.PolicyGuard.IsAuthorizedAsync(AdminPolicies.Operator).ConfigureAwait(true);
        this.Refresh();
        _ = this.RefreshPeriodicallyAsync(this._disposeCts.Token);
    }

    private static TimeZoneInfo FindSydneyTime()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string KindText(ChatLogEntry entry)
    {
        var kind = entry.Kind switch
        {
            ChatMessageType.Normal => Resources.ChatLogKindNormal,
            ChatMessageType.Party => Resources.ChatLogKindParty,
            ChatMessageType.Guild => Resources.ChatLogKindGuild,
            ChatMessageType.Alliance => Resources.ChatLogKindAlliance,
            ChatMessageType.Whisper => Resources.ChatLogKindWhisper,
            ChatMessageType.World => Resources.ChatLogKindWorld,
            ChatMessageType.Gens => Resources.ChatLogKindGens,
            ChatMessageType.GlobalNotification => Resources.ChatLogKindNotice,
            _ => entry.Kind.ToString(),
        };
        return entry.Receiver is { Length: > 0 } receiver ? $"{kind} → {receiver}" : kind;
    }

    private void Refresh()
    {
        this._shownLastId = this.Log.LastId;
        this._entries = this.Log.Find(this._search, MaximumShown);
    }

    private async Task RefreshPeriodicallyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(RefreshInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (this._liveRefresh && this.Log.LastId != this._shownLastId)
                {
                    await this.InvokeAsync(() =>
                    {
                        this.Refresh();
                        this.StateHasChanged();
                    }).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The page was closed.
        }
        catch (ObjectDisposedException)
        {
            // The page was closed.
        }
    }

    private async Task MuteAsync(ChatLogEntry entry, TimeSpan duration)
    {
        try
        {
            var until = await this.Executor.ExecuteAsync(
                AdminRoles.Operator,
                "chat_mute",
                new { characterName = entry.Sender, minutes = (int)duration.TotalMinutes },
                async _ =>
                {
                    var configuration = await this.GameConfigurationSource.GetOwnerAsync().ConfigureAwait(false);
                    return await ChatMute.MuteAsync(this.PersistenceContextProvider, configuration, entry.Sender, duration).ConfigureAwait(false);
                },
                this._disposeCts.Token).ConfigureAwait(true);
            if (until is { } time)
            {
                this.SetStatus(string.Format(CultureInfo.CurrentCulture, Resources.ChatLogMuted, entry.Sender, FormatTime(time)), false);
            }
            else
            {
                this.SetStatus(string.Format(CultureInfo.CurrentCulture, Resources.ChatLogCharacterNotFound, entry.Sender), true);
            }
        }
        catch (AdminOperationException ex)
        {
            this.SetStatus(ex.Message, true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Muting {Character} failed.", entry.Sender);
            this.SetStatus(string.Format(CultureInfo.CurrentCulture, Resources.ChatLogMuteFailed, entry.Sender), true);
        }
    }

    private void SetStatus(string text, bool isError)
    {
        this._status = text;
        this._statusIsError = isError;
    }
}
