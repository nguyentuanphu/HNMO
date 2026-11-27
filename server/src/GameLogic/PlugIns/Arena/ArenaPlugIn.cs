// <copyright file="ArenaPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// The HNMO Arena's characters (owner's choice, 2026-11-20): when a character is picked in the Arena program, it becomes
/// the arena character of its class: the template's level and stats, the template's gear (made again every time, so
/// nothing carries over), potions, no zen, on the arena map. Nothing happens on any other server.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.ArenaPlugIn_Name), Description = nameof(Properties.PlugInResources.ArenaPlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("8E4C2A71-6B93-4D05-A1F8-3C7E9D2B5A64")]
public sealed class ArenaPlugIn : IFeaturePlugIn, IPlayerStateChangedPlugIn, IPeriodicTaskPlugIn, ISupportCustomConfiguration<ArenaConfiguration>, ISupportDefaultCustomConfiguration
{
    private const byte FirstBagSlot = 12;

    /// <summary>The attribute systems the class balance was put on already (once per entry; map changes enter the world again).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> Balanced = new();

    private readonly object _duelsGate = new();
    private ArenaDuels? _duels;
    private ArenaTeams? _teams;
    private ArenaRoyale? _royale;
    private ArenaGuildWar? _guildWar;
    private DateTime _nextTeamTickUtc;
    private DateTime _nextPublishUtc;
    private DateTime _nextMatchUtc;
    private DateTime _nextReturnUtc;

    /// <inheritdoc />
    public ArenaConfiguration? Configuration { get; set; }

    /// <summary>Gets the ranked 1v1 duels of this game server.</summary>
    public ArenaDuels Duels
    {
        get
        {
            lock (this._duelsGate)
            {
                var cfg = this.Configuration ?? new ArenaConfiguration();
                return this._duels ??= new ArenaDuels(cfg.FileName, cfg);
            }
        }
    }

    /// <summary>Gets the 5v5 team battles of this game server.</summary>
    public ArenaTeams Teams
    {
        get
        {
            lock (this._duelsGate)
            {
                return this._teams ??= new ArenaTeams(this.Configuration ?? new ArenaConfiguration()) { MapBusy = () => this.GuildWar.Busy };
            }
        }
    }

    /// <summary>Gets the battle royale of this game server.</summary>
    public ArenaRoyale Royale
    {
        get
        {
            lock (this._duelsGate)
            {
                return this._royale ??= new ArenaRoyale(this.Configuration ?? new ArenaConfiguration());
            }
        }
    }

    /// <summary>Gets the guild war of this game server.</summary>
    public ArenaGuildWar GuildWar
    {
        get
        {
            lock (this._duelsGate)
            {
                return this._guildWar ??= new ArenaGuildWar(this.Configuration ?? new ArenaConfiguration(), this.Duels.Store);
            }
        }
    }

    /// <summary>Gets the active <see cref="ArenaPlugIn"/> of a game context.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in, or <c>null</c>.</returns>
    public static ArenaPlugIn? Get(IGameContext? gameContext) => gameContext?.FeaturePlugIns.GetPlugIn<ArenaPlugIn>();

    /// <summary>Gets a value indicating whether the player is currently taking part in an active Arena team battle, battle royale, or guild war.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if fighting in one of the Arena's multi-player battles.</returns>
    public bool IsInBattle(Player player) =>
        this.Teams.SideOf(player) != 0 || this.Royale.IsFighter(player) || this.GuildWar.SideOf(player) != 0;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ArenaConfiguration();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (!ArenaRules.Active)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var logger = gameContext.LoggerFactory.CreateLogger<ArenaPlugIn>();
        if (now >= this._nextMatchUtc)
        {
            this._nextMatchUtc = now.AddSeconds(2);
            await this.Duels.MatchAsync(logger).ConfigureAwait(false);
        }

