// <copyright file="ChatMessageCommandProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.GmManager;

/// <summary>
/// A chat message processor which handles chat commands.
/// </summary>
public class ChatMessageCommandProcessor : IChatMessageProcessor
{
    private readonly GmPermissionStore? _gmStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageCommandProcessor"/> class.
    /// </summary>
    public ChatMessageCommandProcessor()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageCommandProcessor"/> class with an explicit store (testing).
    /// </summary>
    /// <param name="gmStore">Optional isolated GM permission store.</param>
    internal ChatMessageCommandProcessor(GmPermissionStore? gmStore)
    {
        this._gmStore = gmStore;
    }

    /// <inheritdoc />
    public async ValueTask ProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        var commandKey = content.Message.Split(' ').First();
        var commandHandler = sender.GameContext.PlugInManager.GetStrategy<IChatCommandPlugIn>(commandKey);
        if (commandHandler is null)
        {
            return;
        }

        if (sender.SelectedCharacter!.CharacterStatus < commandHandler.MinCharacterStatusRequirement)
        {
            sender.Logger.LogWarning($"{sender.Name} is trying to execute {commandKey} command without meeting the requirements");
            return;
        }

        if (commandHandler.MinCharacterStatusRequirement == CharacterStatus.GameMaster)
        {
            var store = this._gmStore ?? GmPermissionStore.Default;
            var (allowed, required) = store.AuthorizeAndAudit(
                sender.SelectedCharacter.Name,
                sender.Account?.LoginName ?? string.Empty,
                commandKey,
                content.Message);
            if (!allowed)
            {
                var isVi = string.Equals(sender.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
                var label = GmCommandPermissionCatalog.FormatLabel(required, isVi);
                await sender.ShowBlueMessageAsync(isVi
                    ? $"Từ chối lệnh GM '{commandKey}': thiếu quyền '{label}'."
                    : $"GM command '{commandKey}' denied: missing permission '{label}'.").ConfigureAwait(false);
                return;
            }
        }

        await commandHandler.HandleCommandAsync(sender, content.Message).ConfigureAwait(false);
    }
}