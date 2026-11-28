// <copyright file="TopRacesPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.Community;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// HNMO's top races: races by day, week, month or between two dates (Sydney time), of resets, levels, boss-hunt points,
/// PK kills, event wins, guild points, monster kills or time in the game, for all classes, one class or per class, each
/// with its own prizes (coins, zen, items) for rank ranges, paid once when it ends. The start and the end are told in
/// the game (a golden notice), through the community posts (Discord, the website's highlights) and on the website; the
/// web game shows them in its top races window. The state is kept in a JSON file in the save folder.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TopRacesPlugIn_Name), Description = nameof(PlugInResources.TopRacesPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3E7A1C52-9B84-4D06-A2F3-58C1E9D04B7A")]
public class TopRacesPlugIn : IPeriodicTaskPlugIn, IAttackableGotKilledPlugIn, ICharacterLevelUpPlugIn, IMiniGameEndedPlugIn, IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<TopRacesConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>The name in the coins history's "granted by" column.</summary>
    public const string GrantedBy = "HNMO top races";

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan GuildKeyLifetime = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();
    private static readonly ConcurrentDictionary<uint, (Guid Id, string Name, DateTime AtUtc)> GuildKeys = new();

    private DateTime _nextTickUtc = DateTime.UtcNow + TimeSpan.FromSeconds(15);
    private int _running;
    private (TopRacesConfiguration Configuration, TopRaceEngine Engine)? _engine;

    /// <inheritdoc />
    public TopRacesConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static TopRacesPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<TopRacesPlugIn>().FirstOrDefault();

    /// <summary>
    /// Counts a character's level and resets at once (a reset, relife or delegated reset done by the reset options, also
    /// for characters which aren't in the game): the levels and resets gained since it was last seen go to the races,
    /// a relife (fewer resets) only remembers the new count. Call it before and after the change.
    /// </summary>
    /// <param name="gameContext">A game context of this process, if any (else one the plug-in ran on).</param>
    /// <param name="characterId">The character's id.</param>
    /// <param name="name">The character's name.</param>
    /// <param name="login">The account's login name.</param>
    /// <param name="classNumber">The character class number.</param>
    /// <param name="level">The level.</param>
    /// <param name="resets">The resets.</param>
    public static void ObserveCharacter(IGameContext? gameContext, string characterId, string name, string login, int classNumber, int level, int resets)
    {
        var plugIn = (gameContext is null ? null : Get(gameContext))
            ?? Contexts.Values.Distinct().Select(Get).FirstOrDefault(p => p is not null);
        if (plugIn is null || string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(login))
        {
            return;
        }

        plugIn.GetEngine().Observe(new TopRaceParticipant(characterId, name, login, TopRaceEngine.ClassOf(classNumber)), level, resets, DateTime.UtcNow);
        plugIn.Save();
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new TopRacesConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public TopRaceEngine GetEngine()
    {
        var configuration = this.Configuration ??= new TopRacesConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new TopRaceEngine(configuration, TopRaceStore.Open(configuration.FileName));
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>
    /// Sends the state to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="stateEvent">What happened.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, TopRaceStateEvent? stateEvent = null)
    {
        if (ParticipantOf(player) is not { } participant)
        {
            return;
        }

        var guild = await GuildOfAsync(player).ConfigureAwait(false);
        var json = TopRaceStateWriter.WriteState(this.GetEngine(), participant.Key, participant.Class, guild is { } g ? TopRaceEngine.GuildKey(g.Id) : null, DateTime.UtcNow, stateEvent);
        await player.InvokeViewPlugInAsync<IHnmoTopRacesViewPlugIn>(p => p.ShowTopRacesAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the waiting prizes which fit (the window's button).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask ClaimAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        this.Save();
        await this.SendStateAsync(player, true, new TopRaceStateEvent("claim", left > 0 ? "BagFull" : given > 0 ? "Ok" : "Nothing")).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the boss-hunt points of a world boss kill (called by the world bosses), also for players who left the game.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="record">The kill.</param>
    /// <returns>The task.</returns>
    public async ValueTask WorldBossKilledAsync(IGameContext gameContext, WorldBossKillRecord record)
    {
        if (!record.Killed || !this.GetEngine().Races.Any(r => r.Kind == TopRaceKind.BossHuntPoints))
        {
            return;
        }

        var engine = this.GetEngine();
        foreach (var line in record.Lines.Where(l => l.Points > 0))
        {
            try
            {
                if (await this.ResolveAsync(gameContext, line.Name).ConfigureAwait(false) is { } participant)
                {
                    engine.Add(participant, TopRaceKind.BossHuntPoints, line.Points, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<TopRacesPlugIn>().LogWarning(ex, "Top races: the boss-hunt points of {Name} weren't counted.", line.Name);
            }
        }

        this.Save();
    }

    /// <summary>
    /// Counts guild experience a guild earned (called by the guilds).
    /// </summary>
    /// <param name="guildId">The persistent guild id.</param>
    /// <param name="guildName">The guild's name.</param>
    /// <param name="amount">The experience.</param>
    public void GuildPointsEarned(Guid guildId, string? guildName, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        this.GetEngine().Add(new TopRaceParticipant(TopRaceEngine.GuildKey(guildId), guildName ?? string.Empty, string.Empty, -1), TopRaceKind.GuildPoints, amount, DateTime.UtcNow);
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        var serverId = ServerIdOf(gameContext);
        Contexts[serverId] = gameContext;
        if (now < this._nextTickUtc || Interlocked.Exchange(ref this._running, 1) == 1)
        {
            return;
        }

        try
        {
            this._nextTickUtc = now + TickInterval;
            var engine = this.GetEngine();
            engine.Roll(now);
            foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
            {
                try
                {
                    await this.TickPlayerAsync(player, engine, now).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    player.Logger.LogDebug(ex, "Top races: the tick of {Player} failed.", player);
                }
            }

            // The starts, ends and prizes are handled once, by the game server with the lowest id of this process.
            if (serverId == Contexts.Keys.Min())
            {
                await this.AnnounceAsync(gameContext, now).ConfigureAwait(false);
                await this.PayAsync(gameContext, now).ConfigureAwait(false);
            }

            this.Save();
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<TopRacesPlugIn>().LogError(ex, "Top races: the tick failed.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextTickUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        var player = killer as Player ?? (killer as IPlayerSurrogate)?.Owner ?? (killer as Monster)?.SummonedBy;
        if (player is null || ParticipantOf(player) is not { } participant)
        {
            return ValueTask.CompletedTask;
        }

        if (killed is Monster { SummonedBy: null } monster && monster.Definition.ObjectKind == NpcObjectKind.Monster)
        {
            this.GetEngine().Add(participant, TopRaceKind.MonsterKills, 1, DateTime.UtcNow);
        }
        else if (killed is Player victim && !ReferenceEquals(victim, player) && victim.SelectedCharacter is not null
            && victim.DuelRoom is null && victim.CurrentMiniGame is null
            && !string.Equals(victim.Account?.LoginName, player.Account?.LoginName, StringComparison.OrdinalIgnoreCase))
        {
            this.GetEngine().Add(participant, TopRaceKind.PkKills, 1, DateTime.UtcNow);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player) => this.Observe(player, DateTime.UtcNow);

    /// <inheritdoc />
    public ValueTask MiniGameEndedAsync(MiniGameContext miniGame, Player? winner, IReadOnlyCollection<Player> finishers)
    {
        var kind = miniGame.Definition.Type switch
        {
            MiniGameType.BloodCastle => TopRaceEvent.BloodCastle,
            MiniGameType.DevilSquare => TopRaceEvent.DevilSquare,
            MiniGameType.ChaosCastle => TopRaceEvent.ChaosCastle,
            _ => TopRaceEvent.Any,
        };
        if (kind != TopRaceEvent.Any && winner is not null && ParticipantOf(winner) is { } participant)
        {
            this.GetEngine().Add(participant, TopRaceKind.EventWins, 1, DateTime.UtcNow, kind);
            this.Save();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld || previousState != PlayerState.CharacterSelection || ParticipantOf(player) is null)
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                this.Observe(player, DateTime.UtcNow);
                await this.DeliverAndTellAsync(player).ConfigureAwait(false);
                this.Save();
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Top races: the waiting prizes of {Player} weren't given.", player);
            }
        });
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Formats a moment on the wall clock of the races' time zone.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="utc">The moment.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The text.</returns>
    internal static string FormatTime(TopRaceEngine engine, DateTime utc, CultureInfo culture) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), engine.Clock.TimeZone).ToString("ddd d MMM HH:mm", culture);

    private static byte ServerIdOf(IGameContext context) => (context as IGameServerContext)?.Id ?? 0;

    private static bool IsCounted(Player player) =>
        player is not OfflinePlayer && player.Account is { IsBot: false } && !player.IsTemplatePlayer && player.SelectedCharacter is { } character && character.Id != Guid.Empty;

    private static TopRaceParticipant? ParticipantOf(Player player) =>
        IsCounted(player) && player.Account?.LoginName is { Length: > 0 } login
            ? new TopRaceParticipant(player.SelectedCharacter!.Id.ToString(), player.SelectedCharacter.Name, login, TopRaceEngine.ClassOf(player.SelectedCharacter.CharacterClass?.Number ?? 0))
            : null;

    private static bool IsVietnamese(Player player) => player.Culture.TwoLetterISOLanguageName == "vi";

    private static async ValueTask<(Guid Id, string Name)?> GuildOfAsync(Player player)
    {
        if (player.GuildStatus is not { GuildId: > 0 } status || player.GameContext is not IGameServerContext serverContext)
        {
            return null;
        }

        if (GuildKeys.TryGetValue(status.GuildId, out var cached) && DateTime.UtcNow - cached.AtUtc < GuildKeyLifetime)
        {
            return (cached.Id, cached.Name);
        }

        if (await serverContext.GuildServer.GetPersistentGuildIdAsync(status.GuildId).ConfigureAwait(false) is not { } guildId)
        {
            return null;
        }

        var name = await serverContext.GuildServer.GetPersistentGuildNameAsync(guildId).ConfigureAwait(false) ?? string.Empty;
        GuildKeys[status.GuildId] = (guildId, name, DateTime.UtcNow);
        return (guildId, name);
    }

    private static async ValueTask<Player?> FindPlayerAsync(string characterId)
    {
        foreach (var context in Contexts.Values.Distinct())
        {
            var player = (await context.GetPlayersAsync().ConfigureAwait(false)).FirstOrDefault(p => p.SelectedCharacter?.Id.ToString() == characterId && IsCounted(p));
            if (player is not null)
            {
                return player;
            }
        }

        return null;
    }

    private static async ValueTask ForEachPlayerAsync(Func<Player, Task> action)
    {
        foreach (var context in Contexts.Values.Distinct())
        {
            await context.ForEachPlayerAsync(action).ConfigureAwait(false);
        }
    }

    private void Observe(Player player, DateTime now)
    {
        if (ParticipantOf(player) is not { } participant || player.Attributes is not { } attributes)
        {
            return;
        }

        this.GetEngine().Observe(participant, player.Level, (int)attributes[Stats.Resets], now);
    }

    /// <summary>
    /// Finds a character by name: online, or in the database.
    /// </summary>
    private async ValueTask<TopRaceParticipant?> ResolveAsync(IGameContext gameContext, string name)
    {
        foreach (var context in Contexts.Values.Append(gameContext).Distinct())
        {
            var online = (await context.GetPlayersAsync().ConfigureAwait(false)).FirstOrDefault(p => p.SelectedCharacter?.Name == name);
            if (online is not null)
            {
                return ParticipantOf(online);
            }
        }

        using var players = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
        var account = await players.GetAccountByCharacterNameAsync(name).ConfigureAwait(false);
        if (account is null || account.IsBot)
        {
            return null;
        }

        var character = account.Characters.FirstOrDefault(c => c.Name == name);
        return character is null ? null : new TopRaceParticipant(character.Id.ToString(), character.Name, account.LoginName, TopRaceEngine.ClassOf(character.CharacterClass?.Number ?? 0));
    }

    private async ValueTask TickPlayerAsync(Player player, TopRaceEngine engine, DateTime now)
    {
        if (ParticipantOf(player) is not { } participant || player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            return;
        }

        engine.Add(participant, TopRaceKind.OnlineMinutes, 1, now);
        this.Observe(player, now);
        if (engine.Owed(participant.Key).Count > 0)
        {
            await this.DeliverAndTellAsync(player).ConfigureAwait(false);
        }
    }

    private string ValueText(TopRaceKind kind, long value, bool vietnamese)
    {
        var number = value.ToString("N0", CultureInfo.InvariantCulture);
        return kind switch
        {
            TopRaceKind.ResetsGained => vietnamese ? $"{number} lần reset" : $"{number} resets",
            TopRaceKind.LevelsGained => vietnamese ? $"{number} cấp" : $"{number} levels",
            TopRaceKind.LevelReached => vietnamese ? $"cấp {number}" : $"level {number}",
            TopRaceKind.BossHuntPoints or TopRaceKind.GuildPoints => vietnamese ? $"{number} điểm" : $"{number} points",
            TopRaceKind.PkKills => vietnamese ? $"{number} lần hạ người chơi" : $"{number} player kills",
            TopRaceKind.EventWins => vietnamese ? $"{number} lần thắng" : $"{number} wins",
            TopRaceKind.MonsterKills => vietnamese ? $"{number} quái" : $"{number} monsters",
            _ => vietnamese ? $"{number} phút" : $"{number} minutes",
        };
    }

    private async ValueTask AnnounceAsync(IGameContext gameContext, DateTime now)
    {
        var engine = this.GetEngine();
        var configuration = engine.Configuration;
        var (started, ended) = engine.TakeAnnouncements(now);
        var english = CultureInfo.GetCultureInfo("en-AU");
        var vietnamese = CultureInfo.GetCultureInfo("vi");
        var community = CommunityPlugIn.Get(gameContext);
        foreach (var (race, run) in started)
        {
            // A race noticed long after its start (the server was off) isn't announced as new.
            if (now - run.StartUtc > TimeSpan.FromHours(1))
            {
                continue;
            }

            var en = TopRaceEngine.Fill(configuration.StartTextEn, new Dictionary<string, string> { ["race"] = race.TitleEn, ["end"] = FormatTime(engine, run.EndUtc, english) });
            var vi = TopRaceEngine.Fill(string.IsNullOrWhiteSpace(configuration.StartTextVi) ? configuration.StartTextEn : configuration.StartTextVi, new Dictionary<string, string> { ["race"] = race.TitleViOrEn, ["end"] = FormatTime(engine, run.EndUtc, vietnamese) });
            await this.TellEveryoneAsync(new TopRaceStateEvent("start", RaceId: race.Id, TextEn: en, TextVi: vi, InGame: configuration.AnnounceInGame)).ConfigureAwait(false);
            if (configuration.AnnounceOnCommunity && community is not null)
            {
                await community.TopRaceStartedAsync(gameContext, race.TitleEn, race.TitleViOrEn, FormatTime(engine, run.EndUtc, english), FormatTime(engine, run.EndUtc, vietnamese)).ConfigureAwait(false);
            }
        }

        foreach (var result in ended)
        {
            if (now - result.EndUtc > TimeSpan.FromDays(1))
            {
                continue;
            }

            var winners = result.Tables.Select(t => t.Lines.FirstOrDefault()).Where(l => l is not null).Select(l => l!).ToList();
            var winnerText = winners.Count == 0 ? "-" : string.Join(", ", winners.Select(w => w.Name));
            var top = winners.OrderByDescending(w => w.Value).FirstOrDefault();
            var en = TopRaceEngine.Fill(configuration.EndTextEn, new Dictionary<string, string> { ["race"] = result.TitleEn, ["winner"] = winnerText, ["value"] = top is null ? "-" : this.ValueText(result.Kind, top.Value, false) });
            var vi = TopRaceEngine.Fill(string.IsNullOrWhiteSpace(configuration.EndTextVi) ? configuration.EndTextEn : configuration.EndTextVi, new Dictionary<string, string> { ["race"] = result.TitleVi, ["winner"] = winnerText, ["value"] = top is null ? "-" : this.ValueText(result.Kind, top.Value, true) });
            await this.TellEveryoneAsync(new TopRaceStateEvent("end", RaceId: result.RaceId, TextEn: en, TextVi: vi, InGame: configuration.AnnounceInGame)).ConfigureAwait(false);
            if (configuration.AnnounceOnCommunity && community is not null)
            {
                foreach (var table in result.Tables)
                {
                    foreach (var line in table.Lines.Take(community.GetEngine().Configuration.TopRaceRanksPosted))
                    {
                        await community.TopRaceWinnerAsync(gameContext, result.TitleEn, result.TitleVi, line.Name, line.Rank, this.ValueText(result.Kind, line.Value, false), this.ValueText(result.Kind, line.Value, true)).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private async ValueTask TellEveryoneAsync(TopRaceStateEvent stateEvent)
    {
        await ForEachPlayerAsync(async player =>
        {
            try
            {
                if (stateEvent.InGame && player.SelectedCharacter is not null)
                {
                    var text = IsVietnamese(player) ? stateEvent.TextVi : stateEvent.TextEn;
                    if (!string.IsNullOrEmpty(text))
                    {
                        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(text, MessageType.GoldenCenter)).ConfigureAwait(false);
                    }
                }

                await this.SendStateAsync(player, false, stateEvent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogDebug(ex, "Top races: the notice wasn't shown.");
            }
        }).ConfigureAwait(false);
    }

    private async ValueTask PayAsync(IGameContext gameContext, DateTime now)
    {
        var engine = this.GetEngine();
        var logger = gameContext.LoggerFactory.CreateLogger<TopRacesPlugIn>();
        foreach (var result in engine.TakeUnpaid(now))
        {
            try
            {
                foreach (var table in result.Tables)
                {
                    foreach (var line in table.Lines.Where(l => l.Prized))
                    {
                        if (TopRaceEngine.GuildIdOf(line.Key) is { } guildId)
                        {
                            await this.PayGuildAsync(gameContext, engine, result, table, line, guildId).ConfigureAwait(false);
                        }
                        else
                        {
                            await this.PayCharacterAsync(gameContext, engine, result, table, line, line.Key, line.Login, null, null).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // It's tried again on the next tick; the references keep it from paying twice.
                logger.LogWarning(ex, "Top races: the prizes of {Race} ({Period}) weren't paid yet.", result.RaceId, result.PeriodKey);
                continue;
            }

            engine.MarkPaid(result);
            logger.LogInformation("Top races: the prizes of {Race} ({Period}) were paid.", result.RaceId, result.PeriodKey);
        }
    }

    private async ValueTask PayGuildAsync(IGameContext gameContext, TopRaceEngine engine, TopRaceResult result, TopRaceTable table, TopRaceLine line, Guid guildId)
    {
        IReadOnlyDictionary<Guid, string> members;
        using (var guilds = gameContext.PersistenceContextProvider.CreateNewGuildContext())
        {
            members = await guilds.GetMemberNamesAsync(guildId).ConfigureAwait(false);
        }

        foreach (var (memberId, name) in members)
        {
            if (result.PaidReferences.Contains(TopRaceEngine.PrizeReference(result, table.Class, line.Key, memberId)))
            {
                continue;
            }

            string? login;
            using (var players = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration))
            {
                login = (await players.GetAccountByCharacterNameAsync(name).ConfigureAwait(false))?.LoginName;
            }

            if (login is not null)
            {
                await this.PayCharacterAsync(gameContext, engine, result, table, line, memberId.ToString(), login, memberId, name).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask PayCharacterAsync(IGameContext gameContext, TopRaceEngine engine, TopRaceResult result, TopRaceTable table, TopRaceLine line, string characterId, string login, Guid? memberId, string? memberName)
    {
        var reference = TopRaceEngine.PrizeReference(result, table.Class, line.Key, memberId);
        if (result.PaidReferences.Contains(reference))
        {
            return;
        }

        var grant = TopRaceEngine.PrizeGrant(result, table.Class, line, login, memberId, memberName);
        if (grant is not null)
        {
            await MissionCoinGranter.GrantRewardAsync(gameContext, grant, GrantedBy).ConfigureAwait(false);
        }

        if (line.Zen > 0 || line.Items.Any(i => i.Count > 0))
        {
            engine.AddOwed(characterId, new TopRaceOwedReward
            {
                Reference = reference + ":goods",
                RaceId = result.RaceId,
                TitleEn = result.TitleEn,
                TitleVi = result.TitleVi,
                PeriodKey = result.PeriodKey,
                Rank = line.Rank,
                Reward = new MissionReward { Zen = line.Zen, Items = line.Items.Where(i => i.Count > 0).ToList() },
            });
        }

        engine.NotePaid(result, reference);
        this.Save();
        if (await FindPlayerAsync(characterId).ConfigureAwait(false) is { } player)
        {
            await this.SendStateAsync(player, false, new TopRaceStateEvent("prize", "Ok", result.RaceId, result.TitleEn, result.TitleVi, line.Rank, grant?.Amount ?? 0)).ConfigureAwait(false);
            await this.DeliverAndTellAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives the waiting prizes of a player (zen and items) when they fit.
    /// </summary>
    private async ValueTask<(int Given, int Left)> DeliverAsync(Player player)
    {
        if (ParticipantOf(player) is not { } participant)
        {
            return (0, 0);
        }

        var engine = this.GetEngine();
        var keep = new List<TopRaceOwedReward>();
        var given = 0;
        foreach (var reward in engine.TakeOwed(participant.Key))
        {
            try
            {
                if (await GuildsPlugIn.GiveGoodsAsync(player, reward.Reward).ConfigureAwait(false))
                {
                    given++;
                    player.Logger.LogInformation("Top races: {Player} got the prize {Reference}.", player, reward.Reference);
                }
                else
                {
                    keep.Add(reward);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Top races: the prize {Reference} of {Player} wasn't given; it waits.", reward.Reference, player);
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(participant.Key, keep);
        return (given, keep.Count);
    }

    private async ValueTask DeliverAndTellAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        if (given > 0 || left > 0)
        {
            await this.SendStateAsync(player, false, new TopRaceStateEvent("goods", left > 0 ? "BagFull" : "Ok", Coins: given)).ConfigureAwait(false);
        }
    }

    private void Save()
    {
        var store = this.GetEngine().Store;
        lock (store.Lock)
        {
            store.SaveIfChanged();
        }
    }
}
