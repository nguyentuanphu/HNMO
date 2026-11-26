// <copyright file="MuunCompanionTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.Persistence;
using HNMO.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The Season 9 Muun companions (<see cref="MuunCompanion"/>): the active Muun's bonus applies while its condition holds,
/// never twice, is kept across a restart, and nothing applies outside a Season 9 realm.
/// </summary>
/// <remarks>
/// Luki (13/212): Defense +22 at level 1 from 18:00 to 24:00, doubled by its add option (type 1, value 10): +44.
/// Its evolved form (13/213): +38, doubled: +76. Luki evolves at level 4 (its option has no level 5 value).
/// </remarks>
[TestFixture]
public class MuunCompanionTests
{
    private static readonly byte FirstBagSlot = InventoryConstants.EquippableSlotsCount;

    // 20:00 and 10:00 in Sydney (AEDT, UTC+11) on 27 Nov 2026.
    private static readonly DateTime Evening = new(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Morning = new(2026, 11, 26, 23, 0, 0, DateTimeKind.Utc);

    private string _stateFile = string.Empty;

    /// <summary>Gives each test its own state file.</summary>
    [SetUp]
    public void SetUp() => this._stateFile = Path.Combine(Path.GetTempPath(), $"hnmo-muun-{Guid.NewGuid():N}.json");

    /// <summary>Deletes the state file.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._stateFile))
        {
            File.Delete(this._stateFile);
        }
    }

