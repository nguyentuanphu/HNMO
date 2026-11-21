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
        if (await RefuseMutedOrSpamAsync(sender, content.Message, this.BanServiceOverride).ConfigureAwait(false))
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

    /// <summary>
    /// Refuses a message of a muted sender (mute of the moderation or chat ban of the account) or one the spam guard
    /// stops, and tells the sender why. Chat commands which send text to other players (e.g. /post) use it too.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="message">The message.</param>
    /// <param name="banService">The moderation service; the default one when <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the message is refused.</returns>
    internal static async ValueTask<bool> RefuseMutedOrSpamAsync(Player sender, string message, BanManagerService? banService = null)
    {
        var nowUtc = DateTime.UtcNow;
        var activeMute = (banService ?? BanManagerService.Default).FindActiveChatMute(sender.Account?.LoginName, sender.SelectedCharacter?.Name, nowUtc);
        if (activeMute is not null)
        {
            var isVi = sender.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            await sender.InvokeViewPlugInAsync<IHnmoBanNoticeViewPlugIn>(v => v.SendBanNoticeAsync(BanManagerService.BuildNoticeJson(activeMute))).ConfigureAwait(false);
            await sender.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(BanManagerService.FormatPlayerNotice(activeMute, isVi), HNMO.Interfaces.MessageType.BlueNormal)).ConfigureAwait(false);
            return true;
        }

        var untilUtc = sender.Account?.ChatBanUntil;
        if (untilUtc is { } until && until > nowUtc)
        {
            var isVi = sender.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            var untilSydney = BanManagerService.FormatSydneyExpiry(untilUtc, isVi);
            var msg = isVi
                ? $"Bạn đang bị cấm chat đến {untilSydney}."
                : $"You are muted until {untilSydney}.";
            await sender.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, HNMO.Interfaces.MessageType.BlueNormal)).ConfigureAwait(false);
            return true;
        }

        return !await ChatSpamGuardPlugIn.CheckAsync(sender, message).ConfigureAwait(false);
    }
}
