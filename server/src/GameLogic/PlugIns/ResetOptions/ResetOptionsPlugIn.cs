// <copyright file="ResetOptionsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Character;
using HNMO.GameLogic.Views.Login;
using HNMO.GameLogic.Views.NPC;
using HNMO.PlugIns;

/// <summary>
/// HNMO's reset options: reset tiers (level, zen, coins, items, points, daily limits), VIP advantages, the relife (grand
/// reset) and the delegated reset during offline leveling. While it's on, the reset NPC and /reset follow these rules
/// (else the plain reset feature's). The state (relifes, daily counts, the monthly ranking, the log) is kept in a JSON
/// file in the save folder.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetOptionsPlugIn_Name), Description = nameof(PlugInResources.ResetOptionsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C4D91E27-6A35-4B8E-9F02-7E1A53B6D948")]
public class ResetOptionsPlugIn : IPeriodicTaskPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<ResetOptionsConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();

    private readonly LogoutAction _logoutAction = new();
    private DateTime _nextTickUtc = DateTime.UtcNow + TimeSpan.FromSeconds(15);
    private int _running;
    private (ResetOptionsConfiguration Configuration, ResetEngine Engine)? _engine;

    /// <inheritdoc />
    public ResetOptionsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static ResetOptionsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<ResetOptionsPlugIn>().FirstOrDefault();

    /// <summary>
    /// Gets the game contexts of this process which run the plug-in (for the website).
    /// </summary>
    /// <returns>The contexts.</returns>
    public static IReadOnlyCollection<IGameContext> KnownContexts() => Contexts.Values.Distinct().ToList();

    /// <summary>
    /// The VIP value of a player: the account's "Is VIP" attribute (with its valid grants).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The value.</returns>
    public static int VipOf(Player player)
    {
        var fromAttributes = (int)(player.Attributes?[Stats.IsVip] ?? 0);
        var fromAccount = player.Account is { } account ? (int)account.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow) : 0;
        return Math.Max(fromAttributes, fromAccount);
    }

    /// <summary>
    /// The subject of a player's character; <c>null</c> for bots, templates and players without a character.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The subject.</returns>
    public static ResetSubject? SubjectOf(Player player)
    {
        if (player.IsTemplatePlayer || player.Account is not { IsBot: false, LoginName: { Length: > 0 } login } account
            || player.SelectedCharacter is not { } character || character.Id == Guid.Empty || player.Attributes is not { } attributes)
        {
            return null;
        }

        var items = player.Inventory?.Items.ToList() ?? new List<Item>();
        return new ResetSubject
        {
            CharacterId = character.Id.ToString(),
            Name = character.Name,
            Login = login,
            Class = character.CharacterClass?.Number ?? 0,
            Level = player.Level,
            Resets = (int)attributes[Stats.Resets],
            Zen = player.Money,
            Coins = ResetApplier.CoinsOf(account),
            VipLevel = VipOf(player),
            CountItems = cost => ResetApplier.Count(items, cost),
        };
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ResetOptionsConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public ResetEngine GetEngine()
    {
        var configuration = this.Configuration ??= new ResetOptionsConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new ResetEngine(configuration, ResetStore.Open(configuration.FileName));
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>
    /// Sends the state to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="eventType">What happened.</param>
    /// <param name="result">The result of it.</param>
    /// <param name="count">A number of it.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, string? eventType = null, ResetResult? result = null, int count = 0)
    {
        if (player is OfflinePlayer || SubjectOf(player) is not { } subject)
        {
            return;
        }

        var json = ResetStateWriter.WriteState(this.GetEngine(), subject, DateTime.UtcNow, GiftCodesPlugIn.ItemNames(player.GameContext.Configuration), eventType, result, count);
        await player.InvokeViewPlugInAsync<IHnmoResetsViewPlugIn>(p => p.ShowResetsAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the player's character (the reset NPC, /reset, the web game).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC the player talks to, if any.</param>
    /// <returns><c>true</c>, if it was reset.</returns>
    public ValueTask<bool> ResetAsync(Player player, NonPlayerCharacter? npc = null) => this.DoAsync(player, npc, false);

    /// <summary>
    /// Does the relife of the player's character (/relife, the web game).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC the player talks to, if any.</param>
    /// <returns><c>true</c>, if it was done.</returns>
    public ValueTask<bool> RelifeAsync(Player player, NonPlayerCharacter? npc = null) => this.DoAsync(player, npc, true);

    /// <summary>
    /// Tells the player what the next reset and relife need (/reset info).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask InfoAsync(Player player)
    {
        if (SubjectOf(player) is not { } subject)
        {
            return;
        }

        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var vietnamese = IsVietnamese(player);
        var names = GiftCodesPlugIn.ItemNames(player.GameContext.Configuration);
        var next = engine.PlanReset(subject, now, ResetSource.Game);
        var record = engine.RecordOf(subject.CharacterId, now);
        var limit = next.DailyLimit > 0 ? $"{next.Today}/{next.DailyLimit}" : $"{next.Today}";
        await player.ShowBlueMessageAsync(vietnamese
            ? $"Reset: {subject.Resets}, relife: {record.Relifes}, hôm nay: {limit}{(next.Vip ? " (VIP)" : string.Empty)}."
            : $"Resets: {subject.Resets}, relifes: {record.Relifes}, today: {limit}{(next.Vip ? " (VIP)" : string.Empty)}.").ConfigureAwait(false);
        if (next.Result is ResetResult.Ok or ResetResult.LevelTooLow or ResetResult.NotEnoughZen or ResetResult.NotEnoughCoins or ResetResult.NotEnoughItems)
        {
            await player.ShowBlueMessageAsync(vietnamese
                ? $"Reset lần {next.ResetsAfter}: cấp {next.RequiredLevel}, {ResetTexts.CostText(next, true, names)}."
                : $"Reset {next.ResetsAfter}: level {next.RequiredLevel}, {ResetTexts.CostText(next, false, names)}.").ConfigureAwait(false);
        }
        else
        {
            await player.ShowBlueMessageAsync(ResetTexts.ResultText(next, vietnamese, names)).ConfigureAwait(false);
        }

        if (engine.Configuration.Relife.Enabled)
        {
            var relife = engine.PlanRelife(subject, now);
            await player.ShowBlueMessageAsync(vietnamese
                ? $"Relife {relife.RelifesAfter}: cần {relife.ResetsNeeded} reset, cấp {relife.RequiredLevel}, {ResetTexts.CostText(relife, true, names)}."
                : $"Relife {relife.RelifesAfter}: {relife.ResetsNeeded} resets, level {relife.RequiredLevel}, {ResetTexts.CostText(relife, false, names)}.").ConfigureAwait(false);
        }

        if (engine.Configuration.Delegated.Enabled)
        {
            await player.ShowBlueMessageAsync(vietnamese
                ? $"Ủy thác reset: {(record.Delegated ? "bật" : "tắt")} (/uythac on|off)."
                : $"Delegated reset: {(record.Delegated ? "on" : "off")} (/autoreset on|off).").ConfigureAwait(false);
        }

        await this.SendStateAsync(player, false, "info", next.Result).ConfigureAwait(false);
    }

    /// <summary>
    /// Switches the delegated reset of the player's character on or off.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="on">On or off; <c>null</c> to only tell.</param>
    /// <param name="requested">Whether the web game asked (then it gets the state even if it never asked before).</param>
    /// <returns>The task.</returns>
    public async ValueTask SetDelegatedAsync(Player player, bool? on, bool requested = false)
    {
        if (SubjectOf(player) is not { } subject)
        {
            return;
        }

        var engine = this.GetEngine();
        var vietnamese = IsVietnamese(player);
        if (!engine.Configuration.Delegated.Enabled)
        {
            await player.ShowBlueMessageAsync(vietnamese ? "Ủy thác reset đang tắt trên máy chủ này." : "Delegated resets are switched off on this server.").ConfigureAwait(false);
            await this.SendStateAsync(player, requested, "switch", ResetResult.NotAllowed).ConfigureAwait(false);
            return;
        }

        var value = on ?? engine.IsDelegated(subject.CharacterId);
        if (on is { } set)
        {
            engine.SetDelegated(subject, set);
            this.Save();
            player.Logger.LogInformation("Resets: {Player} switched the delegated reset {State}.", player, set ? "on" : "off");
        }

        await player.ShowBlueMessageAsync(ResetTexts.DelegatedText(value, vietnamese)).ConfigureAwait(false);
        await this.SendStateAsync(player, requested, "switch", ResetResult.Ok).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        var serverId = (gameContext as IGameServerContext)?.Id ?? 0;
        Contexts[serverId] = gameContext;
        if (now < this._nextTickUtc || Interlocked.Exchange(ref this._running, 1) == 1)
        {
            return;
        }

        try
        {
            var engine = this.GetEngine();
            this._nextTickUtc = now + TimeSpan.FromSeconds(Math.Clamp(engine.Configuration.Delegated.CheckSeconds, 5, 3600));
            if (!engine.Configuration.Delegated.Enabled)
            {
                return;
            }

            foreach (var player in gameContext.OfflinePlayerManager.OfflinePlayers)
            {
                try
                {
                    await this.TryDelegatedResetAsync(player).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    player.Logger.LogWarning(ex, "Resets: the delegated reset of {Player} failed.", player);
                }
            }

            this.Save();
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<ResetOptionsPlugIn>().LogError(ex, "Resets: the tick failed.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextTickUtc = DateTime.UtcNow;

    /// <summary>
    /// Does a delegated reset of an offline leveling character when it's due: the level is reached, the limits allow it
    /// and the costs are paid. The character stays where it hunts and keeps its build (if configured).
    /// </summary>
    /// <param name="player">The offline player.</param>
    /// <returns><c>true</c>, if it was reset.</returns>
    public async ValueTask<bool> TryDelegatedResetAsync(OfflinePlayer player)
    {
        if (SubjectOf(player) is not { } subject)
        {
            return false;
        }

        var engine = this.GetEngine();
        if (!engine.Configuration.Delegated.Enabled || !engine.IsDelegated(subject.CharacterId))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var plan = engine.PlanReset(subject, now, ResetSource.Delegated, CatchupCapPlugIn.GetEffectiveDailyResetCap(player.GameContext, now));
        if (plan.Result != ResetResult.Ok)
        {
            if (plan.Result != ResetResult.LevelTooLow)
            {
                engine.SetDelegatedBlocked(subject, plan.Result);
            }

            return false;
        }

        var paid = await TryPayAsync(player, plan).ConfigureAwait(false);
        if (paid != ResetResult.Ok)
        {
            engine.SetDelegatedBlocked(subject, paid);
            return false;
        }

        this.Change(player, subject, plan, engine.Configuration.Delegated.KeepBuild, now, ResetSource.Delegated);
        player.Logger.LogInformation("Resets: delegated reset {Resets} of {Player} while offline leveling.", plan.ResetsAfter, player);
        player.GameContext.PlugInManager.GetPlugInPoint<ICharacterLevelUpPlugIn>()?.CharacterLeveledUp(player);
        await SaveProgressAsync(player).ConfigureAwait(false);
        this.Save();
        return true;
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (player is OfflinePlayer || currentState != PlayerState.EnteredWorld || previousState != PlayerState.CharacterSelection || SubjectOf(player) is not { } subject)
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var engine = this.GetEngine();
                var count = engine.TakeDelegatedUntold(subject.CharacterId);
                if (count > 0)
                {
                    this.Save();
                    await player.ShowBlueMessageAsync(ResetTexts.AwayText(count, subject.Resets, IsVietnamese(player))).ConfigureAwait(false);
                    await this.SendStateAsync(player, false, "away", ResetResult.Ok, count).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogDebug(ex, "Resets: the note of {Player} wasn't sent.", player);
            }
        });
        return ValueTask.CompletedTask;
    }

    private static bool IsVietnamese(Player player) => player.Culture.TwoLetterISOLanguageName == "vi";

    private static async ValueTask SaveProgressAsync(Player player)
    {
        try
        {
            await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Resets: couldn't save {Player} right after the reset; the periodic save will retry.", player);
        }
    }

    /// <summary>
    /// Pays a plan: the items, the zen and the coins; nothing is taken when something is missing.
    /// </summary>
    private static async ValueTask<ResetResult> TryPayAsync(Player player, ResetPlan plan)
    {
        var items = ResetApplier.PickItems(player.Inventory?.Items ?? Enumerable.Empty<Item>(), plan);
        if (items is null)
        {
            return ResetResult.NotEnoughItems;
        }

        if (plan.Zen > int.MaxValue || player.Money < plan.Zen)
        {
            return ResetResult.NotEnoughZen;
        }

        if (plan.Zen > 0 && !player.TryRemoveMoney((int)plan.Zen))
        {
            return ResetResult.NotEnoughZen;
        }

        if (plan.Coins.Count > 0 && (player.Account is not { } account || !ResetApplier.TryTakeCoins(account, plan)))
        {
            if (plan.Zen > 0)
            {
                player.TryAddMoney((int)plan.Zen);
            }

            return ResetResult.NotEnoughCoins;
        }

        foreach (var item in items)
        {
            await player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
        }

        return ResetResult.Ok;
    }

    private void Change(Player player, ResetSubject subject, ResetPlan plan, bool keepBuild, DateTime now, ResetSource source)
    {
        var attributes = player.Attributes!;
        TopRacesPlugIn.ObserveCharacter(player.GameContext, subject.CharacterId, subject.Name, subject.Login, subject.Class, subject.Level, subject.Resets);
        ResetApplier.Apply(player.SelectedCharacter!, plan, keepBuild, a => attributes[a], (a, v) => attributes[a] = v);
        this.GetEngine().Record(subject, plan, now, source);
        CatchupCapPlugIn.Get(player.GameContext)?.GetEngine().ObserveResets(subject.Name, plan.ResetsAfter, now);
        TopRacesPlugIn.ObserveCharacter(player.GameContext, subject.CharacterId, subject.Name, subject.Login, subject.Class, plan.LevelAfter, plan.ResetsAfter);
    }

    private async ValueTask<bool> DoAsync(Player player, NonPlayerCharacter? npc, bool relife)
    {
        async ValueTask Show(string text)
        {
            if (npc is null)
            {
                await player.ShowBlueMessageAsync(text).ConfigureAwait(false);
            }
            else
            {
                await player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync(text, npc)).ConfigureAwait(false);
            }
        }

        var vietnamese = IsVietnamese(player);
        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld && npc is null)
        {
            await Show(player.GetLocalizedMessage(nameof(PlayerMessage.CantResetWithOpenedWindows))).ConfigureAwait(false);
            return false;
        }

        if (SubjectOf(player) is not { } subject || player.SelectedCharacter is not { } character)
        {
            await Show(player.GetLocalizedMessage(nameof(PlayerMessage.NotEnteredTheGame))).ConfigureAwait(false);
            return false;
        }

        var engine = this.GetEngine();
        var names = GiftCodesPlugIn.ItemNames(player.GameContext.Configuration);
        var now = DateTime.UtcNow;
        var plan = relife
            ? engine.PlanRelife(subject, now)
            : engine.PlanReset(subject, now, ResetSource.Game, CatchupCapPlugIn.GetEffectiveDailyResetCap(player.GameContext, now));
        var kind = relife ? "relife" : "reset";
        if (plan.Result == ResetResult.Ok)
        {
            plan.Result = await TryPayAsync(player, plan).ConfigureAwait(false);
        }

        if (plan.Result != ResetResult.Ok)
        {
            await Show(ResetTexts.ResultText(plan, vietnamese, names)).ConfigureAwait(false);
            await this.SendStateAsync(player, false, kind, plan.Result).ConfigureAwait(false);
            return false;
        }

        this.Change(player, subject, plan, false, now, ResetSource.Game);
        player.Logger.LogInformation("Resets: {Player} did {Kind} (resets {Resets}, relifes {Relifes}).", player, kind, plan.ResetsAfter, plan.RelifesAfter);
        this.Save();
        await Show(ResetTexts.ResultText(plan, vietnamese, names, true)).ConfigureAwait(false);
        await this.SendStateAsync(player, false, kind, ResetResult.Ok).ConfigureAwait(false);

        var configuration = engine.Configuration;
        if (configuration.LogOut)
        {
            if (configuration.MoveHome && character.CharacterClass?.HomeMap is { } home
                && await player.GameContext.GetMapAsync((ushort)home.Number).ConfigureAwait(false) is { SafeZoneSpawnGate: { } gate })
            {
                character.PositionX = (byte)Rand.NextInt(gate.X1, gate.X2);
                character.PositionY = (byte)Rand.NextInt(gate.Y1, gate.Y2);
                character.CurrentMap = gate.Map;
                player.Rotation = gate.Direction;
            }

            await SaveProgressAsync(player).ConfigureAwait(false);
            await this._logoutAction.LogoutAsync(player, LogoutType.BackToCharacterSelection).ConfigureAwait(false);
            return true;
        }

        await SaveProgressAsync(player).ConfigureAwait(false);
        if (configuration.MoveHome && character.CharacterClass?.HomeMap is { } homeMap
            && await player.GameContext.GetMapAsync((ushort)homeMap.Number).ConfigureAwait(false) is { SafeZoneSpawnGate: { } spawnGate })
        {
            await player.WarpToAsync(spawnGate).ConfigureAwait(false);
        }

        await player.InvokeViewPlugInAsync<IUpdateCharacterBaseStatsPlugIn>(p => p.UpdateCharacterBaseStatsAsync()).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IUpdateLevelPlugIn>(p => p.UpdateLevelAsync()).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IUpdateCharacterStatsPlugIn>(p => p.UpdateCharacterStatsAsync()).ConfigureAwait(false);
        return true;
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
