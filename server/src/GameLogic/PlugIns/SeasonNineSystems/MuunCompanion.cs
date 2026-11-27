// <copyright file="MuunCompanion.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Persistence;

/// <summary>
/// The Season 9 Muun companions of the players: one Muun in the bag is the active one, and its bonus applies while its
/// condition (map, total level, hour, weekday) holds. See <c>docs/audit/s9-muun-design.md</c>.
/// </summary>
/// <remarks>
/// The Muun stays a bag item; which one is active is a row of the character's state (feature <see cref="FeatureKey"/>),
/// with the converted energy. The bonus is one stat element, always removed before a new one is added, so equipping
/// again never stacks. Only in a Season 9 realm with the Season 9 systems and Muuns switched on.
/// </remarks>
public sealed class MuunCompanion
{
    /// <summary>The feature key of the state rows.</summary>
    public const string FeatureKey = "s9.muun";

    /// <summary>The state of a switched-off Muun system.</summary>
    public const string DisabledState = "{\"v\":1,\"enabled\":false,\"active\":null,\"bag\":[],\"energy\":0}";

    /// <summary>The Evolution Stone.</summary>
    internal const short EvolutionStoneNumber = 211;

    /// <summary>The Muun Energy Converter.</summary>
    internal const short EnergyConverterNumber = 239;

    /// <summary>The item group of the Muuns, the stone and the converter.</summary>
    internal const byte MuunGroup = 13;

    private const int DefaultLevelUpCost = 10;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SeasonNineSystemsPlugIn _plugIn;

    private readonly ConditionalWeakTable<Player, Applied> _applied = new();

    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MuunCompanion"/> class.
    /// </summary>
    /// <param name="plugIn">The plug-in whose settings and season check apply.</param>
    internal MuunCompanion(SeasonNineSystemsPlugIn plugIn) => this._plugIn = plugIn;

    /// <summary>Gets or sets the clock (tests).</summary>
    internal Func<DateTime> UtcNow { get; set; } = static () => DateTime.UtcNow;

    /// <summary>Gets or sets the state file whose rows hold the state (tests); the HNMO state file by default.</summary>
    internal string? StateFile { get; set; }

    private SeasonNineSystemsConfiguration Configuration => this._plugIn.Configuration ?? new SeasonNineSystemsConfiguration();

    private bool IsOn => this._plugIn.IsActive() && this.Configuration.MuunEnabled;

    /// <summary>The stat and the factor an option type adds to; <c>null</c> for options which aren't stats (skills).</summary>
    /// <param name="optionType">The option type.</param>
    /// <returns>The stat and the factor of one option point.</returns>
    public static (AttributeDefinition Stat, float Factor)? StatOf(int optionType) => optionType switch
    {
        1 => (Stats.BaseDamageBonus, 1f),
        7 => (Stats.BaseMaxDamageBonus, 1f),
        2 => (Stats.DefenseBase, 1f),
        3 => (Stats.ExcellentDamageBonus, 1f),
        4 => (Stats.CriticalDamageBonus, 1f),
        5 => (Stats.SkillDamageBonus, 1f),

        // The options marked non-PvP count only on hits on monsters.
        8 => (Stats.BaseDamageBonusPvm, 1f),
        14 => (Stats.BaseMaxDamageBonusPvm, 1f),
        10 => (Stats.ExcellentDamageBonusPvm, 1f),
        11 => (Stats.CriticalDamageChancePvm, 0.01f),
        12 => (Stats.SkillDamageBonusPvm, 1f),
        _ => null,
    };

    /// <summary>Gets a value indicating whether <paramref name="optionType"/> is a non-PvP (PvM-only) option, including attack skill type 53.</summary>
    public static bool IsPvmOnlyOption(int optionType) => optionType is 8 or 10 or 11 or 12 or 14 or 53;

    /// <summary>Gets a value indicating whether <paramref name="optionType"/> triggers a periodic attack skill (50 = all targets, 53 = non-PvP).</summary>
    public static bool IsAttackSkillOption(int optionType) => optionType is 50 or 53;

