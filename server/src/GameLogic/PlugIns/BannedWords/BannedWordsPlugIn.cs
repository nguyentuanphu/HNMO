// <copyright file="BannedWordsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BannedWords;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Configurable EN/VI banned words filter for chat messages and character/guild names (OFF by default).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Banned Words Filter", Description = "Filters banned English and Vietnamese words in chat and character/guild names (OFF by default).")]
[Guid("7F4B2C91-5A18-4D62-B390-8E2C1D4F09A1")]
public sealed class BannedWordsPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<BannedWordsConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <inheritdoc />
    public BannedWordsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active <see cref="BannedWordsPlugIn"/> from <paramref name="gameContext"/>, or <c>null</c> when disabled.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static BannedWordsPlugIn? Get(IGameContext gameContext) =>
        gameContext.FeaturePlugIns.GetPlugIn<BannedWordsPlugIn>();

    /// <summary>
    /// Evaluates an outgoing chat message and either returns it censored or blocks delivery with a player notice.
    /// </summary>
    /// <param name="sender">The player sending the chat line.</param>
    /// <param name="message">The raw chat message.</param>
    /// <returns>A tuple indicating whether the message is allowed and the filtered message text.</returns>
    public static async ValueTask<(bool Allowed, string FilteredMessage)> FilterChatAsync(Player sender, string message)
    {
        if (Get(sender.GameContext) is not { } plugIn)
        {
            return (true, message);
        }

        var cfg = plugIn.Configuration ?? new BannedWordsConfiguration();
        if (!cfg.FilterChat || (cfg.ExemptGameMasters && sender.SelectedCharacter?.CharacterStatus is CharacterStatus.GameMaster))
        {
            return (true, message);
        }

        var result = BannedWordsEngine.EvaluateChat(message, cfg);
        if (!result.IsMatched)
        {
            return (true, message);
        }

        if (cfg.ChatAction == BannedWordChatAction.Block)
        {
            var isVi = sender.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            var notice = isVi
                ? "Tin nhắn của bạn chứa từ ngữ bị cấm nên không được gửi."
                : "Your message contains restricted words and was not sent.";
            await sender.ShowBlueMessageAsync(notice).ConfigureAwait(false);
            return (false, message);
        }

        return (true, result.FilteredText);
    }

    /// <summary>
    /// Checks whether a proposed character name passes the active banned words filter.
    /// </summary>
    /// <param name="player">The player creating the character.</param>
    /// <param name="characterName">The proposed character name.</param>
    /// <returns><c>true</c> if allowed; otherwise <c>false</c> after sending a localized notice.</returns>
    public static async ValueTask<bool> IsCharacterNameAllowedAsync(Player player, string characterName)
    {
        if (Get(player.GameContext) is not { } plugIn)
        {
            return true;
        }

        var cfg = plugIn.Configuration ?? new BannedWordsConfiguration();
        if (!cfg.FilterCharacterNames)
        {
            return true;
        }

        var result = BannedWordsEngine.EvaluateName(characterName, cfg);
        if (!result.IsMatched)
        {
            return true;
        }

        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        var notice = isVi
            ? "Tên nhân vật chứa từ ngữ bị cấm."
            : "This character name contains restricted words.";
        await player.ShowBlueMessageAsync(notice).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Checks whether a proposed guild name passes the active banned words filter.
    /// </summary>
    /// <param name="creator">The player creating the guild.</param>
    /// <param name="guildName">The proposed guild name.</param>
    /// <returns><c>true</c> if allowed; otherwise <c>false</c> after sending a localized notice.</returns>
    public static async ValueTask<bool> IsGuildNameAllowedAsync(Player creator, string guildName)
    {
        if (Get(creator.GameContext) is not { } plugIn)
        {
            return true;
        }

        var cfg = plugIn.Configuration ?? new BannedWordsConfiguration();
        if (!cfg.FilterGuildNames || (cfg.ExemptGameMasters && creator.SelectedCharacter?.CharacterStatus is CharacterStatus.GameMaster))
        {
            return true;
        }

        var result = BannedWordsEngine.EvaluateName(guildName, cfg);
        if (!result.IsMatched)
        {
            return true;
        }

        var isVi = creator.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        var notice = isVi
            ? "Tên hội (Guild) chứa từ ngữ bị cấm."
            : "This guild name contains restricted words.";
        await creator.ShowBlueMessageAsync(notice).ConfigureAwait(false);
        return false;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new BannedWordsConfiguration();
}
