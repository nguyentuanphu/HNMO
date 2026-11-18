// <copyright file="BannableChatMessageBaseProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.GameLogic.PlugIns.Chat;
using HNMO.GameLogic.PlugIns.Moderation;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Login;

/// <summary>
/// A chat message processor for normal chat.
/// </summary>
public abstract class BannableChatMessageBaseProcessor : IChatMessageProcessor
{
    /// <summary>Gets or sets an optional moderation service override for unit tests.</summary>
    internal BanManagerService? BanServiceOverride { get; set; }

    /// <inheritdoc />
    public async ValueTask ProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        var nowUtc = DateTime.UtcNow;
        var activeMute = (this.BanServiceOverride ?? BanManagerService.Default).FindActiveChatMute(sender.Account?.LoginName, sender.SelectedCharacter?.Name, nowUtc);
        if (activeMute is not null)
        {
            var isVi = sender.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            await sender.InvokeViewPlugInAsync<IHnmoBanNoticeViewPlugIn>(v => v.SendBanNoticeAsync(BanManagerService.BuildNoticeJson(activeMute))).ConfigureAwait(false);
            await sender.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(BanManagerService.FormatPlayerNotice(activeMute, isVi), HNMO.Interfaces.MessageType.BlueNormal)).ConfigureAwait(false);
            return;
        }

        TimeSpan remainingChatBan = this.RemainingChatBanTimeSpan(sender);
        if (this.IsSenderBanned(remainingChatBan))
        {
            var untilUtc = sender.Account?.ChatBanUntil;
            var isVi = sender.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            var untilSydney = BanManagerService.FormatSydneyExpiry(untilUtc, isVi);
            var msg = isVi
                ? $"Bạn đang bị cấm chat đến {untilSydney}."
                : $"You are muted until {untilSydney}.";
            await sender.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, HNMO.Interfaces.MessageType.BlueNormal)).ConfigureAwait(false);
            return;
        }

        if (!await ChatSpamGuardPlugIn.CheckAsync(sender, content.Message).ConfigureAwait(false))
        {
            return;
        }

        var (allowed, filteredMessage) = await BannedWordsPlugIn.FilterChatAsync(sender, content.Message).ConfigureAwait(false);
        if (!allowed)
        {
            return;
        }

        await this.SubclassProcessMessageAsync(sender, (filteredMessage, content.PlayerName)).ConfigureAwait(false);
    }

    /// <summary>
    /// A method to be overriden for processing a specific chat message.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="content">The content.</param>
    /// <returns>A value task with the result.</returns>
    public abstract ValueTask SubclassProcessMessageAsync(Player sender, (string Message, string PlayerName) content);

    private TimeSpan RemainingChatBanTimeSpan(Player sender)
    {
        DateTime chatBanUntil = sender.Account?.ChatBanUntil ?? default;
        DateTime currentDateTime = DateTime.UtcNow;
        return chatBanUntil - currentDateTime;
    }

    private bool IsSenderBanned(TimeSpan remainingChatBan) => remainingChatBan > TimeSpan.Zero;
}
