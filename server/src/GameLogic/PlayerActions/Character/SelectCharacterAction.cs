// <copyright file="SelectCharacterAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Character;

using HNMO.GameLogic.PlugIns.Moderation;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Login;

/// <summary>
/// Action to select a character and enter the world with it.
/// </summary>
public class SelectCharacterAction
{
    private readonly BanManagerService? _banService;

    /// <summary>Initializes a new instance of the <see cref="SelectCharacterAction"/> class.</summary>
    public SelectCharacterAction()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SelectCharacterAction"/> class with an explicit moderation store.</summary>
    /// <param name="banService">Isolated moderation service for testing.</param>
    internal SelectCharacterAction(BanManagerService banService)
    {
        this._banService = banService;
    }

    /// <summary>
    /// Selects the character and enters the world.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="characterName">Name of the character.</param>
    public async ValueTask SelectCharacterAsync(Player player, string characterName)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        if (player.PlayerState.CurrentState != PlayerState.CharacterSelection)
        {
            player.Logger.LogError("Could not select character because of wrong current player state: {0}", player.PlayerState.CurrentState);
            await player.DisconnectAsync().ConfigureAwait(false);
            return;
        }

        var candidate = player.Account?.Characters.FirstOrDefault(c => c.Name.Equals(characterName));
        if (candidate is null)
        {
            player.Logger.LogError("Could not select character because character not found: [{0}]", characterName);
            await player.DisconnectAsync().ConfigureAwait(false);
            return;
        }

        var now = DateTime.UtcNow;
        var banService = this._banService ?? BanManagerService.Default;
        var activeBan = banService.FindActiveCharacterBan(candidate.Name, now);
        var persistedBanned = candidate.CharacterStatus == CharacterStatus.Banned && !banService.HasExpiredCharacterBanOnly(candidate.Name, now);
        if (activeBan is not null || persistedBanned)
        {
            var effective = activeBan ?? new BanRecord(
                string.Empty,
                BanTargetKind.CharacterBan,
                candidate.Name,
                candidate.Name,
                "Character is currently banned.",
                "Nhân vật hiện đang bị khóa.",
                now,
                null,
                "system",
                true);
            var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            await player.InvokeViewPlugInAsync<IHnmoBanNoticeViewPlugIn>(v => v.SendBanNoticeAsync(BanManagerService.BuildNoticeJson(effective))).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(BanManagerService.FormatPlayerNotice(effective, isVi), HNMO.Interfaces.MessageType.BlueNormal)).ConfigureAwait(false);
            await player.DisconnectAsync().ConfigureAwait(false);
            return;
        }

        await player.SetSelectedCharacterAsync(candidate).ConfigureAwait(false);
    }
}
