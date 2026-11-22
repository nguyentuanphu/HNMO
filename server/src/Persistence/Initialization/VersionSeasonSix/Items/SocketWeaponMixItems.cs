// <copyright file="SocketWeaponMixItems.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Items;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerActions.Craftings;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Items;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Ensures the shared Creation Notes (14-191..203), their matching level-400 5-socket weapons,
/// and Chaos Machine crafting #57 exist in both Season 6 and Season 9 configurations without duplicating definitions.
/// </summary>
internal class SocketWeaponMixItems : InitializerBase
{
    private const byte SeedSphereNumberStart = 100;
    private const byte SeedSphereNumberEnd = 129;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketWeaponMixItems"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public SocketWeaponMixItems(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var addedItems = SeasonNineOnlyItemsUpdatePlugIn.EnsureSocketWeaponMixItems(this.Context, this.GameConfiguration);
        if (addedItems.Count > 0)
        {
            ItemRules.Apply(addedItems);
            ItemStatProfiles.Apply(this.Context, this.GameConfiguration);
        }

        this.AddCrafting();
    }

    /// <summary>
    /// Adds Chaos Machine mix #57 if not already configured.
    /// </summary>
    internal void AddCrafting()
    {
        var chaosGoblin = this.GameConfiguration.Monsters.FirstOrDefault(m => m.NpcWindow == NpcWindow.ChaosMachine);
        if (chaosGoblin is null)
        {
            return;
        }

        var existing = chaosGoblin.ItemCraftings.FirstOrDefault(c => c.Number == SocketWeaponCrafting.MixNumber);
        if (existing is not null)
        {
            return;
        }

        var crafting = this.Context.CreateNew<ItemCrafting>();
        crafting.Name = LocalizedString.FromResource(() => CraftingNames.SocketWeapon);
        crafting.Number = SocketWeaponCrafting.MixNumber;
        crafting.ItemCraftingHandlerClassName = typeof(SocketWeaponCrafting).FullName!;
        crafting.SetGuid(crafting.Number);

        var settings = this.Context.CreateNew<SimpleCraftingSettings>();
        crafting.SimpleCraftingSettings = settings;
        settings.Money = 1_000_000;
        settings.SuccessPercent = 40;
        settings.MaximumSuccessPercent = 40;
        settings.ResultItemSkillChance = 100;
        settings.ResultItemLuckOptionChance = 25;
        settings.SetGuid(crafting.Number);

        var socketOptionType = this.GameConfiguration.ItemOptionTypes.First(o => o == ItemOptionTypes.SocketOption);
        var socketItem = this.Context.CreateNew<ItemCraftingRequiredItem>();
        socketItem.MinimumAmount = 1;
        socketItem.MaximumAmount = 1;
        socketItem.MinimumItemLevel = 0;
        socketItem.MaximumItemLevel = 15;
        socketItem.SuccessResult = MixResult.Disappear;
        socketItem.FailResult = MixResult.Disappear;
        socketItem.Reference = SocketWeaponCrafting.SocketItemReference;
        socketItem.RequiredItemOptions.Add(socketOptionType);
        foreach (var item in this.GameConfiguration.Items.Where(item => item.MaximumSockets > 0))
        {
            socketItem.PossibleItems.Add(item);
        }

        settings.RequiredItems.Add(socketItem);

        var seedSphere = this.Context.CreateNew<ItemCraftingRequiredItem>();
        seedSphere.MinimumAmount = 1;
        seedSphere.MaximumAmount = 1;
        seedSphere.MinimumItemLevel = 0;
        seedSphere.MaximumItemLevel = 15;
        seedSphere.SuccessResult = MixResult.Disappear;
        seedSphere.FailResult = MixResult.Disappear;
        seedSphere.Reference = SocketWeaponCrafting.SeedSphereReference;
        foreach (var item in this.GameConfiguration.Items
                     .Where(item => item.Group == 12 && item.Number >= SeedSphereNumberStart && item.Number <= SeedSphereNumberEnd))
        {
            seedSphere.PossibleItems.Add(item);
        }

        settings.RequiredItems.Add(seedSphere);

        var chaos = this.Context.CreateNew<ItemCraftingRequiredItem>();
        chaos.MinimumAmount = 1;
        chaos.MaximumAmount = 1;
        chaos.SuccessResult = MixResult.Disappear;
        chaos.FailResult = MixResult.Disappear;
        chaos.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name.ValueInNeutralLanguage == "Jewel of Chaos"));
        settings.RequiredItems.Add(chaos);

        var creation = this.Context.CreateNew<ItemCraftingRequiredItem>();
        creation.MinimumAmount = 1;
        creation.MaximumAmount = 1;
        creation.SuccessResult = MixResult.Disappear;
        creation.FailResult = MixResult.Disappear;
        creation.PossibleItems.Add(this.GameConfiguration.Items.First(i => i.Name.ValueInNeutralLanguage == "Jewel of Creation"));
        settings.RequiredItems.Add(creation);

        var creationNote = this.Context.CreateNew<ItemCraftingRequiredItem>();
        creationNote.MinimumAmount = 1;
        creationNote.MaximumAmount = 1;
        creationNote.MinimumItemLevel = 0;
        creationNote.MaximumItemLevel = 15;
        creationNote.SuccessResult = MixResult.Disappear;
        creationNote.FailResult = MixResult.Disappear;
        creationNote.Reference = SocketWeaponCrafting.CreationNoteReference;
        foreach (var item in this.GameConfiguration.Items
                     .Where(item => item.Group == 14 && SocketWeaponCrafting.NoteToWeaponMap.ContainsKey(item.Number)))
        {
            creationNote.PossibleItems.Add(item);
        }

        settings.RequiredItems.Add(creationNote);

        chaosGoblin.ItemCraftings.Add(crafting);
    }
}
