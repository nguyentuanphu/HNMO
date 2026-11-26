// <copyright file="SeasonNineMasterCapTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.PlugIns;

/// <summary>
/// The master level cap of the Season 9 systems plug-in: it raises the realm's cap at run time with experience tables
/// long enough for it, gives the realm's own cap back when it is turned off, and never runs outside a Season 9 realm.
/// </summary>
[TestFixture]
public class SeasonNineMasterCapTests
{
    /// <summary>
    /// With the cap raised to 330 at run time, a character at master level 250 gains master experience (the tables
    /// were made for 200 before: the next level's entry was outside them).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARaisedCapGivesMasterExperiencePastTheOldCapAsync()
    {
        var (gameContext, plugIn) = Create(enabled: true, cap: 330);
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

        Assert.That(gameContext.Configuration.MaximumMasterLevel, Is.EqualTo((short)330));
        Assert.That(gameContext.MasterExperienceTable, Has.Length.EqualTo(332));

        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Attributes![Stats.MasterLevel] = 250;
        player.SelectedCharacter!.MasterExperience = gameContext.MasterExperienceTable[250];
        await player.AddMasterExperienceAsync(1_000, null).ConfigureAwait(false);

        Assert.That(player.SelectedCharacter.MasterExperience, Is.EqualTo(gameContext.MasterExperienceTable[250] + 1_000));
    }

    /// <summary>
    /// Turned off, the realm's own cap comes back with tables for it; a character above it keeps its master level and
    /// gains no master experience there.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TurnedOffTheRealmCapComesBackAndCharactersKeepTheirLevelAsync()
    {
        var (gameContext, plugIn) = Create(enabled: true, cap: 330);
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Attributes![Stats.MasterLevel] = 250;
        player.SelectedCharacter!.MasterExperience = gameContext.MasterExperienceTable[250];
        var experience = player.SelectedCharacter.MasterExperience;

        plugIn.Configuration!.Enabled = false;
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        await player.AddMasterExperienceAsync(1_000, null).ConfigureAwait(false);

        Assert.That(gameContext.Configuration.MaximumMasterLevel, Is.EqualTo((short)200));
        Assert.That(gameContext.MasterExperienceTable, Has.Length.EqualTo(202));
        Assert.That(player.Attributes[Stats.MasterLevel], Is.EqualTo(250));
        Assert.That(player.SelectedCharacter.MasterExperience, Is.EqualTo(experience));
    }

    /// <summary>A custom cap (here 280) is used as it is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACustomCapIsAppliedAsync()
    {
        var (gameContext, plugIn) = Create(enabled: true, cap: 280);
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

        Assert.That(gameContext.Configuration.MaximumMasterLevel, Is.EqualTo((short)280));
        Assert.That(gameContext.MasterExperienceTable, Has.Length.EqualTo(282));
    }

    /// <summary>
    /// Outside a Season 9 realm (Season 6.3, Arena) nothing changes, even with the systems switched on and the stored
    /// "Season 9 only" flag off.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutsideASeasonNineRealmNothingChangesAsync()
    {
        var (gameContext, plugIn) = Create(enabled: true, cap: 330, seasonNine: false);
        plugIn.Configuration!.SeasonNineOnly = false;
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

        Assert.That(plugIn.IsActive(), Is.False);
        Assert.That(gameContext.Configuration.MaximumMasterLevel, Is.EqualTo((short)200));
        Assert.That(gameContext.MasterExperienceTable, Has.Length.EqualTo(202));
    }

    private static (GameContext GameContext, SeasonNineSystemsPlugIn PlugIn) Create(bool enabled, short cap, bool seasonNine = true)
    {
        var configuration = new PlugInConfiguration
        {
            TypeId = typeof(SeasonNineSystemsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new SeasonNineSystemsConfiguration { Enabled = enabled, MasterLevelMaxCap = cap }),
        };
        var gameContext = (GameContext)GameContextTestHelper.CreateGameContext([configuration], maximumLevel: 400);
        gameContext.Configuration.MaximumMasterLevel = 200;
        gameContext.RefreshExperienceTables();
        var plugIn = SeasonNineSystemsPlugIn.Get(gameContext)!;
        plugIn.IsSeasonNineRealm = () => seasonNine;
        return (gameContext, plugIn);
    }
}
