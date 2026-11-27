// <copyright file="StartRoyaleChatCommandPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.Properties;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// A game master's "/startroyale [seconds]" in the Arena: opens the battle royale's sign-up now, for a start in the
/// given seconds (default 120), besides the set times.
/// </summary>
[Guid("6C2D9A47-1E85-4B3F-9D70-A5E4C1B82F36")]
[PlugIn]
[Display(Name = nameof(PlugInResources.StartRoyaleChatCommandPlugIn_Name), Description = nameof(PlugInResources.StartRoyaleChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public sealed class StartRoyaleChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/startroyale";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc/>
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var vi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        if (!ArenaRules.Active || ArenaPlugIn.Get(player.GameContext) is not { } arena)
        {
            await player.ShowBlueMessageAsync(vi ? "Lệnh này chỉ dùng trong Đấu trường." : "This command works in the Arena only.").ConfigureAwait(false);
            return;
        }

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var seconds = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? Math.Clamp(s, 10, 3600) : 120;
        if (arena.Royale.Running)
        {
            await player.ShowBlueMessageAsync(vi ? "Đang có trận đấu sinh tồn." : "A battle royale is being fought.").ConfigureAwait(false);
            return;
        }

        arena.Royale.ForceStart(TimeSpan.FromSeconds(seconds), DateTime.UtcNow);
        await player.ShowBlueMessageAsync(vi ? $"Đã mở đăng ký đấu sinh tồn; trận bắt đầu sau {seconds} giây." : $"Battle royale sign-up opened; the match starts in {seconds} seconds.").ConfigureAwait(false);
    }
}
