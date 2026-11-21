// <copyright file="SecondClassItemsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update makes the items of class rank 2 wearable from the second class on, as in the item lists of the
/// Season 6/9 server suites (owner's choice, 21 Nov 2026): rank 1 = first class and up, rank 2 = second class and
/// up, rank 3 = third class. HNMO treated rank 2 like rank 1, so a first class character could wear 102 items such as
/// the Dark Phoenix, Grand Soul, Divine and Demonic sets. The first class is removed from these items only.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("C59D0E72-8A14-4B3F-A6E9-2F7B1D8C4053")]
public class SecondClassItemsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Second class items of the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Items of class rank 2 (e.g. Dark Phoenix, Grand Soul, Divine sets) need the second class; the first class can't wear them any more.";

    /// <summary>Gets the group and number of each rank 2 item and the number of the first class which is removed.</summary>
    internal static IReadOnlyList<(byte Group, short Number, byte FirstClass)> Items { get; } =
    [
        (0, 17, 4), // Dark Breaker
        (0, 20, 4), // Knight Blade
        (0, 22, 4), // Bone Blade
        (0, 24, 4), // Daybreak
        (0, 26, 4), // Flameberge
        (0, 27, 4), // Sword Breaker
        (2, 7, 8), // Elemental Mace
        (2, 16, 8), // Frost Mace
        (3, 10, 4), // Dragon Spear
        (3, 11, 4), // Brova
        (4, 17, 8), // Celestial Bow
        (4, 19, 8), // Great Reign Crossbow
        (4, 20, 8), // Arrow Viper Bow
        (4, 21, 8), // Sylph Wind Bow
        (4, 22, 8), // Albatross Bow
        (4, 23, 8), // Dark Stinger
        (4, 24, 8), // Aileen Bowr
        (5, 9, 0), // Dragon Soul Staff
        (5, 11, 0), // Staff of Kundun
        (5, 12, 0), // Grand Viper Staff
        (5, 13, 0), // Platina Staff
        (5, 18, 20), // Demonic Stick
        (5, 19, 20), // Storm Blitz Stick
        (5, 20, 20), // Eternal Wing Stick
        (5, 31, 0), // Inberial Staff
        (5, 33, 0), // Chromatic Staff
        (5, 34, 20), // Raven Stick
        (6, 15, 0), // Grand Soul Shield
        (6, 16, 8), // Elemental Shield
        (6, 17, 4), // Crimson Glory
        (6, 19, 8), // Frost Barrier
        (6, 20, 0), // Guardian Shield
        (7, 17, 4), // Dark Phoenix Helm
        (7, 18, 0), // Grand Soul Helm
        (7, 19, 8), // Divine Helm
        (7, 21, 4), // Great Dragon Helm
        (7, 22, 0), // Dark Soul Helm
        (7, 24, 8), // Red Spirit Helm
        (7, 29, 4), // Dragon Knight Helm
        (7, 30, 0), // Venom Mist Helm
        (7, 31, 8), // Sylphid Ray Helm
        (7, 42, 20), // Demonic Helm
        (7, 43, 20), // Storm Blitz Helm
        (7, 44, 20), // Eternal Wing Helm
        (8, 17, 4), // Dark Phoenix Armor
        (8, 18, 0), // Grand Soul Armor
        (8, 19, 8), // Divine Armor
        (8, 21, 4), // Great Dragon Armor
        (8, 22, 0), // Dark Soul Armor
        (8, 24, 8), // Red Spirit Armor
        (8, 29, 4), // Dragon Knight Armor
        (8, 30, 0), // Venom Mist Armor
        (8, 31, 8), // Sylphid Ray Armor
        (8, 42, 20), // Demonic Armor
        (8, 43, 20), // Storm Blitz Armor
        (8, 44, 20), // Eternal Wing Armor
        (9, 17, 4), // Dark Phoenix Pants
        (9, 18, 0), // Grand Soul Pants
        (9, 19, 8), // Divine Pants
        (9, 21, 4), // Great Dragon Pants
        (9, 22, 0), // Dark Soul Pants
        (9, 24, 8), // Red Spirit Pants
        (9, 29, 4), // Dragon Knight Pants
        (9, 30, 0), // Venom Mist Pants
        (9, 31, 8), // Sylphid Ray Pants
        (9, 42, 20), // Demonic Pants
        (9, 43, 20), // Storm Blitz Pants
        (9, 44, 20), // Eternal Wing Pants
        (10, 17, 4), // Dark Phoenix Gloves
        (10, 18, 0), // Grand Soul Gloves
        (10, 19, 8), // Divine Gloves
        (10, 21, 4), // Great Dragon Gloves
        (10, 22, 0), // Dark Soul Gloves
        (10, 24, 8), // Red Spirit Gloves
        (10, 29, 4), // Dragon Knight Gloves
        (10, 30, 0), // Venom Mist Gloves
        (10, 31, 8), // Sylphid Ray Gloves
        (10, 42, 20), // Demonic Gloves
        (10, 43, 20), // Storm Blitz Gloves
        (10, 44, 20), // Eternal Wing Gloves
        (11, 17, 4), // Dark Phoenix Boots
        (11, 18, 0), // Grand Soul Boots
        (11, 19, 8), // Divine Boots
        (11, 21, 4), // Great Dragon Boots
        (11, 22, 0), // Dark Soul Boots
        (11, 24, 8), // Red Spirit Boots
        (11, 29, 4), // Dragon Knight Boots
        (11, 30, 0), // Venom Mist Boots
        (11, 31, 8), // Sylphid Ray Boots
        (11, 42, 20), // Demonic Boots
        (11, 43, 20), // Storm Blitz Boots
        (11, 44, 20), // Eternal Wing Boots
        (12, 3, 8), // Wings of Spirits
        (12, 4, 0), // Wings of Soul
        (12, 5, 4), // Wings of Dragon
        (12, 12, 4), // Orb of Rageful Blow
        (12, 18, 8), // Orb of Ice Arrow
        (12, 19, 4), // Orb of Death Stab
        (12, 42, 20), // Wind of Despair
        (12, 44, 4), // Crystal of Destruction
        (12, 45, 8), // Crystal of Multi-Shot
        (12, 46, 8), // Crystal of Recovery
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 17, 10, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (group, number, firstClass) in Items)
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is { } item
                && item.QualifiedCharacters.FirstOrDefault(c => c.Number == firstClass) is { } characterClass)
            {
                item.QualifiedCharacters.Remove(characterClass);
            }
        }

        return ValueTask.CompletedTask;
    }
}
