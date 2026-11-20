// <copyright file="PostChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// A chat command plugin which handles post commands by sending a blue system message to all players.
/// </summary>
[Guid("ED2523C1-F66D-4B53-814E-D2FC0C1F46C0")]
[PlugIn]
[Display(Name = nameof(PlugInResources.PostChatCommandPlugIn_Name), Description = nameof(PlugInResources.PostChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(CommandKey, CharacterStatus.Normal)]
public class PostChatCommandPlugIn : IChatCommandPlugIn, ISupportCustomConfiguration<PostChatCommandConfiguration>, ISupportDefaultCustomConfiguration
{
    private const string CommandKey = "/post";

    /// <summary>
    /// The time of each player's last post.
    /// </summary>
    private readonly ConditionalWeakTable<Player, StrongBox<DateTime>> _lastPosts = new();

    /// <summary>
    /// Gets or sets the configuration (cost and wait; the Season 6/9 server suites' 1,000 zen and 5 seconds by default).
    /// </summary>
    public PostChatCommandConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public string Key => CommandKey;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var regex = new Regex(Regex.Escape(CommandKey));
        var message = regex.Replace(command, string.Empty, 1)?.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var (allowed, filteredMessage) = await BannedWordsPlugIn.FilterChatAsync(player, message).ConfigureAwait(false);
        if (!allowed)
        {
            return;
        }

        var configuration = this.Configuration ?? new PostChatCommandConfiguration();
        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        var nowUtc = DateTime.UtcNow;
        if (configuration.CooldownSeconds > 0
            && this._lastPosts.TryGetValue(player, out var lastPost)
            && nowUtc - lastPost.Value < TimeSpan.FromSeconds(configuration.CooldownSeconds))
        {
            var wait = Math.Max(1, (int)Math.Ceiling((TimeSpan.FromSeconds(configuration.CooldownSeconds) - (nowUtc - lastPost.Value)).TotalSeconds));
            await player.ShowBlueMessageAsync(isVi ? $"Hãy chờ {wait} giây nữa để /post." : $"Wait {wait} more seconds to /post.").ConfigureAwait(false);
            return;
        }

        if (configuration.ZenCost > 0 && !player.TryRemoveMoney(configuration.ZenCost))
        {
            await player.ShowBlueMessageAsync(isVi ? $"/post cần {configuration.ZenCost:N0} zen." : $"/post costs {configuration.ZenCost:N0} zen.").ConfigureAwait(false);
            return;
        }

        this._lastPosts.AddOrUpdate(player, new StrongBox<DateTime>(nowUtc));
        message = $"{player.SelectedCharacter?.Name}: {filteredMessage}";
        await player.GameContext.SendGlobalChatMessageAsync("[POST]", message, ChatMessageType.Gens).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new PostChatCommandConfiguration();
}