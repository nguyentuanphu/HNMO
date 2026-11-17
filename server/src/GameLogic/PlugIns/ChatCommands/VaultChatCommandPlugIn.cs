// <copyright file="VaultChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.PlugIns;

/// <summary>
/// Player chat command <c>/vault [index|buy]</c> to check unlocked vaults, switch vaults, or buy extra vaults (ROADMAP 14.5).
/// </summary>
[Guid("1D4E7A93-8C52-4F6B-A019-5E2B8D3C7A40")]
[PlugIn]
[Display(Name = "Multi-vault command (/vault)", Description = "Switch active vault (/vault N) or buy extra vaults (/vault buy).")]
[ChatCommandHelp(Command, typeof(VaultChatCommandArgs), CharacterStatus.Normal)]
public sealed class VaultChatCommandPlugIn : ChatCommandPlugInBase<VaultChatCommandArgs>
{
    private const string Command = "/vault";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, VaultChatCommandArgs arguments)
    {
        var plugIn = MultiVaultPlugIn.Get(player.GameContext);
        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        if (plugIn is null || plugIn.Configuration is not { Enabled: true })
        {
            await player.ShowBlueMessageAsync(isVi ? "Tính năng nhiều rương đang tắt." : "Multi-vault is currently disabled.").ConfigureAwait(false);
            return;
        }

        var arg = arguments.Target?.Trim();
        if (string.IsNullOrEmpty(arg))
        {
            var unlocked = plugIn.GetUnlockedVaultCount(player);
            var active = player.Account is { LoginName: { Length: > 0 } login } ? plugIn.GetActiveVaultIndex(login) : 0;
            var msg = isVi
                ? $"Rương hiện tại: #{active}. Đã mở khóa: {unlocked} (dùng /vault <số> hoặc /vault buy)."
                : $"Current vault: #{active}. Unlocked: {unlocked} (use /vault <index> or /vault buy).";
            await player.ShowBlueMessageAsync(msg).ConfigureAwait(false);
            return;
        }

        if (string.Equals(arg, "buy", StringComparison.OrdinalIgnoreCase))
        {
            var (_, msg) = await plugIn.BuyExtraVaultAsync(player).ConfigureAwait(false);
            await player.ShowBlueMessageAsync(msg).ConfigureAwait(false);
            return;
        }

        if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            var (_, msg) = await plugIn.SwitchVaultAsync(player, index).ConfigureAwait(false);
            await player.ShowBlueMessageAsync(msg).ConfigureAwait(false);
            return;
        }

        await player.ShowBlueMessageAsync(isVi ? "Cú pháp: /vault [số | buy]" : "Usage: /vault [index | buy]").ConfigureAwait(false);
    }
}
