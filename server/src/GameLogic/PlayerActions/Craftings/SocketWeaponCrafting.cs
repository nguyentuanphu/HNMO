// <copyright file="SocketWeaponCrafting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Craftings;

using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.Views.NPC;

/// <summary>
/// Chaos Machine combination that combines a socket item carrying at least one mounted Seed Sphere,
/// a Seed Sphere, a Jewel of Chaos, a Jewel of Creation, and a Combination Note (14-191..203)
/// into the matching 5-socket weapon at a 40 % success rate for 1,000,000 zen.
/// </summary>
public class SocketWeaponCrafting : SimpleItemCraftingHandler
{
    /// <summary>
    /// Gets the Chaos Machine crafting number matching the client mix ID.
    /// </summary>
    public const byte MixNumber = 57;

    /// <summary>
    /// Gets the reference identifier for the input socket item.
    /// </summary>
    public const byte SocketItemReference = 0x91;

    /// <summary>
    /// Gets the reference identifier for the input Seed Sphere.
    /// </summary>
    public const byte SeedSphereReference = 0x92;

    /// <summary>
    /// Gets the reference identifier for the input Combination Note.
    /// </summary>
    public const byte CreationNoteReference = 0x93;

    /// <summary>
    /// Gets the mapping from Combination Note number (group 14, 191..203) to result weapon (group, number).
    /// </summary>
    public static readonly IReadOnlyDictionary<short, (byte Group, short Number)> NoteToWeaponMap = new Dictionary<short, (byte Group, short Number)>
    {
        [191] = (0, 29), // Sonic Combination Note -> Sonic Blade
        [192] = (0, 36), // Cyclone Combination Note -> Cyclone Sword
        [193] = (0, 37), // Blast Combination Note -> Blast Break
        [194] = (3, 12), // Magma Combination Note -> Magmus Peer
        [195] = (2, 20), // Horn Combination Note -> Horn of Steal
        [196] = (4, 25), // Angelic Combination Note -> Angelic Bow
        [197] = (4, 26), // Devil Cross Combination Note -> Devil Crossbow
        [198] = (5, 35), // Miracle Combination Note -> Miracle Staff
        [199] = (5, 37), // Spite Combination Note -> Spite Staff
        [200] = (0, 30), // Asura Combination Note -> Asura
        [201] = (2, 19), // Thunder Combination Note -> Thunderbolt
        [202] = (5, 32), // Soul Combination Note -> Summon Spirit Stick
        [203] = (0, 38), // Greater Combination Note -> Frick Large Glove
    };

    private readonly SimpleCraftingSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketWeaponCrafting"/> class.
    /// </summary>
    /// <param name="settings">The crafting settings.</param>
    public SocketWeaponCrafting(SimpleCraftingSettings settings)
        : base(settings)
    {
        this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public override CraftingResult? TryGetRequiredItems(Player player, out IList<CraftingRequiredItemLink> items, out byte successRate)
    {
        var result = base.TryGetRequiredItems(player, out items, out successRate);
        if (result != default)
        {
            return result;
        }

        var socketItem = items.SingleOrDefault(i => i.ItemRequirement.Reference == SocketItemReference)?.Items.SingleOrDefault();
        var noteItem = items.SingleOrDefault(i => i.ItemRequirement.Reference == CreationNoteReference)?.Items.SingleOrDefault();
        if (socketItem?.Definition is null || noteItem?.Definition is null)
        {
            return CraftingResult.IncorrectMixItems;
        }

        // Require at least one mounted Seed Sphere (bonus socket options alone do not qualify).
        if (!socketItem.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.SocketOption))
        {
            return CraftingResult.IncorrectMixItems;
        }

        if (!NoteToWeaponMap.TryGetValue(noteItem.Definition.Number, out var target)
            || !player.GameContext.Configuration.Items.Any(i => i.Group == target.Group && i.Number == target.Number))
        {
            return CraftingResult.IncorrectMixItems;
        }

        return null;
    }

    /// <inheritdoc />
    protected override async ValueTask<List<Item>> CreateOrModifyResultItemsAsync(IList<CraftingRequiredItemLink> requiredItems, Player player, byte socketSlot, byte successRate)
    {
        var noteItem = requiredItems.Single(i => i.ItemRequirement.Reference == CreationNoteReference).Items.Single();
        var target = NoteToWeaponMap[noteItem.Definition!.Number];
        var weaponDefinition = player.GameContext.Configuration.Items.First(i => i.Group == target.Group && i.Number == target.Number);

        var resultItem = player.PersistenceContext.CreateNew<Item>();
        resultItem.Definition = weaponDefinition;
        resultItem.Level = 0;
        resultItem.SocketCount = (byte)Math.Max(5, weaponDefinition.MaximumSockets);
        this.AddRandomLuckOption(resultItem, player, successRate);
        this.AddRandomItemOption(resultItem, player, successRate);
        this.AddRandomSkill(resultItem, successRate);
        this.AddRandomExcellentOptions(resultItem, player);
        resultItem.Durability = resultItem.GetMaximumDurabilityOfOnePiece();

        await player.TemporaryStorage!.AddItemAsync(resultItem).ConfigureAwait(false);
        return new List<Item> { resultItem };
    }
}
