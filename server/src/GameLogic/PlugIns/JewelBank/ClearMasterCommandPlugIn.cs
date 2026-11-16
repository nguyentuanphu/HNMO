// <copyright file="ClearMasterCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// In-game <c>/clearmaster</c> command: refunds all invested master skill points when <see cref="JewelBankPlugIn"/> is active.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Clear Master Command", Description = "Lets players run /clearmaster to refund spent master skill points.")]
[Guid("517F47DA-8C31-428C-9F60-44A39B7E32C4")]
[ChatCommandHelp(Command, typeof(ClearMasterArguments), CharacterStatus.Normal)]
public sealed class ClearMasterCommandPlugIn : ChatCommandPlugInBase<ClearMasterArguments>
{
    private const string Command = "/clearmaster";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, ClearMasterArguments arguments)
    {
        if (JewelBankPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(
                isVi ? "Ngân hàng ngọc / tẩy master đang tắt." : "Jewel Bank / Clear Master is disabled.",
                MessageType.BlueNormal)).ConfigureAwait(false);
            return;
        }

        await plugIn.ClearMasterSkillsAsync(player, fromPanel: false).ConfigureAwait(false);
    }
}
