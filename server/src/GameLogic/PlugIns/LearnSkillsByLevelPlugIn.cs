// <copyright file="LearnSkillsByLevelPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Offline;
using HNMO.PlugIns;

/// <summary>
/// Teaches the configured skills (by default the A, B and C skills of the HNMO skills) when a character reaches
/// their level, and when it enters the game already past it, e.g. after the skills were added to an existing server.
/// A character learns only the skills its class qualifies for, and gets a message for each.
/// </summary>
/// <remarks>
/// Server-side bots are left out: they learn the skills whose requirements they meet by the <c>BotSkillProgressionPlugIn</c>.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.LearnSkillsByLevelPlugIn_Name), Description = nameof(PlugInResources.LearnSkillsByLevelPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C4E2A9B1-7D35-4F60-8A1C-5B93E07D2F16")]
public class LearnSkillsByLevelPlugIn : ICharacterLevelUpPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<LearnSkillsByLevelPlugInConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public LearnSkillsByLevelPlugInConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player)
    {
        if (player.Account?.IsBot == true)
        {
            return;
        }

        if (player is OfflinePlayer offlinePlayer)
        {
            // Offline leveling: the combat loop may use the skill list right now, its tick serializes both.
            offlinePlayer.PendingBotActions.Enqueue(() => this.LearnSkillsAsync(player));
        }
        else
        {
            _ = this.LearnSkillsSafelyAsync(player);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection
            || currentState != PlayerState.EnteredWorld
            || player.Account?.IsBot == true)
        {
            return;
        }

        await this.LearnSkillsAsync(player).ConfigureAwait(false);
    }

    /// <summary>
    /// Teaches the player the configured skills of its class whose level it has reached, and which it didn't learn yet.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask LearnSkillsAsync(Player player)
    {
        if (player.SelectedCharacter?.CharacterClass is not { } characterClass
            || player.SkillList is not { } skillList)
        {
            return;
        }

        var configuration = this.Configuration ??= LearnSkillsByLevelPlugInConfiguration.CreateDefault();
        var level = player.Level;
        foreach (var entry in configuration.Skills.Where(e => e.MinimumLevel <= level))
        {
            if (skillList.ContainsSkill(unchecked((ushort)entry.SkillNumber))
                || player.GameContext.Configuration.Skills.FirstOrDefault(s => s.Number == entry.SkillNumber) is not { } skill
                || !skill.QualifiedCharacters.Contains(characterClass))
            {
                continue;
            }

            await skillList.AddLearnedSkillAsync(skill).ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SkillLearned), skill.Name.GetTranslation(player.Culture)).ConfigureAwait(false);
            player.Logger.LogDebug("Character {Character} learned the skill {Skill} at level {Level}.", player.SelectedCharacter.Name, skill.Number, level);
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return LearnSkillsByLevelPlugInConfiguration.CreateDefault();
    }

    private async Task LearnSkillsSafelyAsync(Player player)
    {
        try
        {
            await this.LearnSkillsAsync(player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Failed to teach the skills of the level to {Player}.", player);
        }
    }
}
