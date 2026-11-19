// <copyright file="LockItemChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// <c>/lockitem CELL</c> (1-64 for the bag, e1-e12 for the worn items) locks an item; <c>/lockitem list</c> lists them.
/// </summary>
[Guid("B94E3D17-2A6C-4F80-9D51-3E7C08A6B2F4")]
[PlugIn]
[Display(Name = nameof(PlugInResources.LockItemChatCommandPlugIn_Name), Description = nameof(PlugInResources.LockItemChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(AccountSafetyChatCommandArgs), CharacterStatus.Normal)]
public class LockItemChatCommandPlugIn : AccountSafetyChatCommandBase
{
    private const string Command = "/lockitem";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override async ValueTask HandleAsync(Player player, AccountSafetyPlugIn safety, string[] words, bool vietnamese)
    {
        if (words.Length >= 1 && words[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var engine = safety.GetEngine();
            var locks = engine.Locks(player.Account?.LoginName ?? string.Empty, DateTime.UtcNow);
            if (locks.Count == 0)
            {
                await player.ShowBlueMessageAsync(vietnamese ? "Bạn chưa khóa vật phẩm nào." : "You have no locked items.").ConfigureAwait(false);
                return;
            }

            foreach (var itemLock in locks.Take(10))
            {
                var waiting = itemLock.UnlockAtUtc is { } at ? (vietnamese ? $" (mở khóa lúc {engine.Local(at, true)})" : $" (unlocks {engine.Local(at, false)})") : string.Empty;
                await player.ShowBlueMessageAsync($"{itemLock.Description}{waiting}").ConfigureAwait(false);
            }

            return;
        }

        if (words.Length < 1 || SlotOf(words[0]) is not { } slot)
        {
            await player.ShowBlueMessageAsync(vietnamese
                ? "Cách dùng: /lockitem Ô (1-64 theo từng hàng của túi, e1-e12 đồ đang mặc) hoặc /lockitem list"
                : "Usage: /lockitem CELL (1-64 in the bag, row by row; e1-e12 for worn items) or /lockitem list").ConfigureAwait(false);
            return;
        }

        await safety.LockItemAsync(player, false, slot).ConfigureAwait(false);
    }
}
