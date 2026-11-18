// <copyright file="CustomRankChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.CustomRanks;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Handles the <c>/title</c> chat command to list unlocked custom ranks or select a rank (<c>/title &lt;id|auto&gt;</c>).
/// </summary>
[Guid("E2F4A891-5B7C-4D32-9E10-7A3B2C8D5F19")]
[PlugIn]
[Display(Name = "Custom Title Command (/title)", Description = "Lets players view unlocked custom titles or select their active title (/title <id|auto>).")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class CustomRankChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/title";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        if (CustomRanksPlugIn.Get(player.GameContext) is not { } plugIn || !(plugIn.Configuration?.Enabled ?? false))
        {
            await player.ShowBlueMessageAsync(isVi
                ? "Hệ thống danh hiệu tùy chỉnh hiện đang tắt."
                : "Custom ranks / titles are currently disabled.").ConfigureAwait(false);
            return;
        }

        var arg = command.Length > Command.Length ? command[Command.Length..].Trim() : string.Empty;
        if (string.IsNullOrEmpty(arg))
        {
            var engine = plugIn.GetEngine();
            var subject = CustomRanksPlugIn.BuildSubject(player);
            var unlocked = engine.GetUnlockedTiers(subject);
            var active = engine.ResolveActiveTier(subject);
            if (unlocked.Count == 0)
            {
                await player.ShowBlueMessageAsync(isVi
                    ? "Bạn chưa mở khóa danh hiệu tùy chỉnh nào."
                    : "You have not unlocked any custom titles yet.").ConfigureAwait(false);
                return;
            }

            var list = string.Join(", ", unlocked.Select(t => $"{t.Id} ({(isVi ? t.TitleVi : t.TitleEn)})"));
            var activeLabel = active is null ? "-" : (isVi ? active.TitleVi : active.TitleEn);
            await player.ShowBlueMessageAsync(isVi
                ? $"Danh hiệu đang dùng: {activeLabel}. Đã mở: {list}. Gõ /title <id|auto> để đổi."
                : $"Active title: {activeLabel}. Unlocked: {list}. Type /title <id|auto> to switch.").ConfigureAwait(false);
            await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
            return;
        }

        var (_, msgEn, msgVi) = await plugIn.SelectRankAsync(player, arg).ConfigureAwait(false);
        await player.ShowBlueMessageAsync(isVi ? msgVi : msgEn).ConfigureAwait(false);
    }
}
