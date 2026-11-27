// <copyright file="StartGuildWarChatCommandPlugIn.cs" company="HNMO">
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
/// A game master's "/startguildwar [seconds]" in the Arena: opens the guild war's sign-up now (open until the start), for a start in the
/// given seconds (default 120), besides the set times.
/// </summary>
[Guid("9B4E7F12-3C68-4D2A-A51E-7D0F8C3B6E49")]
[PlugIn]
[Display(Name = nameof(PlugInResources.StartGuildWarChatCommandPlugIn_Name), Description = nameof(PlugInResources.StartGuildWarChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public sealed class StartGuildWarChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/startguildwar";

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
        if (arena.GuildWar.Busy)
        {
            await player.ShowBlueMessageAsync(vi ? "Đang có bang chiến." : "A guild war is being fought.").ConfigureAwait(false);
            return;
        }

        arena.GuildWar.ForceStart(TimeSpan.FromSeconds(seconds), DateTime.UtcNow);
        await player.ShowBlueMessageAsync(vi ? $"Đã mở đăng ký bang chiến; bắt đầu sau {seconds} giây." : $"Guild war sign-up opened; it starts in {seconds} seconds.").ConfigureAwait(false);
    }
}
