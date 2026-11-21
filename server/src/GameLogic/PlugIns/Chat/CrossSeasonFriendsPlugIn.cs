// <copyright file="CrossSeasonFriendsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.GameLogic.Properties;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Messenger;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Friends across seasons (owner's choice, 2026-11-21): every HNMO program (Season 6.3, Season 9, the Arena) publishes
/// who plays on it in the shared database. A friend playing in another program shows in the friend list as "Season 9"
/// (and so on) instead of offline, and whispers reach a character playing in another program. Accounts are the same in
/// every program, so a friend is found by their account whatever character they play there. No trading between seasons.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CrossSeasonFriendsPlugIn_Name), Description = nameof(PlugInResources.CrossSeasonFriendsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E7B42C19-8D53-4A6F-9C21-3F5A8D0B6E72")]
public sealed class CrossSeasonFriendsPlugIn : IPeriodicTaskPlugIn
{
    /// <summary>The friend list's "server" for a friend playing in Season 6.3 (the web game shows it by name).</summary>
    public const byte SeasonSixServer = 0xE0;

    /// <summary>The friend list's "server" for a friend playing in Season 9.</summary>
    public const byte SeasonNineServer = 0xE1;

    /// <summary>The friend list's "server" for a friend playing in the Arena.</summary>
    public const byte ArenaServer = 0xE2;

    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();
    private static readonly ConditionalWeakTable<Player, Dictionary<string, byte>> Shown = new();
    private static readonly ConcurrentDictionary<string, (string? Login, DateTime ReadUtc)> Logins = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheGate = new();
    private static IReadOnlyList<OnlinePresence> _elsewhere = [];
    private static DateTime _elsewhereReadUtc = DateTime.MinValue;

    private readonly ConcurrentDictionary<byte, DateTime> _nextPublishUtc = new();
    private readonly ConcurrentDictionary<byte, DateTime> _nextFriendsUtc = new();
    private DateTime _nextWhispersUtc;
    private int _whispering;

    /// <summary>The friend list's "server" of a program.</summary>
    /// <param name="season">The program ("s6", "s9" or "arena").</param>
    /// <returns>The value.</returns>
    public static byte ServerOf(string season) => season switch
    {
        HnmoSeasonInstance.SeasonNine => SeasonNineServer,
        HnmoSeasonInstance.Arena => ArenaServer,
        _ => SeasonSixServer,
    };

    /// <summary>A program's name, for the players.</summary>
    /// <param name="season">The program.</param>
    /// <returns>The name.</returns>
    public static string NameOf(string season) => season switch
    {
        HnmoSeasonInstance.SeasonNine => "Season 9",
        HnmoSeasonInstance.Arena => "Arena",
        _ => "Season 6.3",
    };

    /// <summary>Who plays in the other programs (read at most every 10 seconds).</summary>
    /// <returns>The accounts playing elsewhere.</returns>
    public static IReadOnlyList<OnlinePresence> Elsewhere()
    {
        lock (CacheGate)
        {
            if (DateTime.UtcNow - _elsewhereReadUtc < TimeSpan.FromSeconds(10))
            {
                return _elsewhere;
            }

            _elsewhereReadUtc = DateTime.UtcNow;
            try
            {
                _elsewhere = ArenaShared.Current?.OnlineElsewhere(HnmoSeasonInstance.Current) ?? [];
            }
            catch (Exception)
            {
                // The database can't be reached: nobody elsewhere for now.
                _elsewhere = [];
            }

            return _elsewhere;
        }
    }

