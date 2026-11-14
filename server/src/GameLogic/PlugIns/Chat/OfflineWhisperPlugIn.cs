// <copyright file="OfflineWhisperPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Keeps a whisper to a character which isn't in the game, and delivers it as a whisper, marked "(offline)",
/// when the character enters the game. The sender is told that the receiver is offline.
/// The whispers are kept in a JSON file in the server's folder, so they outlast a restart without a database table.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.OfflineWhisperPlugIn_Name), Description = nameof(PlugInResources.OfflineWhisperPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D3A7C915-6E2B-4F80-9B4D-2A61E8C07F53")]
public class OfflineWhisperPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<OfflineWhisperConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The time between entering the game and the delivery, so that the client has loaded the map.
    /// </summary>
    private static readonly TimeSpan DeliveryDelay = TimeSpan.FromSeconds(3);

    private static readonly object StoreLock = new();
    private static OfflineWhisperStore? _store;

    /// <inheritdoc />
    public OfflineWhisperConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the time between entering the game and the delivery.
    /// </summary>
    internal TimeSpan Delay { get; set; } = DeliveryDelay;

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new OfflineWhisperConfiguration();
    }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c> if it's deactivated.</returns>
    public static OfflineWhisperPlugIn? Get(IGameContext gameContext)
    {
        return gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<OfflineWhisperPlugIn>().FirstOrDefault();
    }

    /// <summary>
    /// Gets the store of the configured file. All game servers of this process share it.
    /// </summary>
    /// <returns>The store.</returns>
    public OfflineWhisperStore GetStore()
    {
        var configuration = this.Configuration ?? new OfflineWhisperConfiguration();
        var path = string.IsNullOrWhiteSpace(configuration.FileName)
            ? null
            : Path.GetFullPath(configuration.FileName.Trim(), AppContext.BaseDirectory);
        lock (StoreLock)
        {
            if (_store is null || !string.Equals(_store.FilePath, path, StringComparison.Ordinal))
            {
                _store = new OfflineWhisperStore(path);
            }

            _store.MaximumPerReceiver = Math.Max(1, configuration.MaximumPerReceiver);
            _store.MaximumAge = TimeSpan.FromDays(Math.Max(1, configuration.MaximumAgeDays));
            return _store;
        }
    }

    /// <summary>
    /// Keeps a whisper to a character which isn't in the game and tells the sender about it.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="receiverName">The receiver's character name.</param>
    /// <param name="message">The message.</param>
    /// <returns><c>true</c>, if the whisper was kept.</returns>
    public async ValueTask<bool> StoreAsync(Player sender, string receiverName, string message)
    {
        if (sender.SelectedCharacter is not { } character || string.IsNullOrWhiteSpace(receiverName))
        {
            return false;
        }

        string? exactName;
        using (var context = sender.GameContext.PersistenceContextProvider.CreateNewPlayerContext(sender.GameContext.Configuration))
        {
            exactName = (await context.GetAccountByCharacterNameAsync(receiverName).ConfigureAwait(false))?
                .Characters.FirstOrDefault(c => string.Equals(c.Name, receiverName, StringComparison.OrdinalIgnoreCase))?.Name;
        }

        if (exactName is null)
        {
            await sender.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CharacterNotFound), receiverName).ConfigureAwait(false);
            return false;
        }

        var result = this.GetStore().Add(character.Name, exactName, message, DateTime.UtcNow);
        if (result == OfflineWhisperStoreResult.ReceiverFull)
        {
            await sender.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.OfflineWhisperReceiverFull), exactName).ConfigureAwait(false);
            return false;
        }

        await sender.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.OfflineWhisperStored), exactName).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection
            || currentState != PlayerState.EnteredWorld
            || player.SelectedCharacter is not { } character)
        {
            return ValueTask.CompletedTask;
        }

        var store = this.GetStore();
        if (store.Count(character.Name, DateTime.UtcNow) == 0)
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(this.Delay).ConfigureAwait(false);
                await this.DeliverAsync(player, character).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "The offline whispers of {Character} couldn't be delivered.", character.Name);
            }
        });

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Delivers the waiting whispers of a character which is in the game.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="character">The character which entered the game.</param>
    /// <returns>The task.</returns>
    internal async ValueTask DeliverAsync(Player player, Character character)
    {
        if (player.SelectedCharacter != character || player.PlayerState.CurrentState == PlayerState.Disconnected)
        {
            // It left the game in the meantime; the whispers wait for the next time.
            return;
        }

        var whispers = this.GetStore().Take(character.Name, DateTime.UtcNow);
        var prefix = player.GetLocalizedMessage(nameof(PlayerMessage.OfflineWhisperPrefix));
        foreach (var whisper in whispers)
        {
            await player.InvokeViewPlugInAsync<IChatViewPlugIn>(p => p.ChatMessageAsync(prefix + whisper.Text, whisper.Sender, ChatMessageType.Whisper)).ConfigureAwait(false);
        }
    }
}
