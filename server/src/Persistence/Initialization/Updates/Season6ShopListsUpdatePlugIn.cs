// <copyright file="Season6ShopListsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.PlugIns;

/// <summary>
/// This update gives the NPC shops the lists of the Season 6 server suite (owner's choice, 21 Nov 2026):
/// in Elbeland Silvia (415) sells the scrolls, Rhea (416) the potions and Marce (417) the weapons which Bolo sells;
/// Natasha (379) in Devias sells the armors and weapons Rhea sold; Lumen (255) also sells the Illusion Temple tickets +6.
/// The shops are only rearranged while they still have HNMO's old lists.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("8F4C2A19-D6E3-4B75-90A8-3E1F5C7B2D64")]
public class Season6ShopListsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "NPC shop lists of the Season 6 server suite";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Silvia sells scrolls, Rhea potions, Marce weapons, Natasha armors and weapons; Lumen adds the Illusion Temple tickets +6.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 17, 20, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MonsterDefinition? Npc(short number) => gameConfiguration.Monsters.FirstOrDefault(m => m.Number == number);
        var silvia = Npc(415);
        var rhea = Npc(416);
        var marce = Npc(417);
        var natasha = Npc(379);
        var bolo = Npc(578);

        // HNMO's old arrangement: Silvia potions, Rhea armors and weapons, Marce scrolls, Natasha no shop.
        if (silvia?.MerchantStore is { } potions && potions.Items.Any(i => i.Definition?.Group == 14)
            && rhea?.MerchantStore is { } armors && armors.Items.Any(i => i.Definition?.Group is >= 7 and <= 11)
            && marce?.MerchantStore is { } scrolls && scrolls.Items.All(i => i.Definition?.Group == 15)
            && natasha is { MerchantStore: null }
            && bolo?.MerchantStore is { } weapons)
        {
            silvia.MerchantStore = scrolls;
            rhea.MerchantStore = potions;
            natasha.MerchantStore = armors;
            natasha.NpcWindow = NpcWindow.Merchant;

            var marceWeapons = context.CreateNew<ItemStorage>();
            foreach (var item in weapons.Items)
            {
                marceWeapons.Items.Add(Copy(context, item));
            }

            marce.MerchantStore = marceWeapons;
        }

        if (Npc(255)?.MerchantStore is { } lumen)
        {
            AddTicket(lumen, 58, 13, 50);
            AddTicket(lumen, 63, 13, 49);
        }

        return ValueTask.CompletedTask;

        void AddTicket(ItemStorage store, byte slot, byte group, short number)
        {
            if (store.Items.Any(i => i.Definition?.Group == group && i.Definition.Number == number && i.Level == 6)
                || store.Items.Any(i => i.ItemSlot == slot)
                || gameConfiguration.Items.FirstOrDefault(d => d.Group == group && d.Number == number) is not { } definition)
            {
                return;
            }

            var ticket = context.CreateNew<Item>();
            ticket.Definition = definition;
            ticket.ItemSlot = slot;
            ticket.Level = 6;
            ticket.Durability = 1;
            store.Items.Add(ticket);
        }
    }

    private static Item Copy(IContext context, Item source)
    {
        var item = context.CreateNew<Item>();
        item.Definition = source.Definition;
        item.ItemSlot = source.ItemSlot;
        item.Level = source.Level;
        item.Durability = source.Durability;
        item.HasSkill = source.HasSkill;
        foreach (var link in source.ItemOptions)
        {
            var copy = context.CreateNew<ItemOptionLink>();
            copy.ItemOption = link.ItemOption;
            copy.Level = link.Level;
            copy.Index = link.Index;
            item.ItemOptions.Add(copy);
        }

        return item;
    }
}