    /// <summary>Makes the Muun in a bag slot the active one.</summary>
    /// <param name="player">The player.</param>
    /// <param name="slot">The inventory slot.</param>
    /// <returns>A task.</returns>
    public async ValueTask EquipAsync(Player player, byte slot)
    {
        if (!await this.EnsureOnAsync(player).ConfigureAwait(false))
        {
            return;
        }

        if (this.BagMuun(player, slot) is not { } muun)
        {
            await this.SendStateAsync(player, true, "InvalidMuun", "Choose a Muun in your bag.", "Hãy chọn một Muun trong túi đồ.").ConfigureAwait(false);
            return;
        }

        var state = this.LoadState(player);
        state.ActiveItemId = muun.Item.GetId();
        this.SaveState(player, state);
        await this.RecomputeAsync(player).ConfigureAwait(false);
        await this.SendStateAsync(player, true, "Equipped", $"{muun.Spec.Name} is now your active Muun.", $"{muun.Spec.Name} đã thành Muun đồng hành.").ConfigureAwait(false);
    }

    /// <summary>Puts the active Muun away (it stays in the bag).</summary>
    /// <param name="player">The player.</param>
    /// <returns>A task.</returns>
    public async ValueTask UnequipAsync(Player player)
    {
        var state = this.LoadState(player);
        state.ActiveItemId = null;
        this.SaveState(player, state);
        await this.RecomputeAsync(player).ConfigureAwait(false);
        await this.SendStateAsync(player, true, "Unequipped", "Your Muun rests in your bag.", "Muun đã về túi đồ.").ConfigureAwait(false);
    }

    /// <summary>
    /// Evolves the active Muun at its highest level with one Evolution Stone from the bag: both are taken and the evolved
    /// Muun takes its place, in one save.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>A task.</returns>
    public async ValueTask EvolveAsync(Player player)
    {
        if (!await this.EnsureOnAsync(player).ConfigureAwait(false))
        {
            return;
        }

        var state = this.LoadState(player);
        if (this.ActiveMuun(player, state) is not { } muun || IsEvolved(muun.Spec))
        {
            await this.SendStateAsync(player, true, "CannotEvolve", "Only an active Muun which can evolve, can evolve.", "Chỉ Muun đồng hành chưa tiến hóa mới tiến hóa được.").ConfigureAwait(false);
            return;
        }

        var maximum = this.MaximumLevel(muun.Spec);
        if (muun.Item.Level < maximum)
        {
            await this.SendStateAsync(player, true, "CannotEvolve", $"The Muun evolves at level {maximum}.", $"Muun tiến hóa ở cấp {maximum}.").ConfigureAwait(false);
            return;
        }

        var evolvedDefinition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == muun.Spec.EvolutionItemGroup && d.Number == muun.Spec.EvolutionItemNumber);
        if (evolvedDefinition is null || FindBagItem(player, EvolutionStoneNumber) is not { } stone)
        {
            await this.SendStateAsync(player, true, "NoStone", "An Evolution Stone is needed in your bag.", "Cần một Đá Tiến Hóa trong túi đồ.").ConfigureAwait(false);
            return;
        }

