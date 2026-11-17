// <copyright file="OfflineTradeChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.OfflineTrade;
using HNMO.PlugIns;

/// <summary>
/// Handles the <c>/offtrade</c> chat command so players can leave their personal shop open while offline.
/// </summary>
[Guid("2D71F48B-E3A9-4920-B4E1-6A9C38F0D1B4")]
[PlugIn]
[Display(Name = "Offline Trade Command (/offtrade)", Description = "Disconnects the client while keeping the character and open personal store alive in town.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class OfflineTradeChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/offtrade";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command) =>
        await OfflineTradePlugIn.TryStartAsync(player).ConfigureAwait(false);
}
