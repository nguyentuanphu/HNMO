// <copyright file="MissionsPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.GameLogic.Views.NPC;
using HNMO.PlugIns;

/// <summary>
/// HNMO's daily and weekly missions and earned coins: missions which count kills, events, chaos machine mixes,
/// picked-up zen, levels and minutes in the game; a daily streak with a bonus; coins for the online time, level
/// milestones and resets, invites and a monthly login calendar, under a daily cap per account. Players see them in the
/// web game's missions window (J) and claim the rewards there. The progress is kept in a JSON file in the save folder.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MissionsPlugIn_Name), Description = nameof(PlugInResources.MissionsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6B1F2E84-3C5D-4A97-8E20-F4D7A1B9C356")]
public class MissionsPlugIn : IPeriodicTaskPlugIn, IAttackableGotKilledPlugIn, ICharacterLevelUpPlugIn, IMiniGameEndedPlugIn,
    IItemCraftedPlugIn, IMoneyPickedUpPlugIn, IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<MissionsConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly object StoreLock = new();
    private static MissionStore? _store;

    private readonly ConcurrentDictionary<Player, DateTime> _lastTick = new();
    private DateTime _nextTickUtc = DateTime.UtcNow + TickInterval;
    private (MissionsConfiguration Configuration, MissionEngine Engine)? _engine;

    /// <inheritdoc />
    public MissionsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static MissionsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<MissionsPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MissionsConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public MissionEngine GetEngine()
    {
        var configuration = this.Configuration ??= new MissionsConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var path = string.IsNullOrWhiteSpace(configuration.FileName) ? null : Path.GetFullPath(configuration.FileName.Trim(), AppContext.BaseDirectory);
        MissionStore store;
        lock (StoreLock)
        {
            if (_store is null || !string.Equals(_store.FilePath, path, StringComparison.Ordinal))
            {
                _store = new MissionStore(path);
            }

            store = _store;
        }

        var engine = new MissionEngine(configuration, store);
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>
    /// Sends the board to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="boardEvent">What happened.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendBoardAsync(Player player, bool requested, MissionBoardEvent? boardEvent = null)
    {
        if (!TryGetKeys(player, out var characterId, out var login))
        {
            return;
        }

        var json = MissionBoardWriter.WriteBoard(this.GetEngine(), characterId, login, DateTime.UtcNow, boardEvent);
        await player.InvokeViewPlugInAsync<IHnmoMissionsViewPlugIn>(p => p.ShowMissionsAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Claims the reward of a mission: zen and items go to the character (nothing is given, and it stays claimable,
    /// when the bag or the zen is full), the coins to the account.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="missionId">The mission id.</param>
    /// <returns>The result.</returns>
    public async ValueTask<MissionClaimResult> ClaimMissionAsync(Player player, string missionId)
    {
        if (!TryGetKeys(player, out var characterId, out var login))
        {
            return MissionClaimResult.Unknown;
        }

        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var result = engine.CheckClaim(characterId, missionId, now, out var mission);
        if (result == MissionClaimResult.Ok && engine.MarkClaimed(characterId, missionId, now, out var reference))
        {
            result = await GiveRewardAsync(player, login, mission!.Reward, reference, $"Mission \"{mission.TitleEn}\" ({mission.Period})").ConfigureAwait(false);
            if (result != MissionClaimResult.Ok)
            {
                engine.Unclaim(characterId, missionId, now);
            }
            else if (HNMO.GameLogic.PlugIns.Guilds.GuildsPlugIn.Get(player.GameContext) is { } guilds)
            {
                await guilds.AddForPlayerAsync(player, HNMO.GameLogic.PlugIns.Guilds.GuildXpSource.Mission).ConfigureAwait(false);
            }

            this.Save();
        }

        await this.SendBoardAsync(player, true, new MissionBoardEvent("claim", result.ToString(), [missionId], result == MissionClaimResult.Ok ? mission!.Reward.Coins : 0)).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Claims the waiting streak bonus.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The result.</returns>
    public async ValueTask<MissionClaimResult> ClaimStreakAsync(Player player)
    {
        if (!TryGetKeys(player, out var characterId, out var login))
        {
            return MissionClaimResult.Unknown;
        }

        var engine = this.GetEngine();
        var result = MissionClaimResult.NotFinished;
        if (engine.TakeStreakBonus(characterId, out var reference))
        {
            result = await GiveRewardAsync(player, login, engine.Configuration.StreakBonus, reference, "Daily streak bonus").ConfigureAwait(false);
            if (result != MissionClaimResult.Ok)
            {
                engine.ReturnStreakBonus(characterId, reference);
            }

            this.Save();
        }

        await this.SendBoardAsync(player, true, new MissionBoardEvent("streak", result.ToString(), Coins: result == MissionClaimResult.Ok ? engine.Configuration.StreakBonus.Coins : 0)).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Claims today's login calendar day.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The result.</returns>
    public async ValueTask<MissionClaimResult> ClaimCalendarAsync(Player player)
    {
        if (!TryGetKeys(player, out _, out var login))
        {
            return MissionClaimResult.Unknown;
        }

        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var result = engine.ClaimCalendar(login, now, out var grant);
        var given = 0;
        var waits = false;
        if (result == MissionClaimResult.Ok && grant is { Amount: > 0 })
        {
            if (engine.ReserveOrOwe(grant, now))
            {
                await MissionCoinGranter.GrantAsync(player.GameContext, grant).ConfigureAwait(false);
                given = grant.Amount;
            }
            else
            {
                waits = true;
            }
        }

        this.Save();
        await this.SendBoardAsync(player, true, new MissionBoardEvent("calendar", result.ToString(), Coins: given, Text: waits ? "capped" : null)).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Enters who invited the player's account: an invite code or a character name of the inviting account.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="codeOrName">The code or character name.</param>
    /// <returns>The result.</returns>
    public async ValueTask<InviteResult> EnterInviteAsync(Player player, string codeOrName)
    {
        if (!TryGetKeys(player, out _, out var login) || player.Account is not { } account)
        {
            return InviteResult.NotFound;
        }

        var engine = this.GetEngine();
        codeOrName = codeOrName.Trim();
        string? inviter = null;
        if (codeOrName.Length > 0)
        {
            lock (engine.Store.Lock)
            {
                inviter = engine.Store.FindByInviteCode(codeOrName);
            }

            if (inviter is null && codeOrName.Length <= 10)
            {
                using var context = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration);
                inviter = (await context.GetAccountByCharacterNameAsync(codeOrName).ConfigureAwait(false))?.LoginName;
            }
        }

        var highestLevel = account.Characters.Select(c => (int)(c.Attributes.FirstOrDefault(a => a.Definition == Stats.Level)?.Value ?? 0)).DefaultIfEmpty(0).Max();
        var result = engine.RegisterInvite(login, inviter, (player as IHasIpAddress)?.IpAddress, Math.Max(highestLevel, player.Level));
        this.Save();
        await this.SendBoardAsync(player, true, new MissionBoardEvent("invite", result.ToString())).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (now < this._nextTickUtc)
        {
            return;
        }

        this._nextTickUtc = now + TickInterval;
        var engine = this.GetEngine();
        var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var gone in this._lastTick.Keys.Except(players).ToList())
        {
            this._lastTick.TryRemove(gone, out _);
        }

        foreach (var player in players)
        {
            if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out var login))
            {
                continue;
            }

            try
            {
                var last = this._lastTick.TryGetValue(player, out var at) ? at : now - TickInterval;
                this._lastTick[player] = now;
                var seconds = (int)Math.Clamp((now - last).TotalSeconds, 0, 5 * 60);
                var autoPlay = (player.Attributes?[Stats.IsMuHelperActive] ?? 0) > 0;
                var finished = engine.AddProgress(characterId, MissionKind.PlayMinutes, (int)Math.Round(seconds / 60.0), null, now);
                var grants = new List<CoinGrant>();
                if (engine.AddOnlineTime(login, seconds, autoPlay, now) is { } online)
                {
                    grants.Add(online);
                }

                grants.AddRange(engine.TakeOwed(login, now));
                await this.GiveEarnedAsync(player, grants, true).ConfigureAwait(false);
                await this.NotifyFinishedAsync(player, finished).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Missions: the minute of {Player} wasn't counted.", player);
            }
        }

        this.Save();
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextTickUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Monster { SummonedBy: null } monster)
        {
            return;
        }

        var player = killer as Player ?? (killer as IPlayerSurrogate)?.Owner ?? (killer as Monster)?.SummonedBy;
        if (player is null || !IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var number = monster.Definition.Number;
        var map = monster.CurrentMap?.Definition.Number ?? -1;
        var finished = this.GetEngine().AddProgress(
            characterId,
            MissionKind.KillMonsters,
            1,
            m => (m.MonsterNumber <= 0 || m.MonsterNumber == number) && (m.MapNumber < 0 || m.MapNumber == map),
            DateTime.UtcNow);
        await this.NotifyFinishedAsync(player, finished).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var finished = this.GetEngine().AddProgress(characterId, MissionKind.LevelUp, 1, null, DateTime.UtcNow);
        _ = Task.Run(async () =>
        {
            try
            {
                await this.NotifyFinishedAsync(player, finished).ConfigureAwait(false);
                await this.CheckLevelRewardsAsync(player).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Missions: the level rewards of {Player} weren't checked.", player);
            }
        });
    }

    /// <inheritdoc />
    public async ValueTask MiniGameEndedAsync(MiniGameContext miniGame, Player? winner, IReadOnlyCollection<Player> finishers)
    {
        var type = miniGame.Definition.Type switch
        {
            MiniGameType.BloodCastle => MissionEventType.BloodCastle,
            MiniGameType.DevilSquare => MissionEventType.DevilSquare,
            MiniGameType.ChaosCastle => MissionEventType.ChaosCastle,
            _ => (MissionEventType?)null,
        };
        if (type is null)
        {
            return;
        }

        bool Matches(MissionDefinition m) => m.EventType == MissionEventType.Any || m.EventType == type;
        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        foreach (var player in finishers.Append(winner).OfType<Player>().Distinct())
        {
            if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
            {
                continue;
            }

            var finished = new List<MissionDefinition>();
            if (finishers.Contains(player))
            {
                finished.AddRange(engine.AddProgress(characterId, MissionKind.FinishEvent, 1, Matches, now));
            }

            if (player == winner)
            {
                finished.AddRange(engine.AddProgress(characterId, MissionKind.WinEvent, 1, Matches, now));
            }

            await this.NotifyFinishedAsync(player, finished).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask ItemCraftedAsync(Player player, CraftingResult result)
    {
        if (result != CraftingResult.Success || !IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var finished = this.GetEngine().AddProgress(characterId, MissionKind.ChaosMachineMix, 1, null, DateTime.UtcNow);
        await this.NotifyFinishedAsync(player, finished).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask MoneyPickedUpAsync(Player player, uint amount)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var finished = this.GetEngine().AddProgress(characterId, MissionKind.PickUpZen, amount, null, DateTime.UtcNow);
        await this.NotifyFinishedAsync(player, finished).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection || currentState != PlayerState.EnteredWorld || !IsCounted(player) || !TryGetKeys(player, out var characterId, out var login))
        {
            return ValueTask.CompletedTask;
        }

        var engine = this.GetEngine();
        engine.SetAddress(login, (player as IHasIpAddress)?.IpAddress);
        lock (engine.Store.Lock)
        {
            var state = engine.Store.Character(characterId);
            state.Name = player.SelectedCharacter?.Name;
            state.Account = login;
            engine.Store.Changed();
        }

        this._lastTick[player] = DateTime.UtcNow;
        _ = Task.Run(async () =>
        {
            try
            {
                await this.CheckLevelRewardsAsync(player).ConfigureAwait(false);
                await this.GiveEarnedAsync(player, engine.TakeOwed(login, DateTime.UtcNow), true).ConfigureAwait(false);
                this.Save();
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Missions: the rewards of {Player} weren't checked.", player);
            }
        });
        return ValueTask.CompletedTask;
    }

    private static bool IsCounted(Player player) =>
        player is not OfflinePlayer && player.Account is { IsBot: false } && !player.IsTemplatePlayer && player.SelectedCharacter is not null;

    private static bool TryGetKeys(Player player, out string characterId, out string login)
    {
        characterId = player.SelectedCharacter?.Id.ToString() ?? string.Empty;
        login = player.Account?.LoginName ?? string.Empty;
        return characterId.Length > 0 && login.Length > 0 && player.SelectedCharacter!.Id != Guid.Empty;
    }

    private static async ValueTask<MissionClaimResult> GiveRewardAsync(Player player, string login, MissionReward reward, string reference, string reason)
    {
        if (player.Inventory is not { } inventory)
        {
            return MissionClaimResult.BagFull;
        }

        var added = new List<Item>();
        foreach (var entry in reward.Items.Where(i => i.Count > 0))
        {
            var definition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == entry.Group && d.Number == entry.Number);
            if (definition is null)
            {
                player.Logger.LogWarning("Missions: the reward item {Group} {Number} doesn't exist; it's left out.", entry.Group, entry.Number);
                continue;
            }

            for (var i = 0; i < entry.Count; i++)
            {
                var item = player.PersistenceContext.CreateNew<Item>();
                item.Definition = definition;
                item.Level = Math.Min(entry.Level, definition.MaximumItemLevel);
                item.Durability = item.GetMaximumDurabilityOfOnePiece();
                if (!await inventory.AddItemAsync(item).ConfigureAwait(false))
                {
                    await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                    foreach (var back in added)
                    {
                        await inventory.RemoveItemAsync(back).ConfigureAwait(false);
                        await player.PersistenceContext.DeleteAsync(back).ConfigureAwait(false);
                    }

                    return MissionClaimResult.BagFull;
                }

                added.Add(item);
            }
        }

        if (reward.Zen > 0 && !player.TryAddMoney(reward.Zen))
        {
            foreach (var back in added)
            {
                await inventory.RemoveItemAsync(back).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(back).ConfigureAwait(false);
            }

            return MissionClaimResult.ZenFull;
        }

        foreach (var item in added)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (reward.Zen > 0)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        if (reward.Coins > 0)
        {
            await MissionCoinGranter.GrantAsync(player.GameContext, new CoinGrant(login, reward.CoinType, reward.Coins, reason, reference)).ConfigureAwait(false);
        }

        player.Logger.LogInformation("Missions: {Player} got the reward of {Reference}.", player, reference);
        return MissionClaimResult.Ok;
    }

    private async ValueTask CheckLevelRewardsAsync(Player player)
    {
        if (!TryGetKeys(player, out var characterId, out var login))
        {
            return;
        }

        var engine = this.GetEngine();
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0);
        var grants = new List<CoinGrant>(engine.TakeMilestones(characterId, login, player.SelectedCharacter!.Name, player.Level, resets));
        var invite = engine.TakeInviteRewards(login, player.Level);
        var now = DateTime.UtcNow;
        foreach (var grant in invite)
        {
            if (!string.Equals(grant.LoginName, login, StringComparison.OrdinalIgnoreCase))
            {
                // The inviting account's coins: under its own cap, given now or when it plays next.
                if (engine.ReserveOrOwe(grant, now))
                {
                    await MissionCoinGranter.GrantAsync(player.GameContext, grant).ConfigureAwait(false);
                }
            }
            else
            {
                grants.Add(grant);
            }
        }

        await this.GiveEarnedAsync(player, grants, false).ConfigureAwait(false);
        this.Save();
    }

    /// <summary>
    /// Gives earned coins to the player's account.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="grants">The grants.</param>
    /// <param name="reserved">Whether they were reserved under the daily cap already (online time, waiting rewards);
    /// otherwise each is reserved here, or waits for a later day.</param>
    private async ValueTask GiveEarnedAsync(Player player, IEnumerable<CoinGrant> grants, bool reserved)
    {
        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var given = 0;
        string? reason = null;
        foreach (var grant in grants)
        {
            if ((reserved || engine.ReserveOrOwe(grant, now)) && await MissionCoinGranter.GrantAsync(player.GameContext, grant).ConfigureAwait(false))
            {
                given += grant.Amount;
                reason = grant.Reason;
            }
        }

        if (given > 0)
        {
            await this.SendBoardAsync(player, false, new MissionBoardEvent("coins", "Ok", Coins: given, Text: reason)).ConfigureAwait(false);
        }
    }

    private async ValueTask NotifyFinishedAsync(Player player, IReadOnlyList<MissionDefinition> finished)
    {
        if (finished.Count == 0)
        {
            return;
        }

        player.Logger.LogInformation("Missions: {Player} finished {Missions}.", player, string.Join(", ", finished.Select(m => m.Id)));
        await this.SendBoardAsync(player, false, new MissionBoardEvent("finished", "Ok", finished.Select(m => m.Id).ToList())).ConfigureAwait(false);
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
