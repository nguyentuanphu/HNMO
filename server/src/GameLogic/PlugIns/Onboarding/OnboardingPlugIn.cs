// <copyright file="OnboardingPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// HNMO's new-player guide and starter kit: an ordered list of steps (talk to an NPC, walk somewhere, defeat monsters, use
/// an item or a skill, reach a level, join a party, add stat points, open the cash shop or the missions window), each
/// checked by the server and with its own reward, a final reward, and a starter kit (items, zen, coins, stat points, an
/// EXP buff) for new characters. The web game shows it in a guide panel. The progress is kept in a JSON file in the save
/// folder. Rewards which don't fit the bag wait until they do.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.OnboardingPlugIn_Name), Description = nameof(PlugInResources.OnboardingPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A3D58E21-7C46-4B9F-8E13-5F20C6B47D98")]
public class OnboardingPlugIn : IPeriodicTaskPlugIn, IAttackableGotKilledPlugIn, IAttackableGotHitPlugIn, IItemConsumedPlugIn,
    INpcTalkStartedPlugIn, IPlayerStateChangedPlugIn, ICharacterCreatedPlugIn,
    ISupportCustomConfiguration<OnboardingConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan DeliverInterval = TimeSpan.FromSeconds(30);

    /// <summary>The players with a running guide and the kind of their current step, so the hooks stay cheap.</summary>
    private readonly ConcurrentDictionary<Player, OnboardingStepKind?> _current = new();

    private DateTime _nextCheckUtc = DateTime.UtcNow;

    private DateTime _nextDeliverUtc = DateTime.UtcNow + DeliverInterval;

    private (OnboardingConfiguration Configuration, OnboardingEngine Engine)? _engine;

    /// <inheritdoc />
    public OnboardingConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static OnboardingPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<OnboardingPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new OnboardingConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public OnboardingEngine GetEngine()
    {
        var configuration = this.Configuration ??= new OnboardingConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new OnboardingEngine(configuration, OnboardingStore.Open(configuration.FileName));
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>
    /// Sends the state to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="boardEvent">What happened.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, OnboardingBoardEvent? boardEvent = null)
    {
        if (!TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var engine = this.GetEngine();
        var kitNew = requested && engine.TakeKitNotice(characterId);
        var json = OnboardingStateWriter.WriteState(engine, characterId, FactsOf(player), this.TargetOf(player, engine.CurrentStep(characterId)), kitNew, ClassOf(player), boardEvent);
        await player.InvokeViewPlugInAsync<IHnmoOnboardingViewPlugIn>(p => p.ShowOnboardingAsync(json, requested)).ConfigureAwait(false);
        this.Save();
    }

    /// <summary>
    /// Ends the guide of the player (the player confirmed it in the client).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask SkipAsync(Player player)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var skipped = this.GetEngine().Skip(characterId, DateTime.UtcNow);
        this.Refresh(player);
        if (skipped)
        {
            player.Logger.LogInformation("Onboarding: {Player} skipped the guide.", player);
        }

        await this.SendStateAsync(player, true, new OnboardingBoardEvent("skip", skipped ? "Ok" : "NotRunning")).ConfigureAwait(false);
    }

    /// <summary>
    /// Skips the current step of the player, when it's optional.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask SkipStepAsync(Player player)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var step = this.GetEngine().CurrentStep(characterId);
        var advance = this.GetEngine().SkipStep(characterId, DateTime.UtcNow);
        var skipped = step is { Optional: true };
        await this.SendStateAsync(player, true, new OnboardingBoardEvent("skipStep", skipped ? "Ok" : "NotOptional", step is null ? null : [step.Id])).ConfigureAwait(false);
        if (advance.Finished)
        {
            await this.AfterAdvanceAsync(player, advance).ConfigureAwait(false);
        }
        else
        {
            this.Refresh(player);
            await this.CheckAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives the waiting rewards of the player (the take button).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask ClaimAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        await this.SendStateAsync(player, true, new OnboardingBoardEvent("claim", left > 0 ? "BagFull" : given > 0 ? "Ok" : "Nothing", Count: given)).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the missions window being opened (the web game asks for the missions then).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public ValueTask MissionsOpenedAsync(Player player) => this.ReportAsync(player, OnboardingStepKind.OpenMissions, 1, null);

    /// <inheritdoc />
    public void CharacterCreated(Account account, Character createdCharacter, IContext persistenceContext, GameConfiguration gameConfiguration, ILogger logger)
    {
        if (account.IsBot || createdCharacter.Id == Guid.Empty)
        {
            return;
        }

        try
        {
            var points = this.GetEngine().CharacterCreated(createdCharacter.Id.ToString(), account.LoginName, createdCharacter.Name, DateTime.UtcNow);
            if (points > 0)
            {
                createdCharacter.LevelUpPoints += points;
            }

            this.Save();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Onboarding: the new character {Name} wasn't noted.", createdCharacter.Name);
        }
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld || previousState != PlayerState.CharacterSelection)
        {
            if (currentState == PlayerState.CharacterSelection || currentState == PlayerState.Disconnected || currentState == PlayerState.LoginScreen)
            {
                this._current.TryRemove(player, out _);
            }

            return ValueTask.CompletedTask;
        }

        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out var login))
        {
            return ValueTask.CompletedTask;
        }

        var engine = this.GetEngine();
        var character = player.SelectedCharacter!;
        engine.See(characterId, login, character.Name, character.CreateDate, player.Level, DateTime.UtcNow);
        var kit = engine.TakeStarterKit(characterId, login, ClassOf(player), DateTime.UtcNow);
        if (kit is not null)
        {
            player.Logger.LogInformation("Onboarding: {Player} gets the starter kit.", player);
        }

        this.Refresh(player);
        this.Save();
        _ = Task.Run(async () =>
        {
            try
            {
                var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
                if (given > 0 || left > 0)
                {
                    await this.SendStateAsync(player, false, new OnboardingBoardEvent("reward", left > 0 ? "BagFull" : "Ok", Count: given)).ConfigureAwait(false);
                }

                await this.CheckAsync(player).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Onboarding: the login of {Player} wasn't handled.", player);
            }
        });
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (now < this._nextCheckUtc)
        {
            return;
        }

        this._nextCheckUtc = now + CheckInterval;
        var deliver = now >= this._nextDeliverUtc;
        if (deliver)
        {
            this._nextDeliverUtc = now + DeliverInterval;
        }

        try
        {
            foreach (var player in this._current.Keys.Where(p => p.GameContext == gameContext).ToList())
            {
                if (!IsCounted(player) || player.PlayerState.CurrentState == PlayerState.Disconnected)
                {
                    this._current.TryRemove(player, out _);
                    continue;
                }

                await this.CheckAsync(player).ConfigureAwait(false);
            }

            if (deliver)
            {
                var engine = this.GetEngine();
                foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
                {
                    if (IsCounted(player) && TryGetKeys(player, out var characterId, out _) && engine.Owed(characterId).Count > 0)
                    {
                        var (given, _) = await this.DeliverAsync(player).ConfigureAwait(false);
                        if (given > 0)
                        {
                            await this.SendStateAsync(player, false, new OnboardingBoardEvent("reward", "Ok", Count: given)).ConfigureAwait(false);
                        }
                    }
                }
            }

            this.Save();
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<OnboardingPlugIn>().LogError(ex, "Onboarding: the tick failed.");
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextCheckUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Monster { SummonedBy: null } monster)
        {
            return ValueTask.CompletedTask;
        }

        var player = killer as Player ?? (killer as IPlayerSurrogate)?.Owner ?? (killer as Monster)?.SummonedBy;
        if (player is null || !this.Wants(player, OnboardingStepKind.KillMonsters))
        {
            return ValueTask.CompletedTask;
        }

        var number = monster.Definition.Number;
        var map = monster.CurrentMap?.Definition.Number ?? -1;
        return this.ReportAsync(player, OnboardingStepKind.KillMonsters, 1, s => (s.MonsterNumber <= 0 || s.MonsterNumber == number) && (s.MapNumber < 0 || s.MapNumber == map));
    }

    /// <inheritdoc />
    public void AttackableGotHit(IAttackable attackable, IAttacker attacker, HitInfo hitInfo)
    {
        if (attacker is not Player player || hitInfo.SkillEntry?.Skill is not { } skill || !this.Wants(player, OnboardingStepKind.UseSkill))
        {
            return;
        }

        var number = skill.Number;
        this.Fire(player, () => this.ReportAsync(player, OnboardingStepKind.UseSkill, 1, s => s.SkillNumber <= 0 || s.SkillNumber == number));
    }

    /// <inheritdoc />
    public void ItemConsumed(Player player, Item item, Item? targetItem)
    {
        if (item.Definition is not { } definition || !this.Wants(player, OnboardingStepKind.UseItem))
        {
            return;
        }

        var group = (short)definition.Group;
        var number = definition.Number;
        this.Fire(player, () => this.ReportAsync(player, OnboardingStepKind.UseItem, 1, s => (s.ItemGroup < 0 || s.ItemGroup == group) && (s.ItemNumber < 0 || s.ItemNumber == number)));
    }

    /// <inheritdoc />
    public ValueTask NpcTalkStartedAsync(Player player, NonPlayerCharacter npc)
    {
        if (!this.Wants(player, OnboardingStepKind.TalkToNpc))
        {
            return ValueTask.CompletedTask;
        }

        var number = npc.Definition.Number;
        return this.ReportAsync(player, OnboardingStepKind.TalkToNpc, 1, s => s.NpcNumber <= 0 || s.NpcNumber == number);
    }

    private static bool IsCounted(Player player) =>
        player is not OfflinePlayer && player.Account is { IsBot: false } && !player.IsTemplatePlayer && player.SelectedCharacter is not null;

    private static bool TryGetKeys(Player player, out string characterId, out string login)
    {
        characterId = player.SelectedCharacter?.Id.ToString() ?? string.Empty;
        login = player.Account?.LoginName ?? string.Empty;
        return characterId.Length > 0 && login.Length > 0 && player.SelectedCharacter!.Id != Guid.Empty;
    }

    private static int ClassOf(Player player) => player.SelectedCharacter?.CharacterClass?.Number ?? -1;

    private static OnboardingFacts? FactsOf(Player player)
    {
        if (player.SelectedCharacter is not { } character || player.Attributes is not { } attributes)
        {
            return null;
        }

        var stats = (int)(attributes[Stats.BaseStrength] + attributes[Stats.BaseAgility] + attributes[Stats.BaseVitality] + attributes[Stats.BaseEnergy] + attributes[Stats.BaseLeadership]);
        return new OnboardingFacts(
            player.Level,
            player.CurrentMap?.Definition.Number ?? -1,
            player.Position.X,
            player.Position.Y,
            player.Party is not null,
            stats,
            character.LearnedSkills.Count,
            player.IsCashShopOpen);
    }

    /// <summary>
    /// Gives the zen, items and buff of a reward, all or nothing.
    /// </summary>
    private static async ValueTask<bool> GiveGoodsAsync(Player player, OnboardingOwedReward reward)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var added = new List<Item>();
        async ValueTask TakeBackAsync()
        {
            foreach (var back in added)
            {
                await inventory.RemoveItemAsync(back).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(back).ConfigureAwait(false);
            }
        }

        foreach (var entry in reward.Items.Where(i => i.Count > 0))
        {
            var definition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == entry.Group && d.Number == entry.Number);
            if (definition is null)
            {
                player.Logger.LogWarning("Onboarding: the reward item {Group} {Number} doesn't exist; it's left out.", entry.Group, entry.Number);
                continue;
            }

            var specification = new ItemSpecification((byte)entry.Group, entry.Number, Math.Min(entry.Level, definition.MaximumItemLevel), entry.Skill, entry.Luck, entry.OptionLevel, entry.ExcellentOptions);
            if (!GameMasterItemFactory.IsValid(definition, specification))
            {
                player.Logger.LogWarning("Onboarding: the options of the item {Group} {Number} don't fit it; it's given without them.", entry.Group, entry.Number);
                specification = new ItemSpecification((byte)entry.Group, entry.Number, Math.Min(entry.Level, definition.MaximumItemLevel), entry.Skill && definition.Skill is not null);
            }

            for (var i = 0; i < Math.Min(entry.Count, 50); i++)
            {
                var temporary = GameMasterItemFactory.CreateItem(definition, specification);
                if (temporary.IsStackable())
                {
                    temporary.Durability = Math.Clamp(entry.Durability == 0 ? definition.Durability : entry.Durability, 1, Math.Max(1, (int)definition.Durability));
                }
                else
                {
                    var maximum = temporary.GetMaximumDurabilityOfOnePiece();
                    temporary.Durability = entry.Durability == 0 ? maximum : Math.Min(entry.Durability, maximum);
                }

                if (inventory.CheckInvSpace(temporary) is not { } slot)
                {
                    await TakeBackAsync().ConfigureAwait(false);
                    return false;
                }

                var item = temporary.MakePersistent(player.PersistenceContext);
                if (!await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
                {
                    player.PersistenceContext.Detach(item);
                    await TakeBackAsync().ConfigureAwait(false);
                    return false;
                }

                added.Add(item);
            }
        }

        if (reward.Zen > 0 && !player.TryAddMoney(reward.Zen))
        {
            await TakeBackAsync().ConfigureAwait(false);
            return false;
        }

        foreach (var item in added)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (reward.Zen > 0)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        if (reward.BuffMinutes > 0)
        {
            await ApplyBuffAsync(player, reward).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Gives the effect of an item of the game's data (e.g. the Seal of Ascension) for some minutes.
    /// </summary>
    private static async ValueTask ApplyBuffAsync(Player player, OnboardingOwedReward reward)
    {
        var effectDefinition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == reward.BuffItemGroup && d.Number == reward.BuffItemNumber)?.ConsumeEffect;
        if (effectDefinition is null || player.Attributes is not { } attributes)
        {
            player.Logger.LogWarning("Onboarding: the item {Group} {Number} has no effect; the buff is left out.", reward.BuffItemGroup, reward.BuffItemNumber);
            return;
        }

        var boosts = effectDefinition.PowerUpDefinitions
            .Where(def => def.Boost is not null && def.TargetAttribute is not null)
            .Select(def => new MagicEffect.ElementWithTarget(attributes.CreateElement(def), def.TargetAttribute!))
            .ToArray();
        if (boosts.Length == 0)
        {
            return;
        }

        if (player.MagicEffectList.TryGetActiveEffectOfSubType(effectDefinition.SubType) is { } existing)
        {
            await existing.DisposeAsync().ConfigureAwait(false);
        }

        await player.MagicEffectList.AddEffectAsync(new MagicEffect(TimeSpan.FromMinutes(reward.BuffMinutes), effectDefinition, boosts)).ConfigureAwait(false);
    }

    private bool Wants(Player player, OnboardingStepKind kind) => this._current.TryGetValue(player, out var current) && current == kind;

    private void Fire(Player player, Func<ValueTask> action) => _ = Task.Run(async () =>
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Onboarding: a step of {Player} wasn't counted.", player);
        }
    });

    /// <summary>Remembers the kind of the player's current step (or forgets the player when the guide doesn't run).</summary>
    private void Refresh(Player player)
    {
        if (!TryGetKeys(player, out var characterId, out _) || this.GetEngine().CurrentStep(characterId) is not { } step)
        {
            this._current.TryRemove(player, out _);
            return;
        }

        this._current[player] = step.Kind;
    }

    private async ValueTask ReportAsync(Player player, OnboardingStepKind kind, int amount, Func<OnboardingStep, bool>? matches)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return;
        }

        var engine = this.GetEngine();
        var before = engine.Progress(characterId, null);
        var advance = engine.Report(characterId, kind, amount, matches, DateTime.UtcNow);
        if (advance.Any)
        {
            await this.AfterAdvanceAsync(player, advance).ConfigureAwait(false);
            return;
        }

        var after = engine.Progress(characterId, null);
        if (after is { } a && before is { } b && a.Step.Id == b.Step.Id && a.Value != b.Value)
        {
            // A count went up: the panel shows it.
            await this.SendStateAsync(player, false).ConfigureAwait(false);
        }
    }

    private async ValueTask CheckAsync(Player player)
    {
        if (!TryGetKeys(player, out var characterId, out _) || FactsOf(player) is not { } facts)
        {
            return;
        }

        var advance = this.GetEngine().Check(characterId, facts, DateTime.UtcNow);
        if (advance.Any)
        {
            await this.AfterAdvanceAsync(player, advance).ConfigureAwait(false);
        }
    }

    private async ValueTask AfterAdvanceAsync(Player player, OnboardingAdvance advance)
    {
        this.Refresh(player);
        if (advance.Completed.Count > 0)
        {
            player.Logger.LogInformation("Onboarding: {Player} did {Steps}.", player, string.Join(", ", advance.Completed.Select(s => s.Id)));
        }

        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        var ids = advance.Completed.Select(s => s.Id).ToList();
        await this.SendStateAsync(player, false, new OnboardingBoardEvent(advance.Finished ? "final" : "step", left > 0 ? "BagFull" : "Ok", ids, given)).ConfigureAwait(false);
        if (!advance.Finished)
        {
            // The next step may be done already (e.g. the level is reached).
            await this.CheckAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives the waiting rewards of a player: the coins at once, the rest (zen, items, stat points, the buff) when it fits.
    /// </summary>
    /// <returns>The rewards given completely and the ones which still wait.</returns>
    private async ValueTask<(int Given, int Left)> DeliverAsync(Player player)
    {
        if (!IsCounted(player) || !TryGetKeys(player, out var characterId, out _))
        {
            return (0, 0);
        }

        var engine = this.GetEngine();
        var owed = engine.TakeOwed(characterId);
        var keep = new List<OnboardingOwedReward>();
        var given = 0;
        foreach (var reward in owed)
        {
            try
            {
                if (!reward.CoinsGiven && reward.Coins > 0)
                {
                    await MissionCoinGranter.GrantRewardAsync(player.GameContext, new CoinGrant(reward.Login, reward.CoinType, reward.Coins, reward.Reason, reward.Reference), OnboardingEngine.GrantedBy).ConfigureAwait(false);
                    reward.CoinsGiven = true;
                }

                if (!reward.HasGoods || await GiveGoodsAsync(player, reward).ConfigureAwait(false))
                {
                    given++;
                    player.Logger.LogInformation("Onboarding: {Player} got the reward {Reference}.", player, reward.Reference);
                }
                else
                {
                    keep.Add(reward);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Onboarding: the reward {Reference} of {Player} wasn't given; it waits.", reward.Reference, player);
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(characterId, keep);
        this.Save();
        return (given, keep.Count);
    }

    /// <summary>
    /// Where the current step leads: the NPC (the nearest merchant on the map for "any NPC"), the spot or the map.
    /// </summary>
    private OnboardingTarget? TargetOf(Player player, OnboardingStep? step)
    {
        if (step is null)
        {
            return null;
        }

        var maps = player.GameContext.Configuration.Maps;
        var here = player.CurrentMap?.Definition;
        string? MapName(int number) => maps.FirstOrDefault(m => m.Number == number)?.Name.ValueInNeutralLanguage;
        switch (step.Kind)
        {
            case OnboardingStepKind.WalkTo:
                return new OnboardingTarget(step.MapNumber, step.X, step.Y, MapName(step.MapNumber));
            case OnboardingStepKind.EnterMap:
                return new OnboardingTarget(step.MapNumber, -1, -1, MapName(step.MapNumber));
            case OnboardingStepKind.TalkToNpc:
                var spawns = (step.NpcNumber > 0
                        ? maps.SelectMany(m => m.MonsterSpawns).Where(s => s.MonsterDefinition?.Number == step.NpcNumber)
                        : (here?.MonsterSpawns ?? []).Where(s => s.MonsterDefinition is { MerchantStore.Items.Count: > 0 }))
                    .Where(s => s.GameMap is not null)
                    .ToList();
                var position = player.Position;
                var best = spawns
                    .OrderBy(s => s.GameMap == here ? 0 : 1)
                    .ThenBy(s => Math.Max(Math.Abs(((s.X1 + s.X2) / 2) - position.X), Math.Abs(((s.Y1 + s.Y2) / 2) - position.Y)))
                    .FirstOrDefault();
                return best is null
                    ? null
                    : new OnboardingTarget(best.GameMap!.Number, (best.X1 + best.X2) / 2, (best.Y1 + best.Y2) / 2, best.MonsterDefinition?.Designation.ValueInNeutralLanguage);
            default:
                return null;
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
