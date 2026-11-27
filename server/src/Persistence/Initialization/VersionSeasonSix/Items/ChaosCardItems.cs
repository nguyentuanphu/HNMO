// <copyright file="ChaosCardItems.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Items;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerActions.Craftings;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;

/// <summary>
/// Initializer for the four Chaos Cards (<c>14:54, 14:92, 14:93, 14:95</c>) and the
/// Chaos Card Master combination (<c>MixType = 40</c>).
/// </summary>
public class ChaosCardItems : InitializerBase
{
    /// <summary>
    /// The item group of all Chaos Card items.
    /// </summary>
    internal const byte ItemGroup = 14;

    /// <summary>
    /// The item number of the Chaos Card (<c>14:54</c>).
    /// </summary>
    internal const short ChaosCardNumber = 54;

    /// <summary>
    /// The item number of the Chaos Card Gold (<c>14:92</c>).
    /// </summary>
    internal const short ChaosCardGoldNumber = 92;

    /// <summary>
    /// The item number of the Chaos Card Rare (<c>14:93</c>).
    /// </summary>
    internal const short ChaosCardRareNumber = 93;

    /// <summary>
    /// The item number of the Chaos Card Mini (<c>14:95</c>).
    /// </summary>
    internal const short ChaosCardMiniNumber = 95;

    /// <summary>
    /// The crafting number sent by the client for Chaos Card combinations (<c>MixType = 40</c>).
    /// </summary>
    internal const byte CraftingNumber = ChaosCardCrafting.MixNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaosCardItems"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public ChaosCardItems(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.CreateItems();
    }

    /// <summary>
    /// Creates the missing Chaos Card item definitions and returns the newly added definitions.
    /// </summary>
    /// <returns>The created item definitions.</returns>
    internal IReadOnlyCollection<ItemDefinition> CreateItems()
    {
        var created = new List<ItemDefinition>(4);
        var specs = new (short Number, LocalizedString Name)[]
        {
            (ChaosCardNumber, LocalizedString.FromResource(() => ItemNames.ChaosCard)),
            (ChaosCardGoldNumber, LocalizedString.FromResource(() => ItemNames.ChaosCardGold)),
            (ChaosCardRareNumber, LocalizedString.FromResource(() => ItemNames.ChaosCardRare)),
            (ChaosCardMiniNumber, LocalizedString.FromResource(() => ItemNames.ChaosCardMini)),
        };

        foreach (var (number, name) in specs)
        {
            if (this.TryCreateCard(number, name) is { } card)
            {
                created.Add(card);
            }
        }

        return created;
    }

    /// <summary>
    /// Registers the Chaos Card combination recipe (<c>Number = 40</c>) on Chaos Card Master (<c>#375</c>)
    /// when not already present.
    /// </summary>
    internal void AddCraftings()
    {
        if (this.GameConfiguration.Monsters.FirstOrDefault(m => m.NpcWindow == NpcWindow.ChaosCardCombination) is not { } chaosCardMaster
            || chaosCardMaster.ItemCraftings.Any(c => c.Number == CraftingNumber))
        {
            return;
        }

        var cards = new[] { ChaosCardNumber, ChaosCardGoldNumber, ChaosCardRareNumber, ChaosCardMiniNumber }
            .Select(this.FindItem)
            .Where(item => item is not null)
            .Cast<ItemDefinition>()
            .ToList();

        if (cards.Count == 0)
        {
            return;
        }

        var crafting = this.Context.CreateNew<ItemCrafting>();
        crafting.Name = LocalizedString.FromResource(() => CraftingNames.ChaosCardCombination);
        crafting.Number = CraftingNumber;
        crafting.ItemCraftingHandlerClassName = typeof(ChaosCardCrafting).FullName!;
        crafting.SetGuid(CraftingNumber);

        var settings = this.Context.CreateNew<SimpleCraftingSettings>();
        crafting.SimpleCraftingSettings = settings;
        settings.SuccessPercent = 100;
        settings.Money = 0;
        settings.ResultItemSelect = ResultItemSelection.Any;

        var requiredCard = this.Context.CreateNew<ItemCraftingRequiredItem>();
        requiredCard.MinimumAmount = 1;
        requiredCard.MaximumAmount = 1;
        requiredCard.SuccessResult = MixResult.Disappear;
        requiredCard.FailResult = MixResult.Disappear;
        foreach (var card in cards)
        {
            requiredCard.PossibleItems.Add(card);
        }

        settings.RequiredItems.Add(requiredCard);

        var addedResults = new HashSet<(byte Group, short Number)>();
        foreach (var entry in ChaosCardCrafting.Sections.SelectMany(s => s.Entries))
        {
            if (!addedResults.Add((entry.Group, entry.Number)))
            {
                continue;
            }

            if (this.GameConfiguration.Items.FirstOrDefault(i => i.Group == entry.Group && i.Number == entry.Number) is not { } itemDef)
            {
                continue;
            }

            var resultItem = this.Context.CreateNew<ItemCraftingResultItem>();
            resultItem.ItemDefinition = itemDef;
            settings.ResultItems.Add(resultItem);
        }

        chaosCardMaster.ItemCraftings.Add(crafting);
    }

    private ItemDefinition? TryCreateCard(short number, LocalizedString name)
    {
        if (this.FindItem(number) is not null)
        {
            return null;
        }

        var def = this.Context.CreateNew<ItemDefinition>();
        def.Group = ItemGroup;
        def.Number = number;
        def.Name = name;
        def.Width = 1;
        def.Height = 1;
        def.Durability = 1;
        def.DropsFromMonsters = false;
        def.DropLevel = 0;
        def.MaximumItemLevel = 0;
        def.IsBoundToCharacter = false;
        def.SetGuid(ItemGroup, number);
        this.GameConfiguration.Items.Add(def);
        return def;
    }

    private ItemDefinition? FindItem(short number) =>
        this.GameConfiguration.Items.FirstOrDefault(i => i.Group == ItemGroup && i.Number == number);
}
