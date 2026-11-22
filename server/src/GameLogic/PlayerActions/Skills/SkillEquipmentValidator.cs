// <copyright file="SkillEquipmentValidator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;

/// <summary>
/// Checks whether a player holds the weapon, gloves, bow/crossbow, or mount required to
/// cast an attack skill, matching the desktop and web clients.
/// </summary>
internal static class SkillEquipmentValidator
{
    private const byte SwordOrGloveGroup = 0;
    private const byte SpearGroup = 3;
    private const byte BowGroup = 4;
    private const byte StaffOrBookGroup = 5;

    private static readonly IReadOnlyDictionary<short, short> StrengthenedToBaseSkill = new Dictionary<short, short>
    {
        [331] = 42, // RagefulBlowStreng -> RagefulBlow
        [333] = 42, // RagefulBlowMastery -> RagefulBlow
        [336] = 43, // DeathStabStrengthener -> DeathStab
        [339] = 43, // DeathStabProficiency -> DeathStab
        [342] = 43, // DeathStabMastery -> DeathStab
        [337] = 232, // StrikeofDestrStr -> StrikeofDestruction
        [340] = 232, // StrikeofDestrProf -> StrikeofDestruction
        [343] = 232, // StrikeofDestrMast -> StrikeofDestruction
        [411] = 235, // MultiShotStreng -> MultiShot
        [414] = 24, // TripleShotStrengthener -> TripleShot
        [416] = 52, // PenetrationStrengthener -> Penetration
        [418] = 24, // TripleShotMastery -> TripleShot
        [424] = 51, // IceArrowStrengthener -> IceArrow
        [431] = 235, // MultiShotMastery -> MultiShot
        [434] = 427, // PoisonArrowStr -> PoisonArrow
        [482] = 56, // PowerSlashStreng -> PowerSlash
        [490] = 55, // BloodAttackStrengthen -> FireSlash
        [492] = 236, // FlameStrikeStrengthen -> FlameStrike
        [493] = 55, // FireSlashMastery -> FireSlash
        [494] = 236, // FlameStrikeMastery -> FlameStrike
        [512] = 62, // EarthshakeStreng -> Earthshake
        [516] = 62, // EarthshakeMastery -> Earthshake
        [551] = 260, // KillingBlowStrengthener -> KillingBlow
        [552] = 261, // BeastUppercutStrengthener -> BeastUppercut
        [554] = 260, // KillingBlowMastery -> KillingBlow
        [555] = 261, // BeastUppercutMastery -> BeastUppercut
        [558] = 262, // ChainDriveStrengthener -> ChainDrive
        [560] = 264, // DragonRoarStrengthener -> DragonRoar
    };

    /// <summary>
    /// Determines whether the player meets the weapon, glove, bow, or mount rule for <paramref name="skill"/>.
    /// </summary>
    /// <param name="player">The attacking player.</param>
    /// <param name="skill">The skill being cast.</param>
    /// <returns><c>true</c> if equipped items satisfy the skill's equipment rule; otherwise, <c>false</c>.</returns>
    internal static bool HasRequiredEquipment(Player player, Skill skill)
    {
        var right = player.Inventory?.GetItem(InventoryConstants.RightHandSlot);
        var left = player.Inventory?.GetItem(InventoryConstants.LeftHandSlot);
        var pet = player.Inventory?.GetItem(InventoryConstants.PetSlot);

        var baseNumber = ResolveBaseSkillNumber(skill);

        if (skill.DamageType == DamageType.Fenrir || baseNumber == 76)
        {
            return IsActiveMount(pet, ItemConstants.Fenrir);
        }

        return baseNumber switch
        {
            47 => IsActiveRideableMount(pet) && right?.Definition?.Group == SpearGroup, // Impale
            49 => IsActiveRideableMount(pet), // Rider (Fire Breath)
            62 => IsActiveMount(pet, ItemConstants.DarkHorse), // Earthshake
            43 => HasNonStaffRightWeapon(right), // Death Stab
            57 => right?.Definition?.Group == SwordOrGloveGroup, // Spiral Slash
            73 => IsStaffOrBook(right), // Mana Rays: a staff or book in the right hand
            42 or 55 or 232 or 236 => HasMeleeWeapon(right, left), // Rageful Blow, Fire Slash, Destruction, Flame Strike
            56 => HasMeleeWeapon(right, left) && !IsRideableMount(pet), // Power Slash
            24 or 46 or 51 or 52 or 54 or 235 or 427 => HasBowOrCrossbow(right, left), // Elf bow/crossbow skills
            260 or 261 or 269 => !IsHornMount(pet), // Killing Blow, Beast Uppercut, Charge
            264 => HasSwordformGloves(right) || HasSwordformGloves(left), // Dragon Roar
            262 or 265 => !IsHornMount(pet) && (HasSwordformGloves(right) || HasSwordformGloves(left)), // Chain Drive, Dragon Slasher
            _ => true,
        };
    }

    private static short ResolveBaseSkillNumber(Skill skill)
    {
        var current = skill;
        for (var i = 0; i < 4 && current.MasterDefinition?.ReplacedSkill is { } replaced; i++)
        {
            current = replaced;
        }

        return StrengthenedToBaseSkill.TryGetValue(current.Number, out var baseNumber) ? baseNumber : current.Number;
    }

    private static bool IsStaffOrBook(Item? item) => item?.Definition?.Group == StaffOrBookGroup;

    private static bool HasNonStaffRightWeapon(Item? right)
        => right?.Definition is { IsAmmunition: false } && !IsStaffOrBook(right);

    private static bool HasMeleeWeapon(Item? right, Item? left)
        => HasNonStaffRightWeapon(right) && !IsStaffOrBook(left);

    private static bool HasBowOrCrossbow(Item? right, Item? left)
        => right?.Definition is { Group: BowGroup, IsAmmunition: false }
           || left?.Definition is { Group: BowGroup, IsAmmunition: false };

    private static bool HasSwordformGloves(Item? item)
        => item?.Definition is { Group: SwordOrGloveGroup } def
           && ((def.Number >= 32 && def.Number <= 35) || def.Number == 38);

    private static bool Matches(Item? pet, ItemIdentifier identifier)
        => pet?.Definition is { } def && def.Group == identifier.Group && def.Number == identifier.Number;

    private static bool IsHornMount(Item? pet)
        => Matches(pet, ItemConstants.Uniria)
           || Matches(pet, ItemConstants.Dinorant)
           || Matches(pet, ItemConstants.Fenrir);

    private static bool IsRideableMount(Item? pet)
        => IsHornMount(pet) || Matches(pet, ItemConstants.DarkHorse);

    private static bool IsActiveMount(Item? pet, ItemIdentifier identifier)
        => pet is { Durability: > 0 } && Matches(pet, identifier);

    private static bool IsActiveRideableMount(Item? pet)
        => pet is { Durability: > 0 } && IsRideableMount(pet);
}