    /// <summary>Forgets who plays elsewhere, so that the next read asks the database again (tests).</summary>
    internal static void ForgetElsewhere()
    {
        lock (CacheGate)
        {
            _elsewhereReadUtc = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Sends a whisper to a character playing in another program: by the character's name there, or by the account of a
    /// character of this program (a friend's name here). Tells the sender where it went.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="name">The name the sender wrote.</param>
    /// <param name="message">The text.</param>
    /// <returns><c>true</c>, if the whisper went to another program.</returns>
    public static async ValueTask<bool> TrySendAsync(Player sender, string name, string message)
    {
        if (ArenaShared.Current is not { } shared || sender.SelectedCharacter?.Name is not { } from)
        {
            return false;
        }

        var elsewhere = Elsewhere();
        var target = elsewhere.FirstOrDefault(p => string.Equals(p.Character, name, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            if (await LoginOfAsync(sender.GameContext, name).ConfigureAwait(false) is { } login)
            {
                target = elsewhere.FirstOrDefault(p => string.Equals(p.Login, login, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (target is null)
        {
            return false;
        }

        try
        {
            shared.SendCrossWhisper(from, HnmoSeasonInstance.Current, target.Season, target.Character, message);
        }
        catch (Exception ex)
        {
            sender.Logger.LogWarning(ex, "Friends across seasons: a whisper couldn't be sent.");
            return false;
        }

        var vi = string.Equals(sender.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        await sender.ShowBlueMessageAsync(vi
            ? $"Đã gửi thì thầm tới {target.Character} ({NameOf(target.Season)})."
            : $"Whisper sent to {target.Character} ({NameOf(target.Season)}).").ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (ArenaShared.Current is not { } shared)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var serverId = (gameContext as IGameServerContext)?.Id ?? 0;
        Contexts[serverId] = gameContext;
        var players = (await gameContext.GetPlayersAsync().ConfigureAwait(false))
            .Where(p => p.PlayerState.CurrentState != PlayerState.Disconnected && p.SelectedCharacter is not null && p.Account is not null)
            .ToList();
        var logger = gameContext.LoggerFactory.CreateLogger<CrossSeasonFriendsPlugIn>();

        if (now >= this._nextPublishUtc.GetValueOrDefault(serverId))
        {
            this._nextPublishUtc[serverId] = now.AddSeconds(15);
            try
            {
                shared.PublishOnline(HnmoSeasonInstance.Current, serverId, players.Select(p => (p.Account!.LoginName, p.SelectedCharacter!.Name)).ToList());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Friends across seasons: who plays here couldn't be published.");
            }
        }

        if (now >= this._nextFriendsUtc.GetValueOrDefault(serverId))
        {
            this._nextFriendsUtc[serverId] = now.AddSeconds(20);
            await this.UpdateFriendListsAsync(gameContext, players, logger).ConfigureAwait(false);
        }

        if (serverId == Contexts.Keys.Min() && now >= this._nextWhispersUtc && Interlocked.Exchange(ref this._whispering, 1) == 0)
        {
            try
            {
                this._nextWhispersUtc = now.AddSeconds(2);
                await DeliverWhispersAsync(shared, logger).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref this._whispering, 0);
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextPublishUtc.Clear();
        this._nextFriendsUtc.Clear();
    }

    /// <summary>The login name of the account of a character of this program (kept for ten minutes).</summary>
    private static async ValueTask<string?> LoginOfAsync(IGameContext gameContext, string characterName)
    {
        if (Logins.TryGetValue(characterName, out var cached) && DateTime.UtcNow - cached.ReadUtc < TimeSpan.FromMinutes(10))
        {
            return cached.Login;
        }

        using var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
        var login = (await context.GetAccountByCharacterNameAsync(characterName).ConfigureAwait(false))?.LoginName;
        Logins[characterName] = (login, DateTime.UtcNow);
        return login;
    }

    private static async ValueTask DeliverWhispersAsync(IArenaShared shared, ILogger logger)
    {
        IReadOnlyList<CrossWhisper> whispers;
        try
        {
            whispers = shared.TakeCrossWhispers(HnmoSeasonInstance.Current);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Friends across seasons: the whispers couldn't be read.");
            return;
        }

        foreach (var whisper in whispers)
        {
            var receiver = OnlineCharacters.Get(whisper.ToCharacter)
                ?? Contexts.Values.Select(c => c.GetPlayerByCharacterName(whisper.ToCharacter)).FirstOrDefault(p => p is not null);
            if (receiver is null)
            {
                continue;
            }

            await receiver.InvokeViewPlugInAsync<IChatViewPlugIn>(p => p.ChatMessageAsync($"({NameOf(whisper.FromSeason)}) {whisper.Text}", whisper.From, ChatMessageType.Whisper)).ConfigureAwait(false);
        }
    }

    private async ValueTask UpdateFriendListsAsync(IGameContext gameContext, List<Player> players, ILogger logger)
    {
        var elsewhere = Elsewhere();
        foreach (var player in players)
        {
            try
            {
                using var friendContext = gameContext.PersistenceContextProvider.CreateNewFriendServerContext();
                var friends = (await friendContext.GetFriendNamesAsync(player.SelectedCharacter!.GetId()).ConfigureAwait(false)).ToList();
                var notHere = friends.Where(f => gameContext.GetPlayerByCharacterName(f) is null && OnlineCharacters.Get(f) is null).ToList();
                var shown = Shown.GetOrCreateValue(player);
                foreach (var friend in notHere)
                {
                    var login = await LoginOfAsync(gameContext, friend).ConfigureAwait(false);
                    var presence = login is null ? null : elsewhere.FirstOrDefault(p => string.Equals(p.Login, login, StringComparison.OrdinalIgnoreCase));
                    var server = presence is null ? (byte)SpecialServerId.Offline : ServerOf(presence.Season);
                    var before = shown.TryGetValue(friend, out var b) ? b : (byte)SpecialServerId.Offline;
                    if (server == before)
                    {
                        continue;
                    }

                    shown[friend] = server;
                    await player.InvokeViewPlugInAsync<IFriendStateUpdatePlugIn>(p => p.FriendStateUpdateAsync(friend, server)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Friends across seasons: the friend list of {Player} couldn't be updated.", player);
            }
        }
    }
}
