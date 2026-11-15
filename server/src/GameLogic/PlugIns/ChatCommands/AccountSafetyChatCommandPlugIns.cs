// <copyright file="AccountSafetyChatCommandPlugIns.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// Arguments of the account safety chat commands (they read the raw text; this is for the help).
/// </summary>
public class AccountSafetyChatCommandArgs : ArgumentsBase
{
    /// <summary>Gets or sets the first value (a PIN or a bag cell).</summary>
    [Argument("value", false)]
    public string? Value { get; set; }

    /// <summary>Gets or sets the second value (the PIN of /unlockitem).</summary>
    [Argument("pin", false)]
    public string? Pin { get; set; }
}

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