        if (now >= this._nextTeamTickUtc)
        {
            this._nextTeamTickUtc = now.AddSeconds(1);
            await this.Teams.TickAsync(gameContext, this.Duels, logger).ConfigureAwait(false);
            await this.Royale.TickAsync(gameContext, this.Teams, this.Duels, logger).ConfigureAwait(false);
            await this.GuildWar.TickAsync(gameContext, this.Teams, this.Royale, this.Duels, logger).ConfigureAwait(false);
        }

        if (now >= this._nextPublishUtc && Persistence.ArenaShared.Current is { } shared)
        {
            // The home servers show and announce the scheduled battles from what the Arena publishes.
            this._nextPublishUtc = now.AddMinutes(1);
            var cfg = this.Configuration ?? new ArenaConfiguration();
            try
            {
                shared.PublishSchedule("royale", string.Empty, cfg.RoyaleTimes, this.Royale.NextStartUtc);
                shared.PublishSchedule("guildwar", cfg.GuildWarDays, cfg.GuildWarTimes, this.GuildWar.NextStartUtc);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Arena: the schedule couldn't be published for the home servers.");
            }
        }

        if (now >= this._nextReturnUtc)
        {
            // After a duel the game moves both players to the duel exit: arena characters belong on the arena map.
            this._nextReturnUtc = now.AddSeconds(5);
            var cfg = this.Configuration ?? new ArenaConfiguration();
            if (cfg.HomeGate(gameContext.Configuration) is { } gate)
            {
                foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
                {
                    if (player.PlayerState.CurrentState == PlayerState.EnteredWorld && player.DuelRoom is null && player.IsAlive
                        && this.Teams.SideOf(player) == 0 && !this.Royale.IsAlive(player) && this.GuildWar.SideOf(player) == 0
                        && !player.IsTeleporting && player.CurrentMap is { } current && current.MapId != cfg.MapNumber)
                    {
                        await player.WarpToAsync(gate).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextMatchUtc = DateTime.MinValue;
    }

    /// <summary>An arena character enters with full health, mana, shield and ability.</summary>
    /// <param name="player">The player.</param>
    /// <param name="previousState">The previous state.</param>
    /// <param name="currentState">The current state.</param>
    /// <returns>The task.</returns>
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (!ArenaRules.Active || currentState != PlayerState.EnteredWorld || player.Attributes is not { } attributes)
        {
            return ValueTask.CompletedTask;
        }

        // The class balance (admin, per class): the damage dealt and taken, put on once per entry.
        if (player.SelectedCharacter?.CharacterClass is { } characterClass && this.TemplateOf(characterClass.Number) is { } template
            && (template.DamagePercent != 100 || template.DefensePercent != 100)
            && Balanced.TryAdd(attributes, attributes))
        {
            if (template.DamagePercent != 100)
            {
                attributes.AddElement(new ConstantElement(Math.Clamp(template.DamagePercent, 10, 500) / 100f, AggregateType.Multiplicate), Stats.AttackDamageIncrease);
            }

            if (template.DefensePercent != 100)
            {
                attributes.AddElement(new ConstantElement(100f / Math.Clamp(template.DefensePercent, 10, 500), AggregateType.Multiplicate), Stats.DamageReceiveDecrement);
            }
        }

        attributes[Stats.CurrentHealth] = attributes[Stats.MaximumHealth];
        attributes[Stats.CurrentMana] = attributes[Stats.MaximumMana];
        attributes[Stats.CurrentShield] = attributes[Stats.MaximumShield];
        attributes[Stats.CurrentAbility] = attributes[Stats.MaximumAbility];
        return ValueTask.CompletedTask;
    }

    /// <summary>The template of a character class, if there is one.</summary>
    /// <param name="classNumber">The character class number.</param>
    /// <returns>The template.</returns>
    public ArenaClassTemplate? TemplateOf(int classNumber) =>
        (this.Configuration ?? new ArenaConfiguration()).Templates.FirstOrDefault(t => t.ClassFamily == classNumber / 4);

    /// <summary>
    /// Makes a character the arena character of its class, before it enters the world (only in the Arena program).
    /// </summary>
    /// <param name="player">The player (its persistence context and game data).</param>
    /// <param name="character">The picked character.</param>
    /// <returns>The task.</returns>
    public async ValueTask ApplyTemplateAsync(Player player, Character character)
    {
        if (!ArenaRules.Active || character.CharacterClass is not { } characterClass || this.TemplateOf(characterClass.Number) is not { } template)
        {
            return;
        }

        var cfg = this.Configuration ?? new ArenaConfiguration();
        var gameConfiguration = player.GameContext.Configuration;
        var level = Math.Clamp(template.Level, 1, gameConfiguration.MaximumLevel);
        SetStat(character, Stats.Level, level);
        SetStat(character, Stats.BaseStrength, template.Strength);
        SetStat(character, Stats.BaseAgility, template.Agility);
        SetStat(character, Stats.BaseVitality, template.Vitality);
        SetStat(character, Stats.BaseEnergy, template.Energy);
        SetStat(character, Stats.BaseLeadership, template.Leadership);
        character.LevelUpPoints = 0;
        var table = player.GameContext.ExperienceTable;
        character.Experience = level < table.Length ? table[level] : character.Experience;

        if (character.Inventory is { } inventory)
        {
            // The gear is made again on every entry: whatever the character had in the arena goes.
            foreach (var old in inventory.Items.ToList())
            {
                inventory.Items.Remove(old);
                await player.PersistenceContext.DeleteAsync(old).ConfigureAwait(false);
            }

            inventory.Money = 0;
            foreach (var entry in template.Items)
            {
                this.AddItem(player, inventory, gameConfiguration, entry);
            }

            if (cfg.HealingPotions > 0)
            {
                this.AddItem(player, inventory, gameConfiguration, new ArenaTemplateItem { Slot = FirstBagSlot, Group = 14, Number = 3, Durability = cfg.HealingPotions });
            }
        }

        if (cfg.HomeGate(gameConfiguration) is { Map: { } map } gate)
        {
            character.CurrentMap = map;
            character.PositionX = (byte)Rand.NextInt(gate.X1, gate.X2 + 1);
            character.PositionY = (byte)Rand.NextInt(gate.Y1, gate.Y2 + 1);
        }

        player.Logger.LogInformation("Arena: {Name} plays the {Template} arena character (level {Level}).", character.Name, template.Name, level);
    }

    private static void SetStat(Character character, AttributeDefinition definition, int value)
    {
        if (character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id) is { } attribute)
        {
            attribute.Value = value;
        }
    }

    private void AddItem(Player player, ItemStorage inventory, GameConfiguration gameConfiguration, ArenaTemplateItem entry)
    {
        if (gameConfiguration.Items.FirstOrDefault(d => d.Group == entry.Group && d.Number == entry.Number) is not { } definition)
        {
            player.Logger.LogWarning("Arena: the template item {Group} {Number} doesn't exist; it's left out.", entry.Group, entry.Number);
            return;
        }

        var level = Math.Min(entry.Level, definition.MaximumItemLevel);
        var specification = new ItemSpecification(entry.Group, entry.Number, level, entry.Skill, entry.Luck, entry.OptionLevel, entry.ExcellentOptions);
        if (!GameMasterItemFactory.IsValid(definition, specification))
        {
            specification = new ItemSpecification(entry.Group, entry.Number, level, entry.Skill && definition.Skill is not null);
        }

        var temporary = GameMasterItemFactory.CreateItem(definition, specification);
        if (entry.Durability > 0)
        {
            temporary.Durability = entry.Durability;
        }

        var item = temporary.MakePersistent(player.PersistenceContext);
        item.ItemSlot = entry.Slot;
        inventory.Items.Add(item);
    }
}
