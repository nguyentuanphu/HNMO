// <copyright file="LockChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.PlugIns;

/// <summary>
/// <c>/lock PIN</c>: immediately re-locks protected second-password actions in the current session.
/// </summary>
[Guid("1D9E5C73-8F24-4A10-B36E-5A2F9C1D8E40")]
[PlugIn]
[Display(Name = "Lock Account Safety Command (/lock)", Description = "Re-locks the second password for vault, trade, and personal shop immediately.")]
[ChatCommandHelp(Command, typeof(AccountSafetyChatCommandArgs), CharacterStatus.Normal)]
public sealed class LockChatCommandPlugIn : AccountSafetyChatCommandBase
{
    private const string Command = "/lock";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override async ValueTask HandleAsync(Player player, AccountSafetyPlugIn safety, string[] words, bool vietnamese)
    {
        if (words.Length < 1)
        {
            await player.ShowBlueMessageAsync(vietnamese
                ? "Cách dùng: /lock 1234 (khóa ngay mật khẩu cấp 2 cho phiên hiện tại)"
                : "Usage: /lock 1234 (immediately re-locks second password for the current session)").ConfigureAwait(false);
            return;
        }

        await safety.LockPinAsync(player, AccountSafetyEngine.NormalizePin(words[0])).ConfigureAwait(false);
    }
}
