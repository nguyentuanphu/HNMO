// <copyright file="CreateCharacterAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Character;

using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.GameLogic.Views.Character;

/// <summary>
/// Action to create a new character in the character selection screen.
/// </summary>
public class CreateCharacterAction
{
    /// <summary>
    /// Tries to create a new character.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="characterName">Name of the character.</param>
    /// <param name="characterClassId">The character class identifier.</param>
    public async ValueTask CreateCharacterAsync(Player player, string characterName, int characterClassId)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        if (player.PlayerState.CurrentState != PlayerState.CharacterSelection)
        {
            player.Logger.LogError($"Account {player.Account!.LoginName} not in the right state, but {player.PlayerState.CurrentState}.");
            return;
        }

        var characterClass = player.GameContext.Configuration.CharacterClasses.FirstOrDefault(c => c.Number == characterClassId);
        if (characterClass is not null)
        {
            var character = await this.CreateCharacterAsync(player, characterName, characterClass).ConfigureAwait(false);
            if (character != null)
            {
                await player.InvokeViewPlugInAsync<IShowCreatedCharacterPlugIn>(p => p.ShowCreatedCharacterAsync(character)).ConfigureAwait(false);
                return;
            }
        }

        await player.InvokeViewPlugInAsync<IShowCharacterCreationFailedPlugIn>(p => p.ShowCharacterCreationFailedAsync()).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether the account may create a character of the class: the class needs no level, or it was unlocked
    /// for the account, or a character of the account has the level already (characters which reached it before the
    /// unlock existed never level up to it again).
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="characterClass">The character class.</param>
    /// <returns><see langword="true"/> if the class can be created.</returns>
    internal static bool IsClassAvailable(DataModel.Entities.Account account, CharacterClass characterClass)
    {
        if (characterClass.LevelRequirementByCreation <= 0
            || account.UnlockedCharacterClasses.Any(c => c.Number == characterClass.Number))
        {
            return true;
        }

        return account.Characters.Any(c => c.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.Level.Id)?.Value >= characterClass.LevelRequirementByCreation);
    }

    private async ValueTask<DataModel.Entities.Character?> CreateCharacterAsync(Player player, string name, CharacterClass characterClass)
    {
        var account = player.Account;
        if (account is null)
        {
            player.Logger.LogWarning("Account Object is null.");
            throw new ArgumentNullException(nameof(player));
        }

        player.Logger.LogDebug("Enter CreateCharacter: {0} {1} {2}", account.LoginName, name, characterClass);
        var isValidName = player.GameContext.Configuration.IsValidCharacterName(name);
        player.Logger.LogDebug("CreateCharacter: Character Name matches = {0}", isValidName);
        if (!isValidName)
        {
            return null;
        }

        if (!await BannedWordsPlugIn.IsCharacterNameAllowedAsync(player, name).ConfigureAwait(false))
        {
            return null;
        }

        var freeSlot = account.GetFreeCharacterSlot(player.GameContext.Configuration);
        if (freeSlot is null)
        {
            return null;
        }

        // The character starts on the home map, so it must be available in the game.
        if (!characterClass.CanGetCreated || !characterClass.IsActive || characterClass.HomeMap is not { IsActive: true })
        {
            return null;
        }

        // Magic Gladiator, Dark Lord and Rage Fighter need a character of the account at a level first (220, 250,
        // 150). The class is unlocked when a character reaches it (UnlockCharacterAtLevelBase), but the creation
        // itself didn't check it, so any account could create them from the start.
        if (!IsClassAvailable(account, characterClass))
        {
            player.Logger.LogInformation("Account {account} can't create {class} yet: it needs a character of level {level}.", account.LoginName, characterClass.Name, characterClass.LevelRequirementByCreation);
            await player.ShowBlueMessageAsync(
                $"{characterClass.Name.ValueInNeutralLanguage}: a character of level {characterClass.LevelRequirementByCreation} is needed first. / Cần một nhân vật cấp {characterClass.LevelRequirementByCreation} trước.").ConfigureAwait(false);
            return null;
        }

        var character = player.PersistenceContext.CreateNew<DataModel.Entities.Character>();
        character.Name = name;
        character.CharacterSlot = freeSlot.Value;
        player.PersistenceContext.InitializeNewCharacter(character, characterClass);
        account.Characters.Add(character);
        player.GameContext.PlugInManager.GetPlugInPoint<ICharacterCreatedPlugIn>()?.CharacterCreated(account, character, player.PersistenceContext, player.GameContext.Configuration, player.Logger);
        try
        {
            await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            account.Characters.Remove(character);
            player.PersistenceContext.Detach(character);
            player.Logger.LogError(ex, "Error when trying to create character '{0}'", name);
            var message = ex.InnerException?.Message ?? ex.Message;
            if (message.Contains("IX_Character_Name") || message.Contains("23505"))
            {
                await player.ShowLocalizedBlueMessageAsync(PlayerMessage.CharacterWithNameAlreadyExists).ConfigureAwait(false);
            }

            return null;
        }

        player.Logger.LogDebug("Creating Character Complete.");
        return character;
    }
}