        var stoneSlot = stone.ItemSlot;
        var muunSlot = muun.Item.ItemSlot;
        var level = muun.Item.Level;
        await TakeOneAsync(player, stone).ConfigureAwait(false);
        await RemoveAsync(player, muun.Item).ConfigureAwait(false);
        var evolved = await AddAsync(player, evolvedDefinition, level, muunSlot).ConfigureAwait(false);
        if (evolved is null || !await player.SaveProgressAsync().ConfigureAwait(false))
        {
            // Nothing is changed when it can't be saved: the stone and the Muun come back.
            if (evolved is not null)
            {
                await RemoveAsync(player, evolved).ConfigureAwait(false);
            }

            var back = await AddAsync(player, muun.Item.Definition!, level, muunSlot).ConfigureAwait(false);
            await AddAsync(player, stone.Definition!, 0, stoneSlot, merge: true).ConfigureAwait(false);
            state.ActiveItemId = back?.GetId();
            this.SaveState(player, state);
            await this.RecomputeAsync(player).ConfigureAwait(false);
            await this.SendStateAsync(player, true, "SaveFailed", "The evolution couldn't be saved; nothing changed. Please try again.", "Không lưu được lần tiến hóa, chưa có gì thay đổi. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        state.ActiveItemId = evolved.GetId();
        this.SaveState(player, state);
        await this.RecomputeAsync(player).ConfigureAwait(false);
        await this.SendStateAsync(player, true, "Evolved", $"{muun.Spec.Name} evolved!", $"{muun.Spec.Name} đã tiến hóa!").ConfigureAwait(false);
    }

    /// <summary>
    /// Converts a Muun of the bag which isn't the active one into energy, with one Muun Energy Converter. The items are
    /// saved as taken before the energy is written: a stop in between loses the energy, it never doubles it.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="slot">The inventory slot of the Muun.</param>
    /// <returns>A task.</returns>
    public ValueTask ConvertAsync(Player player, byte slot) => this.ConvertAsync(player, slot, operationId: null);

    /// <summary>
    /// Converts a Muun of the bag which isn't the active one into energy, with one Muun Energy Converter and an
    /// optional deterministic <paramref name="operationId"/>, committing item removal, energy state, and receipt in one
    /// database transaction.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="slot">The inventory slot of the Muun.</param>
    /// <param name="operationId">Optional idempotent asset operation id.</param>
    /// <returns>A task.</returns>
    internal async ValueTask ConvertAsync(Player player, byte slot, Guid? operationId)
    {
        if (!await this.EnsureOnAsync(player).ConfigureAwait(false))
        {
            return;
        }

        var state = this.LoadState(player);
        if (this.BagMuun(player, slot) is not { } muun || muun.Item.GetId() == state.ActiveItemId)
        {
            await this.SendStateAsync(player, true, "InvalidMuun", "Choose a Muun in your bag which isn't your active one.", "Hãy chọn một Muun trong túi đồ không phải Muun đồng hành.").ConfigureAwait(false);
            return;
        }

        if (FindBagItem(player, EnergyConverterNumber) is not { } converter)
        {
            await this.SendStateAsync(player, true, "NoConverter", "A Muun Energy Converter is needed in your bag.", "Cần một Máy Chuyển Năng Lượng Muun trong túi đồ.").ConfigureAwait(false);
            return;
        }

        var points = SeasonNineSystemsEngine.CalculateMuunEnergyConversionPoints(muun.Spec.MaxRank, muun.Item.Level, this.Configuration.MuunEvoPointMultiplier);
        var (muunDefinition, muunLevel) = (muun.Item.Definition!, muun.Item.Level);
        var converterSlot = converter.ItemSlot;
        var nextState = new MuunState { ActiveItemId = state.ActiveItemId, Energy = state.Energy + points };
        await RemoveAsync(player, muun.Item).ConfigureAwait(false);
        await TakeOneAsync(player, converter).ConfigureAwait(false);
        var committed = await this.CommitStateAndSaveAsync(
            player,
            nextState,
            operationId,
            "muun-convert",
            $"{player.SelectedCharacter!.GetId():N}|{muun.Item.GetId():N}|{points}").ConfigureAwait(false);
        if (!committed)
        {
            await AddAsync(player, muunDefinition, muunLevel, slot).ConfigureAwait(false);
            await AddAsync(player, converter.Definition!, 0, converterSlot, merge: true).ConfigureAwait(false);
            await this.SendStateAsync(player, true, "SaveFailed", "The conversion couldn't be saved; nothing changed. Please try again.", "Không lưu được lần chuyển đổi, chưa có gì thay đổi. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        await this.SendStateAsync(player, true, "Converted", $"{muun.Spec.Name} became {points} energy.", $"{muun.Spec.Name} đã thành {points} năng lượng.").ConfigureAwait(false);
    }

    /// <summary>
    /// Levels up the active Muun with energy.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>A task.</returns>
    public ValueTask LevelUpAsync(Player player) => this.LevelUpAsync(player, operationId: null);

    /// <summary>
    /// Levels up the active Muun with energy and an optional deterministic <paramref name="operationId"/>, committing
    /// energy deduction, Muun item level, and receipt in one database transaction.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">Optional idempotent asset operation id.</param>
    /// <returns>A task.</returns>
    internal async ValueTask LevelUpAsync(Player player, Guid? operationId)
    {
        if (!await this.EnsureOnAsync(player).ConfigureAwait(false))
        {
            return;
        }

        var state = this.LoadState(player);
        if (this.ActiveMuun(player, state) is not { } muun || IsEvolved(muun.Spec) || muun.Item.Level >= this.MaximumLevel(muun.Spec))
        {
            await this.SendStateAsync(player, true, "CannotLevel", "Only an active Muun below its highest level can level up.", "Chỉ Muun đồng hành chưa đạt cấp cao nhất mới lên cấp được.").ConfigureAwait(false);
            return;
        }

        var cost = this.LevelUpCost(muun.Item.Level);
        if (state.Energy < cost)
        {
            await this.SendStateAsync(player, true, "NotEnoughEnergy", $"Level up needs {cost} energy.", $"Lên cấp cần {cost} năng lượng.").ConfigureAwait(false);
            return;
        }

        var nextState = new MuunState { ActiveItemId = state.ActiveItemId, Energy = state.Energy - cost };
        muun.Item.Level++;
        var committed = await this.CommitStateAndSaveAsync(
            player,
            nextState,
            operationId,
            "muun-level-up",
            $"{player.SelectedCharacter!.GetId():N}|{muun.Item.GetId():N}|{muun.Item.Level}|{cost}").ConfigureAwait(false);
        if (!committed)
        {
            muun.Item.Level--;
            await this.SendStateAsync(player, true, "SaveFailed", "The level up couldn't be saved; nothing changed. Please try again.", "Không lưu được lần lên cấp, chưa có gì thay đổi. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        await player.InvokeViewPlugInAsync<IItemUpgradedPlugIn>(p => p.ItemUpgradedAsync(muun.Item)).ConfigureAwait(false);
        await this.RecomputeAsync(player).ConfigureAwait(false);
        await this.SendStateAsync(player, true, "LeveledUp", $"{muun.Spec.Name} reached level {muun.Item.Level}.", $"{muun.Spec.Name} đạt cấp {muun.Item.Level}.").ConfigureAwait(false);
    }

    /// <summary>
    /// Levels up the active Muun by feeding it a duplicate Muun from <paramref name="slot"/> in the bag.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="slot">The bag slot of the duplicate Muun.</param>
    /// <returns>A task.</returns>
    public async ValueTask FeedDuplicateAsync(Player player, byte slot)
    {
        if (!await this.EnsureOnAsync(player).ConfigureAwait(false))
        {
            return;
        }

        var state = this.LoadState(player);
        if (this.ActiveMuun(player, state) is not { } active || IsEvolved(active.Spec) || active.Item.Level >= this.MaximumLevel(active.Spec))
        {
            await this.SendStateAsync(player, true, "CannotLevel", "Only an active Muun below its highest level can level up.", "Chỉ Muun đồng hành chưa đạt cấp cao nhất mới lên cấp được.").ConfigureAwait(false);
            return;
        }

        if (this.BagMuun(player, slot) is not { } feeder
            || feeder.Item.GetId() == state.ActiveItemId
            || feeder.Spec.ItemGroup != active.Spec.ItemGroup
            || feeder.Spec.ItemNumber != active.Spec.ItemNumber)
        {
            await this.SendStateAsync(player, true, "InvalidMuun", "Choose another duplicate Muun of the same kind in your bag.", "Hãy chọn một Muun cùng loại khác trong túi đồ.").ConfigureAwait(false);
            return;
        }

        var (feederDef, feederLevel) = (feeder.Item.Definition!, feeder.Item.Level);
        await RemoveAsync(player, feeder.Item).ConfigureAwait(false);
        active.Item.Level++;
        if (!await player.SaveProgressAsync().ConfigureAwait(false))
        {
            active.Item.Level--;
            await AddAsync(player, feederDef, feederLevel, slot).ConfigureAwait(false);
            await this.SendStateAsync(player, true, "SaveFailed", "The level up couldn't be saved; nothing changed. Please try again.", "Không lưu được lần lên cấp, chưa có gì thay đổi. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        await player.InvokeViewPlugInAsync<IItemUpgradedPlugIn>(p => p.ItemUpgradedAsync(active.Item)).ConfigureAwait(false);
        await this.RecomputeAsync(player).ConfigureAwait(false);
        await this.SendStateAsync(player, true, "LeveledUp", $"{active.Spec.Name} reached level {active.Item.Level}.", $"{active.Spec.Name} đạt cấp {active.Item.Level}.").ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the active Muun's bonus as it is now: the previous element goes, the new one comes. Nothing applies when
    /// the system is off, no Muun is active, the Muun left the bag or its condition doesn't hold.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> when the bonus changed.</returns>
    public ValueTask<bool> RecomputeAsync(Player player)
    {
        if (player.Attributes is not { } attributes)
        {
            return ValueTask.FromResult(false);
        }

        var applied = this._applied.GetOrCreateValue(player);
        var (stat, value) = (default(AttributeDefinition), 0f);
        if (this.IsOn && this.ActiveMuun(player, this.LoadState(player)) is { } muun && this.FindOption(muun.Spec.OptionIndex) is { } option
            && StatOf(option.OptionType) is { } target)
        {
            var bonus = this.BonusOf(player, muun);
            if (bonus > 0)
            {
                (stat, value) = (target.Stat, bonus * target.Factor);
            }
        }

        if (applied.Stat == stat && Math.Abs(applied.Value - value) < 0.0001f)
        {
            return ValueTask.FromResult(false);
        }

        if (applied.Stat is { } previous && applied.Element is { } element)
        {
            attributes.RemoveElement(element, previous);
        }

        applied.Stat = null;
        applied.Element = null;
        applied.Value = 0;
        if (stat is not null && value != 0)
        {
            var next = new ConstantElement(value);
            attributes.AddElement(next, stat);
            (applied.Stat, applied.Element, applied.Value) = (stat, next, value);
        }

        return ValueTask.FromResult(true);
    }

    /// <summary>Recomputes the bonus and sends the state when it changed (the periodic check of map, level and time).</summary>
    /// <param name="player">The player.</param>
    /// <returns>A task.</returns>
    public async ValueTask RefreshAsync(Player player)
    {
        if (await this.RecomputeAsync(player).ConfigureAwait(false))
        {
            await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
        }
    }

    /// <summary>Sends the Muun state to the player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the player asked for it.</param>
    /// <param name="result">An event code, or <c>null</c>.</param>
    /// <param name="textEn">The event's English text.</param>
    /// <param name="textVi">The event's Vietnamese text.</param>
    /// <returns>A task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, string? result = null, string? textEn = null, string? textVi = null)
    {
        var json = this.WriteState(player, result, textEn, textVi);
        await player.InvokeViewPlugInAsync<IHnmoMuunViewPlugIn>(p => p.ShowMuunAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Writes the Muun state of the player as plain-ASCII JSON.</summary>
    /// <param name="player">The player.</param>
    /// <param name="result">An event code, or <c>null</c>.</param>
    /// <param name="textEn">The event's English text.</param>
    /// <param name="textVi">The event's Vietnamese text.</param>
    /// <returns>The JSON.</returns>
    internal string WriteState(Player player, string? result = null, string? textEn = null, string? textVi = null)
    {
        var state = this.LoadState(player);
        var active = this.IsOn ? this.ActiveMuun(player, state) : null;
        object? activeJson = null;
        if (active is { } muun && this.FindOption(muun.Spec.OptionIndex) is { } option)
        {
            var conditionMet = !this.Configuration.MuunStrictConditions
                || SeasonNineSystemsEngine.IsMuunConditionMet(option, MapNumber(player), TotalLevel(player), this.LocalNow());
            var bonus = this.BonusOf(player, muun);
            var shown = conditionMet ? bonus : SeasonNineSystemsEngine.EvaluateMuunBonus(muun.Spec, option, muun.Item.Level, IsEvolved(muun.Spec), MapNumber(player), TotalLevel(player), this.LocalNow(), strictConditions: false, enforceDatePeriod: this.Configuration.MuunEnforceDatePeriod);
            var maximum = this.MaximumLevel(muun.Spec);
            var (conditionEn, conditionVi) = DescribeCondition(option);
            activeJson = new
            {
                slot = muun.Item.ItemSlot,
                group = (int)muun.Spec.ItemGroup,
                number = (int)muun.Spec.ItemNumber,
                name = muun.Spec.Name,
                level = (int)muun.Item.Level,
                maxLevel = maximum,
                evolved = IsEvolved(muun.Spec),
                bonusEn = string.IsNullOrEmpty(option.Name)
                    ? string.Empty
                    : option.Name.Replace("%d", shown.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
                bonusVi = string.IsNullOrEmpty(option.Name) ? string.Empty : DescribeOptionVi(option.OptionType, shown),
                bonus = shown,
                applies = conditionMet && bonus > 0 && StatOf(option.OptionType) is not null,
                conditionEn,
                conditionVi,
                canEvolve = !IsEvolved(muun.Spec) && muun.Item.Level >= maximum,
                levelUpCost = !IsEvolved(muun.Spec) && muun.Item.Level < maximum ? this.LevelUpCost(muun.Item.Level) : 0,
            };
        }

        var bag = this.IsOn
            ? this.BagMuuns(player).Select(m => new
            {
                slot = m.Item.ItemSlot,
                group = (int)m.Spec.ItemGroup,
                number = (int)m.Spec.ItemNumber,
                name = m.Spec.Name,
                level = (int)m.Item.Level,
                evolved = IsEvolved(m.Spec),
                active = m.Item.GetId() == state.ActiveItemId,
                energy = SeasonNineSystemsEngine.CalculateMuunEnergyConversionPoints(m.Spec.MaxRank, m.Item.Level, this.Configuration.MuunEvoPointMultiplier),
            }).ToList()
            : [];
        var payload = new
        {
            v = 1,
            enabled = this.IsOn,
            active = activeJson,
            bag,
            energy = state.Energy,
            stones = this.IsOn ? CountBag(player, EvolutionStoneNumber) : 0,
            converters = this.IsOn ? CountBag(player, EnergyConverterNumber) : 0,
            strictConditions = this.Configuration.MuunStrictConditions,
            @event = result is null ? null : new { result, textEn, textVi },
        };
        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    /// <summary>Loads the player's Muun state.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The state.</returns>
    internal MuunState LoadState(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return new MuunState();
        }

        lock (this._gate)
        {
            var scope = character.GetId().ToString("N");
            var row = this.Backend().LoadAll(FeatureKey).FirstOrDefault(r => r.ScopeId == scope);
            return row is null ? new MuunState() : JsonSerializer.Deserialize<MuunState>(row.PayloadJson, JsonOptions) ?? new MuunState();
        }
    }

    private static bool IsEvolved(SeasonNineSystemsCatalog.MuunSpec spec) => spec.EvolutionItemGroup < 0;

    private static int MapNumber(Player player) => player.CurrentMap?.MapId ?? -1;

    private static int TotalLevel(Player player) => player.Attributes is { } a ? (int)(a[Stats.Level] + a[Stats.MasterLevel]) : 0;

    private static bool IsBagSlot(byte slot) => slot >= InventoryConstants.EquippableSlotsCount && slot < InventoryConstants.FirstStoreItemSlotIndex;

    private static Item? FindBagItem(Player player, short number) =>
        player.Inventory?.Items.Where(i => IsBagSlot(i.ItemSlot) && i.Definition is { Group: MuunGroup } d && d.Number == number)
            .OrderBy(i => i.ItemSlot).FirstOrDefault();

    private static int CountBag(Player player, short number) =>
        player.Inventory?.Items.Where(i => IsBagSlot(i.ItemSlot) && i.Definition is { Group: MuunGroup } d && d.Number == number)
            .Sum(i => Math.Max(1, (int)i.Durability)) ?? 0;

    // Takes one piece of a stack, or the item.
    private static async ValueTask TakeOneAsync(Player player, Item item)
    {
        if (item.Durability > 1)
        {
            item.Durability--;
            await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, false)).ConfigureAwait(false);
            return;
        }

        await RemoveAsync(player, item).ConfigureAwait(false);
    }

    private static async ValueTask RemoveAsync(Player player, Item item)
    {
        var slot = item.ItemSlot;
        if (player.Inventory is { } inventory && inventory.GetItem(slot) == item)
        {
            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
        }
    }

    // Puts an item into the bag slot (or adds a piece to the stack there, with merge), or into any free slot.
    private static async ValueTask<Item?> AddAsync(Player player, ItemDefinition definition, byte level, byte slot, bool merge = false)
    {
        if (player.Inventory is not { } inventory)
        {
            return null;
        }

        if (merge && inventory.GetItem(slot) is { } stack && stack.Definition == definition)
        {
            stack.Durability++;
            await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(stack, false)).ConfigureAwait(false);
            return stack;
        }

        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = level;
        item.Durability = 1;
        var target = inventory.GetItem(slot) is null ? slot : inventory.CheckInvSpace(item);
        if (target is not { } freeSlot || !await inventory.AddItemAsync(freeSlot, item).ConfigureAwait(false))
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            return null;
        }

        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        return item;
    }

    private static (string En, string Vi) DescribeCondition(SeasonNineSystemsCatalog.MuunOptionSpec option) => option.ConditionType switch
    {
        1 when option.ConditionValue1 >= 0 => ($"On map {option.ConditionValue1}", $"Ở bản đồ {option.ConditionValue1}"),
        2 => ($"Level (with master level) {option.ConditionValue1} to {option.ConditionValue2}", $"Cấp (cả cấp master) từ {option.ConditionValue1} đến {option.ConditionValue2}"),
        3 => ($"From {option.ConditionValue1}:00 to {option.ConditionValue2}:59 (Sydney time)", $"Từ {option.ConditionValue1}:00 đến {option.ConditionValue2}:59 (giờ Sydney)"),
        4 when option.ConditionValue1 >= 0 => ($"On {Days(option.ConditionValue1, vi: false)}", $"Vào {Days(option.ConditionValue1, vi: true)}"),
        _ => ("Always", "Luôn luôn"),
    };

    private static string Days(int mask, bool vi)
    {
        (int Bit, string En, string Vi)[] days =
        [
            (32, "Monday", "Thứ Hai"), (16, "Tuesday", "Thứ Ba"), (8, "Wednesday", "Thứ Tư"), (4, "Thursday", "Thứ Năm"),
            (2, "Friday", "Thứ Sáu"), (1, "Saturday", "Thứ Bảy"), (64, "Sunday", "Chủ Nhật"),
        ];
        return string.Join(", ", days.Where(d => (mask & d.Bit) != 0).Select(d => vi ? d.Vi : d.En));
    }

    private static string DescribeOptionVi(int optionType, int value) => optionType switch
    {
        1 => $"Tăng sát thương/phép/nguyền +{value}",
        8 => $"Tăng sát thương/phép/nguyền +{value} (không PvP)",
        7 => $"Tăng sát thương tối đa/phép/nguyền +{value}",
        14 => $"Tăng sát thương tối đa/phép/nguyền +{value} (không PvP)",
        2 => $"Tăng phòng thủ +{value}",
        3 => $"Tăng sát thương hoàn hảo +{value}",
        10 => $"Tăng sát thương hoàn hảo +{value} (không PvP)",
        4 => $"Tăng sát thương chí mạng +{value}",
        11 => $"Tăng tỉ lệ chí mạng +{value}% (không PvP)",
        5 => $"Tăng sát thương kỹ năng +{value}",
        12 => $"Tăng sát thương kỹ năng +{value} (không PvP)",
        53 => $"Kỹ năng tấn công (cấp {value}) (không PvP)",
        _ => $"Kỹ năng tấn công (cấp {value})",
    };

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }

    private (Item Item, SeasonNineSystemsCatalog.MuunSpec Spec)? BagMuun(Player player, byte slot)
    {
        if (!IsBagSlot(slot) || player.Inventory?.GetItem(slot) is not { Definition: { } definition } item
            || this.FindPet(definition.Group, definition.Number) is not { } spec)
        {
            return null;
        }

        return (item, spec);
    }

    private IEnumerable<(Item Item, SeasonNineSystemsCatalog.MuunSpec Spec)> BagMuuns(Player player)
    {
        foreach (var item in player.Inventory?.Items.OrderBy(i => i.ItemSlot) ?? Enumerable.Empty<Item>())
        {
            if (this.BagMuun(player, item.ItemSlot) is { } muun && muun.Item == item)
            {
                yield return muun;
            }
        }
    }

    private (Item Item, SeasonNineSystemsCatalog.MuunSpec Spec)? ActiveMuun(Player player, MuunState state)
    {
        if (state.ActiveItemId is not { } id)
        {
            return null;
        }

        foreach (var muun in this.BagMuuns(player))
        {
            if (muun.Item.GetId() == id)
            {
                return muun;
            }
        }

        return null;
    }

    // The admin's table: entry (level - 1) is the cost from that level to the next; a level without one uses the per-level
    // cost times the level.
    private int LevelUpCost(int level)
    {
        if (this.Configuration.MuunLevelUpEnergyCosts is { } table && level >= 1 && level <= table.Count && table[level - 1] > 0)
        {
            return table[level - 1];
        }

        return (this.Configuration.MuunLevelUpEnergyCost > 0 ? this.Configuration.MuunLevelUpEnergyCost : DefaultLevelUpCost) * Math.Max(1, level);
    }

    private DateTime LocalNow() => SeasonNineSystemsPlugIn.ToRealmTime(this.UtcNow());

    private int BonusOf(Player player, (Item Item, SeasonNineSystemsCatalog.MuunSpec Spec) muun) =>
        this.FindOption(muun.Spec.OptionIndex) is { } option
            ? SeasonNineSystemsEngine.EvaluateMuunBonus(muun.Spec, option, muun.Item.Level, IsEvolved(muun.Spec), MapNumber(player), TotalLevel(player), this.LocalNow(), this.Configuration.MuunStrictConditions, this.Configuration.MuunEnforceDatePeriod)
            : 0;

    // The admin's tables (configuration), or the built-in ones when the admin hasn't set any.
    private IReadOnlyList<SeasonNineSystemsCatalog.MuunSpec> Pets() =>
        this.Configuration.MuunPets is { Count: > 0 } pets ? pets : SeasonNineSystemsCatalog.Muuns;

    private SeasonNineSystemsCatalog.MuunSpec? FindPet(byte group, short number)
    {
        foreach (var pet in this.Pets())
        {
            if (pet.ItemGroup == group && pet.ItemNumber == number)
            {
                return pet;
            }
        }

        return null;
    }

    private SeasonNineSystemsCatalog.MuunOptionSpec? FindOption(int index)
    {
        var options = this.Configuration.MuunOptions is { Count: > 0 } configured ? configured : SeasonNineSystemsCatalog.MuunOptions;
        foreach (var option in options)
        {
            if (option.Index == index)
            {
                return option;
            }
        }

        return null;
    }

    private int MaximumLevel(SeasonNineSystemsCatalog.MuunSpec pet) =>
        this.FindOption(pet.OptionIndex) is { } option ? SeasonNineSystemsEngine.MuunMaximumLevel(option) : 1;

    private async ValueTask<bool> EnsureOnAsync(Player player)
    {
        if (this.IsOn)
        {
            return true;
        }

        await player.InvokeViewPlugInAsync<IHnmoMuunViewPlugIn>(p => p.ShowMuunAsync(DisabledState, requested: true)).ConfigureAwait(false);
        return false;
    }

    private void SaveState(Player player, MuunState state)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        lock (this._gate)
        {
            var backend = this.Backend();
            backend.EnsureSchema();
            backend.UpsertBatch([new HnmoFeatureRow(FeatureKey, character.GetId().ToString("N"), JsonSerializer.Serialize(state, JsonOptions), DateTime.UtcNow)], []);
        }
    }

    private async ValueTask<bool> CommitStateAndSaveAsync(
        Player player,
        MuunState nextState,
        Guid? operationId,
        string kind,
        string payload)
    {
        if (player.Account is not { } account || player.SelectedCharacter is not { } character)
        {
            if (!await player.SaveProgressAsync().ConfigureAwait(false))
            {
                return false;
            }

            this.SaveState(player, nextState);
            return true;
        }

        var accountId = account.GetId();
        var stateJson = JsonSerializer.Serialize(nextState, JsonOptions);
        var stateWrite = new FeatureStateWrite(
            FeatureKey,
            character.GetId().ToString("N"),
            stateJson,
            this.UtcNow(),
            this.ResolveStateFileKey());
        var request = new AssetOperationRequest(
            operationId ?? Guid.NewGuid(),
            accountId,
            kind,
            AssetOperationCommitter.PayloadHash(payload),
            "game",
            null,
            [],
            FeatureState: stateWrite);
        var status = await AssetOperationCommitter.CommitAsync(player, request).ConfigureAwait(false);
        return status is AssetCommitStatus.Committed or AssetCommitStatus.AlreadyCommitted;
    }

    private string ResolveStateFileKey()
    {
        var key = string.IsNullOrWhiteSpace(this.StateFile) ? HnmoStateStore.StateFileName : this.StateFile.Trim();
        return HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(HnmoStateStore.StateFileName);
    }

    private IHnmoPlayerStateRowBackend Backend() => HnmoPlayerStateRowStoreRegistry.GetBackend(this.ResolveStateFileKey());

    /// <summary>The bonus element currently applied to a player.</summary>
    private sealed class Applied
    {
        public AttributeDefinition? Stat { get; set; }

        public ConstantElement? Element { get; set; }

        public float Value { get; set; }
    }
}
