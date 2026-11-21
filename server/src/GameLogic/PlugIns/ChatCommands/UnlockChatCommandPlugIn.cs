// <copyright file="UnlockChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// <c>/unlock PIN</c>: enters the second password (the vault PIN).
/// </summary>
[Guid("2C7A9E41-5B83-4D16-A0F2-91E64B3D7C58")]
[PlugIn]
[Display(Name = nameof(PlugInResources.UnlockChatCommandPlugIn_Name), Description = nameof(PlugInResources.UnlockChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(AccountSafetyChatCommandArgs), CharacterStatus.Normal)]
public class UnlockChatCommandPlugIn : AccountSafetyChatCommandBase
{
    private const string Command = "/unlock";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override async ValueTask HandleAsync(Player player, AccountSafetyPlugIn safety, string[] words, bool vietnamese)
    {
        if (words.Length < 1)
        {
            await player.ShowBlueMessageAsync(vietnamese ? "Cách dùng: /unlock 1234 (mật khẩu cấp 2, 4 chữ số)" : "Usage: /unlock 1234 (the second password, 4 digits)").ConfigureAwait(false);
            return;
        }

        await safety.EnterPinAsync(player, AccountSafetyEngine.NormalizePin(words[0]), true).ConfigureAwait(false);
    }
}
