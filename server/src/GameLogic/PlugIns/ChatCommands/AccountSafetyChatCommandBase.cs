// <copyright file="AccountSafetyChatCommandBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// The base of the account safety chat commands: they read their words from the raw text.
/// </summary>
public abstract class AccountSafetyChatCommandBase : ChatCommandPlugInBase<AccountSafetyChatCommandArgs>
{
    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <summary>
    /// Reads a cell: 1-64 is the bag (row by row), e1-e12 the worn items.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The inventory slot; <c>null</c> when it isn't a cell.</returns>
    public static byte? SlotOf(string? text)
    {
        var value = text?.Trim().ToLowerInvariant() ?? string.Empty;
        var equipment = value.StartsWith('e');
        if (!int.TryParse(equipment ? value[1..] : value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        if (equipment)
        {
            return number is >= 1 and <= 12 ? (byte)(number - 1) : null;
        }

        return number is >= 1 and <= 64 ? (byte)(InventoryConstants.EquippableSlotsCount + number - 1) : null;
    }

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        try
        {
            var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).ToArray();
            var vietnamese = player.Culture.TwoLetterISOLanguageName == "vi";
            if (AccountSafetyPlugIn.Get(player.GameContext) is not { } safety)
            {
                await player.ShowBlueMessageAsync(vietnamese ? "Máy chủ này chưa bật bảo vệ tài khoản." : "Account safety isn't on for this server.").ConfigureAwait(false);
                return;
            }

            await this.HandleAsync(player, safety, words, vietnamese).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error handling the chat command '{Command}'.", this.Key);
        }
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="safety">The account safety.</param>
    /// <param name="words">The words after the command.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <returns>The task.</returns>
    protected abstract ValueTask HandleAsync(Player player, AccountSafetyPlugIn safety, string[] words, bool vietnamese);

    /// <inheritdoc />
    protected override ValueTask DoHandleCommandAsync(Player player, AccountSafetyChatCommandArgs arguments) => ValueTask.CompletedTask;
}
