// <copyright file="UnlockItemChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// <c>/unlockitem CELL PIN</c>: asks the unlock of an item with the second password (it may wait).
/// </summary>
[Guid("5F18C6A2-7D94-4B3E-8E07-C2A9D41B6E35")]
[PlugIn]
[Display(Name = nameof(PlugInResources.UnlockItemChatCommandPlugIn_Name), Description = nameof(PlugInResources.UnlockItemChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(AccountSafetyChatCommandArgs), CharacterStatus.Normal)]
public class UnlockItemChatCommandPlugIn : AccountSafetyChatCommandBase
{
    private const string Command = "/unlockitem";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override async ValueTask HandleAsync(Player player, AccountSafetyPlugIn safety, string[] words, bool vietnamese)
    {
        if (words.Length < 1 || SlotOf(words[0]) is not { } slot)
        {
            await player.ShowBlueMessageAsync(vietnamese
                ? "Cách dùng: /unlockitem Ô PIN (1-64 theo từng hàng của túi, e1-e12 đồ đang mặc)"
                : "Usage: /unlockitem CELL PIN (1-64 in the bag, row by row; e1-e12 for worn items)").ConfigureAwait(false);
            return;
        }

        await safety.UnlockItemAsync(player, false, slot, words.Length > 1 ? AccountSafetyEngine.NormalizePin(words[1]) : null).ConfigureAwait(false);
    }
}
