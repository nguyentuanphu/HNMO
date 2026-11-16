// <copyright file="RankingChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.PlayerRanking;
using HNMO.PlugIns;

/// <summary>
/// Handles the <c>/rank</c> chat command, pushing the live ranking panel and printing top 5 players in chat.
/// </summary>
[Guid("5F1E9A30-4C6B-4D12-9A7E-8C2F4E1B3D01")]
[PlugIn]
[Display(Name = "Ranking Command (/rank)", Description = "Displays the top characters in chat and refreshes the in-game Ranking panel.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class RankingChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/rank";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var plugIn = PlayerRankingPlugIn.Get(player.GameContext);
        if (plugIn is null)
        {
            return;
        }

        await plugIn.SendStateAsync(player, requested: false).ConfigureAwait(false);

        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        using var context = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration);
        var top5 = await context.GetCharacterRankingAsync(Stats.Level.Id, Stats.MasterLevel.Id, Stats.Resets.Id, 5).ConfigureAwait(false);
        if (top5.Count == 0)
        {
            await player.ShowBlueMessageAsync(isVi
                ? "Chưa có dữ liệu xếp hạng nhân vật."
                : "No character ranking entries found yet.").ConfigureAwait(false);
            return;
        }

        var header = isVi ? "--- TOP 5 Bảng Xếp Hạng (Nhấn Shift+R để mở bảng đầy đủ) ---" : "--- TOP 5 Rankings (Press Shift+R for full panel) ---";
        await player.ShowBlueMessageAsync(header).ConfigureAwait(false);
        for (var i = 0; i < top5.Count; i++)
        {
            var e = top5[i];
            await player.ShowBlueMessageAsync($"#{i + 1} {e.Name} — RS {e.Resets} | Lv {e.Level} (+ML {e.MasterLevel})").ConfigureAwait(false);
        }
    }
}
