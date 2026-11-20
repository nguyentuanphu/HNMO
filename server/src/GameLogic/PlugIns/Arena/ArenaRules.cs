// <copyright file="ArenaRules.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using HNMO.Interfaces;

/// <summary>
/// What the HNMO Arena doesn't allow (owner's choice, 2026-11-20): everyone plays the same arena character of their class,
/// so no trade, personal store, dropping or picking up items, moving to other maps, chat commands, cash shop or HNMO's
/// own windows (market, vault pages, mail, ...). The Arena keeps its own database, so nothing there reaches a home server
/// anyway; these rules keep the battles fair. Only the Arena program refuses; every other server is unchanged.
/// </summary>
public static class ArenaRules
{
    /// <summary>The F5 sub code of the Arena travel request (F5 52), the one HNMO window the Arena keeps.</summary>
    public const byte ArenaTravelSubCode = 0x52;

    /// <summary>What a refusal is about.</summary>
    public enum Action
    {
        /// <summary>A trade.</summary>
        Trade,

        /// <summary>A personal store.</summary>
        Store,

        /// <summary>Dropping an item.</summary>
        Drop,

        /// <summary>Picking up an item.</summary>
        PickUp,

        /// <summary>Moving to another map.</summary>
        Warp,

        /// <summary>A chat command.</summary>
        Command,

        /// <summary>The cash shop.</summary>
        CashShop,

        /// <summary>The vault.</summary>
        Vault,
    }

    /// <summary>Gets a value indicating whether this program is the Arena (its rules apply).</summary>
    public static bool Active => HnmoSeasonInstance.IsArena;

    /// <summary>Refuses an action in the Arena with an EN/VI notice.</summary>
    /// <param name="player">The player.</param>
    /// <param name="action">The action.</param>
    /// <returns><c>true</c>, if it's refused (this is the Arena).</returns>
    public static async ValueTask<bool> RefuseAsync(Player player, Action action)
    {
        if (!Active)
        {
            return false;
        }

        var vi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        var (en, viText) = action switch
        {
            Action.Trade => ("trade", "giao dịch"),
            Action.Store => ("open a store", "mở cửa hàng"),
            Action.Drop => ("drop items", "vứt đồ"),
            Action.PickUp => ("pick up items", "nhặt đồ"),
            Action.Warp => ("go to other maps", "đi bản đồ khác"),
            Action.Command => ("use chat commands", "dùng lệnh chat"),
            Action.CashShop => ("use the cash shop", "dùng cửa hàng xu"),
            _ => ("use the vault", "dùng rương"),
        };
        await player.ShowBlueMessageAsync(vi
            ? $"Trong Đấu trường không thể {viText}: mọi người dùng nhân vật đấu trường giống nhau."
            : $"You can't {en} in the Arena: everyone plays the same arena characters.").ConfigureAwait(false);
        return true;
    }

    /// <summary>Gets a value indicating whether an HNMO message (F5 sub code 2 and up) is handled in the Arena.</summary>
    /// <param name="subCode">The sub code.</param>
    /// <returns><c>true</c>, if it's handled: everywhere but the Arena, and in the Arena only the travel request.</returns>
    public static bool AllowsHnmoMessage(byte subCode) => !Active || subCode == ArenaTravelSubCode;
}
