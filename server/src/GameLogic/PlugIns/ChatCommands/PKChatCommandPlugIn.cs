// <copyright file="PKChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.GameLogic.Views.Character;
using HNMO.Interfaces;
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
    /// Gets the outlaw points total a game master's <c>/pk</c> gives for a stage in the active season.
    /// </summary>
    /// <param name="state">The stage asked for.</param>
    /// <returns>The points total.</returns>
    internal static int GetPoints(HeroState state) => GetPoints(state, HnmoSeasonInstance.IsSeasonNine);

    /// <summary>
    /// Gets the outlaw points total a game master's <c>/pk</c> gives for a stage in the specified season:
    /// full 3h/stage totals for Season 6.3 (<c>10,800 / 21,600 / 32,400</c>), or interior stage totals for
    /// Season 9 (<c>21,600 / 64,800 / 90,000</c>).
    /// </summary>
    /// <param name="state">The stage asked for.</param>
    /// <param name="isSeasonNine"><c>true</c> for Season 9 rules; <c>false</c> for Season 6.3 / Arena rules.</param>
    /// <returns>The points total.</returns>
    internal static int GetPoints(HeroState state, bool isSeasonNine)
        => isSeasonNine
            ? state switch
            {
                HeroState.PlayerKillWarning => OutlawPoints.WarningPoints,
                HeroState.PlayerKiller1stStage => OutlawPoints.SeasonNineFirstStageEntryPoints,
                HeroState.PlayerKiller2ndStage => OutlawPoints.PlayerKiller2ndStagePoints + OutlawPoints.FurtherKillPoints,
                _ => 0,
            }
            : state switch
            {
                HeroState.PlayerKillWarning => OutlawPoints.FormerStageSeconds,
                HeroState.PlayerKiller1stStage => 2 * OutlawPoints.FormerStageSeconds,
                HeroState.PlayerKiller2ndStage => 3 * OutlawPoints.FormerStageSeconds,
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