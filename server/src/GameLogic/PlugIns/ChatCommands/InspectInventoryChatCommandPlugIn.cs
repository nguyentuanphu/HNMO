// <copyright file="InspectInventoryChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>GM command (<c>/inspectinv &lt;character&gt;</c>) inspecting a live player's Zen and items.</summary>
[Guid("8B31D2D0-50E7-4A24-8F84-9F57B20D5E11")]
[PlugIn]
[Display(Name = "Inspect Inventory Command", Description = "Inspects a target player's Zen, equipped gear, and inventory items.")]
[ChatCommandHelp(Command, typeof(CharInfoChatCommandArgs), CharacterStatus.GameMaster)]
public sealed class InspectInventoryChatCommandPlugIn : ChatCommandPlugInBase<CharInfoChatCommandArgs>
{
    private const string Command = "/inspectinv";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CharInfoChatCommandArgs arguments)
    {
        var target = await this.GetPlayerByCharacterNameAsync(gameMaster, arguments.CharacterName ?? string.Empty).ConfigureAwait(false);
        if (target?.SelectedCharacter is null)
        {
            return;
        }

        var inv = target.Inventory;
        if (inv is null)
        {
            await gameMaster.ShowBlueMessageAsync($"Inspect {target.Name}: inventory unavailable.").ConfigureAwait(false);
            return;
        }

        var items = inv.ItemStorage.Items.OrderBy(i => i.ItemSlot).ToList();
        await gameMaster.ShowBlueMessageAsync($"[{target.Name}] Zen: {target.Money:N0} | Items: {items.Count}").ConfigureAwait(false);
        foreach (var item in items.Take(20))
        {
            var slotLabel = item.ItemSlot < InventoryConstants.EquippableSlotsCount ? $"Eq#{item.ItemSlot}" : $"Bag#{item.ItemSlot}";
            var itemName = item.Definition?.Name.GetTranslation(gameMaster.Culture) ?? $"G{item.Definition?.Group}:{item.Definition?.Number}";
            await gameMaster.ShowBlueMessageAsync($"  {slotLabel}: {itemName} +{item.Level}").ConfigureAwait(false);
        }
    }
}
