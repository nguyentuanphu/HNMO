// <copyright file="ChatMessageNormalProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using HNMO.GameLogic.Gens;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Gens;

/// <summary>
/// A chat message processor for normal chat.
/// </summary>
public class ChatMessageNormalProcessor : BannableChatMessageBaseProcessor
{
    /// <inheritdoc/>
    public override async ValueTask SubclassProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        sender.Logger.LogDebug("Sending Chat Message to Observers, Count: {0}", sender.Observers.Count);
        var hideFromOppositeGens = GensFeaturePlugIn.GetConfiguration(sender.GameContext) is { CanSeeOppositeGensChat: false }
                                   && sender.GetGens() != GensType.None;
        var senderGens = sender.GetGens();
        await sender.ForEachObservingAsync<IWorldObserver>(
            o => o is Player observingPlayer && IsOppositeGens(hideFromOppositeGens, senderGens, observingPlayer.GetGens())
                ? default
                : o.InvokeViewPlugInAsync<IChatViewPlugIn>(p => p.ChatMessageAsync(content.Message, sender.SelectedCharacter!.Name, ChatMessageType.Normal)),
            true).ConfigureAwait(false);
        await sender.NotifyChatMessageSentAsync(content.Message, ChatMessageType.Normal).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether an observer belongs to the gens opposite to the sender's gens.
    /// </summary>
    /// <param name="hide">Whether the chat is hidden from the opposite gens.</param>
    /// <param name="senderGens">The gens of the sender.</param>
    /// <param name="observerGens">The gens of the observer.</param>
    /// <returns><c>true</c>, if the observer must not see the message.</returns>
    internal static bool IsOppositeGens(bool hide, GensType senderGens, GensType observerGens)
    {
        return hide && senderGens != GensType.None && observerGens != GensType.None && observerGens != senderGens;
    }
}