    /// <summary>The active Muun's bonus applies once, however often it is equipped, and goes when it is put away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheBonusAppliesOnceAndGoesWhenPutAwayAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var luki = await AddAsync(player, 212, FirstBagSlot, level: 1).ConfigureAwait(false);
        var before = player.Attributes![Stats.DefenseBase];

        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);
        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);
        await plugIn.Muuns.RecomputeAsync(player).ConfigureAwait(false);

        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before + 44));

        await plugIn.Muuns.UnequipAsync(player).ConfigureAwait(false);
        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before));
    }

    /// <summary>Outside its hours the bonus goes, and it comes back when the hour does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheBonusFollowsTheHourAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var luki = await AddAsync(player, 212, FirstBagSlot, level: 1).ConfigureAwait(false);
        var before = player.Attributes![Stats.DefenseBase];
        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);

        plugIn.Muuns.UtcNow = () => Morning;
        await plugIn.Muuns.RefreshAsync(player).ConfigureAwait(false);
        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before), "10:00 is outside 18:00-24:00");
        using (var state = JsonDocument.Parse(plugIn.Muuns.WriteState(player)))
        {
            Assert.That(state.RootElement.GetProperty("active").GetProperty("applies").GetBoolean(), Is.False);
        }

        plugIn.Muuns.UtcNow = () => Evening;
        await plugIn.Muuns.RefreshAsync(player).ConfigureAwait(false);
        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before + 44));
    }

    /// <summary>A Muun which leaves the bag stops giving its bonus.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AMuunWhichLeavesTheBagStopsAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var luki = await AddAsync(player, 212, FirstBagSlot, level: 1).ConfigureAwait(false);
        var before = player.Attributes![Stats.DefenseBase];
        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);

        await player.Inventory!.RemoveItemAsync(luki).ConfigureAwait(false);
        await plugIn.Muuns.RefreshAsync(player).ConfigureAwait(false);

        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before));
    }

    /// <summary>The active Muun is kept in the state: after a restart it applies again, once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheActiveMuunIsKeptAcrossARestartAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var luki = await AddAsync(player, 212, FirstBagSlot, level: 1).ConfigureAwait(false);
        var before = player.Attributes![Stats.DefenseBase];
        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);

        // The old process ends (its bonus goes with it); a new plug-in reads the same state.
        plugIn.Configuration!.Enabled = false;
        await plugIn.Muuns.RecomputeAsync(player).ConfigureAwait(false);
        var restarted = new SeasonNineSystemsPlugIn { Configuration = new SeasonNineSystemsConfiguration { Enabled = true }, IsSeasonNineRealm = () => true };
        restarted.Muuns.StateFile = this._stateFile;
        restarted.Muuns.UtcNow = () => Evening;
        await restarted.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(restarted.Muuns.LoadState(player).ActiveItemId, Is.EqualTo(luki.GetId()));
        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before + 44));
    }

    /// <summary>Outside a Season 9 realm, nothing can be equipped and no bonus applies.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingAppliesOutsideASeasonNineRealmAsync()
    {
        var (plugIn, player) = await this.CreateAsync(seasonNine: false).ConfigureAwait(false);
        var luki = await AddAsync(player, 212, FirstBagSlot, level: 1).ConfigureAwait(false);
        var before = player.Attributes![Stats.DefenseBase];

        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);
        await plugIn.Muuns.RecomputeAsync(player).ConfigureAwait(false);

        Assert.That(plugIn.Muuns.LoadState(player).ActiveItemId, Is.Null);
        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before));
        using var state = JsonDocument.Parse(plugIn.Muuns.WriteState(player));
        Assert.That(state.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
    }

    /// <summary>
    /// A Muun converts into energy with a converter; the energy levels the active Muun up; at its highest level it
    /// evolves with a stone, and the evolved bonus applies.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConvertLevelUpAndEvolveAsync()
    {
        var (plugIn, player) = await this.CreateAsync(levelUpCost: 2).ConfigureAwait(false);
        var luki = await AddAsync(player, 212, FirstBagSlot, level: 1).ConfigureAwait(false);
        var tony = await AddAsync(player, 214, (byte)(FirstBagSlot + 1), level: 1).ConfigureAwait(false);
        await AddAsync(player, MuunCompanion.EnergyConverterNumber, (byte)(FirstBagSlot + 2), level: 0).ConfigureAwait(false);
        await AddAsync(player, MuunCompanion.EvolutionStoneNumber, (byte)(FirstBagSlot + 3), level: 0).ConfigureAwait(false);
        var before = player.Attributes![Stats.DefenseBase];
        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);

        // Tony (rank 3) at level 1: (3 + 1 * 3) = 6 energy.
        await plugIn.Muuns.ConvertAsync(player, tony.ItemSlot).ConfigureAwait(false);
        Assert.That(plugIn.Muuns.LoadState(player).Energy, Is.EqualTo(6));
        Assert.That(player.Inventory!.GetItem(tony.ItemSlot), Is.Null);

        // Levels 1 -> 2 -> 3 -> 4 cost 2, 4 and 6 at 2 per level: 6 energy reaches level 3, then it runs out.
        await plugIn.Muuns.LevelUpAsync(player).ConfigureAwait(false);
        await plugIn.Muuns.LevelUpAsync(player).ConfigureAwait(false);
        await plugIn.Muuns.LevelUpAsync(player).ConfigureAwait(false);
        Assert.That(luki.Level, Is.EqualTo(3));
        Assert.That(plugIn.Muuns.LoadState(player).Energy, Is.EqualTo(0));
        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before + 60), "level 3: 30, doubled");

        await plugIn.Muuns.EvolveAsync(player).ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(luki.ItemSlot), Is.SameAs(luki), "not yet: Luki evolves at level 4");

        luki.Level = 4;
        await plugIn.Muuns.EvolveAsync(player).ConfigureAwait(false);
        var evolved = player.Inventory.GetItem(luki.ItemSlot);
        Assert.That(evolved?.Definition?.Number, Is.EqualTo((short)213));
        Assert.That(player.Inventory.Items.Any(i => i.Definition?.Number == MuunCompanion.EvolutionStoneNumber), Is.False, "the stone is taken");
        Assert.That(plugIn.Muuns.LoadState(player).ActiveItemId, Is.EqualTo(evolved!.GetId()));
        Assert.That(player.Attributes[Stats.DefenseBase], Is.EqualTo(before + 76));
    }

    /// <summary>The active Muun can't be converted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheActiveMuunCantBeConvertedAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var luki = await AddAsync(player, 212, FirstBagSlot, level: 1).ConfigureAwait(false);
        await AddAsync(player, MuunCompanion.EnergyConverterNumber, (byte)(FirstBagSlot + 2), level: 0).ConfigureAwait(false);
        await plugIn.Muuns.EquipAsync(player, luki.ItemSlot).ConfigureAwait(false);

        await plugIn.Muuns.ConvertAsync(player, luki.ItemSlot).ConfigureAwait(false);

        Assert.That(player.Inventory!.GetItem(luki.ItemSlot), Is.SameAs(luki));
        Assert.That(plugIn.Muuns.LoadState(player).Energy, Is.Zero);
    }

    private static async Task<Item> AddAsync(Player player, short number, byte slot, byte level)
    {
        var definition = player.GameContext.Configuration.Items.First(d => d.Group == MuunCompanion.MuunGroup && d.Number == number);
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = level;
        item.Durability = 1;
        Assert.That(await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false), Is.True);
        return item;
    }

    private async Task<(SeasonNineSystemsPlugIn PlugIn, Player Player)> CreateAsync(bool seasonNine = true, int levelUpCost = 10)
    {
        var configuration = new PlugInConfiguration
        {
            TypeId = typeof(SeasonNineSystemsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new SeasonNineSystemsConfiguration { Enabled = true, MuunLevelUpEnergyCosts = [levelUpCost, levelUpCost * 2, levelUpCost * 3] }),
        };
        var gameContext = GameContextTestHelper.CreateGameContext([configuration]);
        foreach (var number in new short[] { MuunCompanion.EvolutionStoneNumber, 212, 213, 214, 215, MuunCompanion.EnergyConverterNumber })
        {
            gameContext.Configuration.Items.Add(new BasicModel.ItemDefinition { Group = MuunCompanion.MuunGroup, Number = number, Width = 1, Height = 1, Durability = 1 });
        }

        var plugIn = SeasonNineSystemsPlugIn.Get(gameContext)!;
        plugIn.IsSeasonNineRealm = () => seasonNine;
        plugIn.Muuns.StateFile = this._stateFile;
        plugIn.Muuns.UtcNow = () => Evening;
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        return (plugIn, player);
    }
}
