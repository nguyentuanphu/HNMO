// <copyright file="PKChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.GameLogic.Views.Character;
using HNMO.PlugIns;

/// <summary>
/// A chat command plugin which handles pk commands.
/// </summary>
[Guid("30B7EFF0-33EE-4136-BEB0-BE503B748DC6")]
[PlugIn]
[Display(Name = nameof(PlugInResources.PkChatCommandPlugIn_Name), Description = nameof(PlugInResources.PkChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(PkChatCommandArgs), CharacterStatus.GameMaster)]
public class PkChatCommandPlugIn : ChatCommandPlugInBase<PkChatCommandArgs>
{
    private const string Command = "/pk";
    private const int MinPkLevel = HeroState.PlayerKillWarning - HeroState.Normal;
    private const int MaxPkLevel = HeroState.PlayerKiller2ndStage - HeroState.Normal;

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <summary>
    /// Gets the outlaw points total a game master's <c>/pk</c> gives for a stage: 21,600 for the Warning stage, and for
    /// the killer stages their lowest total plus 21,600 (six hours), so the character stays in the stage asked for
    /// instead of falling to the stage below on the next tick (the total goes down every second).
    /// </summary>
    /// <param name="state">The stage asked for.</param>
    /// <returns>The points total.</returns>
    internal static int GetPoints(HeroState state)
        => state switch
        {
            HeroState.PlayerKillWarning => OutlawPoints.WarningPoints,
            > HeroState.PlayerKillWarning => OutlawPoints.GetMinimumPoints(state) + OutlawPoints.WarningPoints,
            _ => 0,
        };

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, PkChatCommandArgs arguments)
    {
        if (arguments.Level < MinPkLevel || arguments.Level > MaxPkLevel)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.PlayerKillLevelRangeError), MinPkLevel, MaxPkLevel).ConfigureAwait(false);
            return;
        }

        if (arguments.Count <= 0)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.PlayerKillCountMustBePositive)).ConfigureAwait(false);
            return;
        }

        var targetPlayer = await this.GetPlayerByCharacterNameAsync(gameMaster, arguments.CharacterName ?? string.Empty).ConfigureAwait(false);
        var character = targetPlayer?.SelectedCharacter;
        if (character is null)
        {
            // logged out in the mean time ...
            return;
        }

        character.State = HeroState.Normal + arguments.Level;
        character.StateRemainingSeconds = GetPoints(character.State);
        character.PlayerKillCount = arguments.Count;
        await targetPlayer!.ForEachWorldObserverAsync<IUpdateCharacterHeroStatePlugIn>(p => p.UpdateCharacterHeroStateAsync(targetPlayer!), true).ConfigureAwait(false);

        await gameMaster.ShowLocalizedBlueMessageAsync(
            nameof(PlayerMessage.PlayerKillStateChangeResult),
            this.Key,
            character.Name,
            character.State,
            character.PlayerKillCount,
            Math.Round(TimeSpan.FromSeconds(character!.StateRemainingSeconds).TotalMinutes)).ConfigureAwait(false);
    }
}