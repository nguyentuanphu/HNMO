// <copyright file="HnmoSkillService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Administration;
using HNMO.Interfaces;
using HNMO.Persistence;

/// <summary>
/// Shows, grants and removes the HNMO skills (700 to 727) of characters, e.g. the fourth skill of the class without
/// its trial quest.
/// </summary>
/// <remarks>
/// When the account is in the game, the game server which holds it changes the live character
/// (<see cref="HnmoSkillsAdminOperationPlugIn"/>), so the skill shows up in the game at once. Otherwise the stored
/// character is changed while the account is reserved at the login server, so it can't log in meanwhile.
/// </remarks>
public class HnmoSkillService
{
    private readonly IPersistenceContextProvider _persistenceContextProvider;
    private readonly IDataSource<GameConfiguration> _gameConfigurationSource;
    private readonly ILoginServer _loginServer;
    private readonly IServerProvider _serverProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoSkillService"/> class.
    /// </summary>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    /// <param name="gameConfigurationSource">The game configuration source.</param>
    /// <param name="loginServer">The login server, which knows the accounts which are online.</param>
    /// <param name="serverProvider">The server provider, which holds the game servers.</param>
    public HnmoSkillService(IPersistenceContextProvider persistenceContextProvider, IDataSource<GameConfiguration> gameConfigurationSource, ILoginServer loginServer, IServerProvider serverProvider)
    {
        this._persistenceContextProvider = persistenceContextProvider;
        this._gameConfigurationSource = gameConfigurationSource;
        this._loginServer = loginServer;
        this._serverProvider = serverProvider;
    }

    /// <summary>
    /// Gets the characters of an account with the HNMO skills of their classes.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The characters; <c>null</c>, if the account doesn't exist.</returns>
    public async ValueTask<IReadOnlyList<HnmoCharacterSkills>?> GetCharactersAsync(string loginName, CancellationToken cancellationToken = default)
    {
        var configuration = await this._gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = this._persistenceContextProvider.CreateNewPlayerContext(configuration);
        if (await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return null;
        }

        var online = await this.GetServerIdAsync(account.LoginName).ConfigureAwait(false) is not null;
        return account.Characters
            .OrderBy(c => c.CharacterSlot)
            .Select(c => Describe(configuration, c, online))
            .ToList();
    }

    /// <summary>
    /// Grants or removes an HNMO skill of a character.
    /// </summary>
    /// <param name="characterName">The name of the character.</param>
    /// <param name="skillNumber">The number of the skill (700 to 727).</param>
    /// <param name="learn"><c>true</c> to grant the skill, <c>false</c> to remove it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async ValueTask<HnmoSkillChangeResult> SetSkillAsync(string characterName, int skillNumber, bool learn, CancellationToken cancellationToken = default)
    {
        if (!HnmoSkills.IsHnmoSkill(skillNumber))
        {
            return new(HnmoSkillChangeStatus.SkillNotOfClass, false);
        }

        var configuration = await this._gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        Account account;
        Character character;
        using (var context = this._persistenceContextProvider.CreateNewPlayerContext(configuration))
        {
            if (await context.GetAccountByCharacterNameAsync(characterName, cancellationToken).ConfigureAwait(false) is not { } found
                || found.Characters.FirstOrDefault(c => string.Equals(c.Name, characterName, StringComparison.OrdinalIgnoreCase)) is not { } foundCharacter)
            {
                return new(HnmoSkillChangeStatus.CharacterNotFound, false);
            }

            account = found;
            character = foundCharacter;
        }

        if (character.CharacterClass is not { } characterClass
            || HnmoSkills.GetSkillsOf(configuration, characterClass).All(s => s.Number != skillNumber))
        {
            return new(HnmoSkillChangeStatus.SkillNotOfClass, false);
        }

        if (await this.GetServerIdAsync(account.LoginName).ConfigureAwait(false) is { } serverId)
        {
            return await this.SetSkillOfLiveCharacterAsync(serverId, character, skillNumber, learn, cancellationToken).ConfigureAwait(false);
        }

        return await this.SetSkillOfStoredCharacterAsync(configuration, account.LoginName, character.Name, skillNumber, learn, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Grants or removes the skill on a stored character.
    /// </summary>
    /// <param name="context">The persistence context of the character.</param>
    /// <param name="character">The character.</param>
    /// <param name="skill">The skill.</param>
    /// <param name="learn"><c>true</c> to grant the skill, <c>false</c> to remove it.</param>
    /// <returns><c>true</c>, if the character changed.</returns>
    internal static async ValueTask<bool> ApplyToStoredCharacterAsync(IContext context, Character character, Skill skill, bool learn)
    {
        var entries = character.LearnedSkills.Where(s => s.Skill?.Number == skill.Number).ToList();
        if (learn)
        {
            if (entries.Count > 0)
            {
                return false;
            }

            var entry = context.CreateNew<SkillEntry>();
            entry.Skill = skill;
            entry.Level = 0;
            character.LearnedSkills.Add(entry);
            return true;
        }

        foreach (var entry in entries)
        {
            character.LearnedSkills.Remove(entry);
            await context.DeleteAsync(entry).ConfigureAwait(false);
        }

        return entries.Count > 0;
    }

    private static HnmoCharacterSkills Describe(GameConfiguration configuration, Character character, bool online)
    {
        var skills = character.CharacterClass is { } characterClass
            ? HnmoSkills.GetSkillsOf(configuration, characterClass)
                .Select(s => new HnmoSkillState(s.Number, HnmoSkills.GetLetter(s.Number), s.Name.ToString() ?? string.Empty, (int)(s.Requirements.FirstOrDefault(r => r.Attribute?.Id == GameLogic.Attributes.Stats.Level.Id)?.MinimumValue ?? 0), HnmoSkills.HasLearned(character, s.Number)))
                .ToList()
            : [];
        return new HnmoCharacterSkills(character.Name, character.CharacterClass?.Name.ToString() ?? string.Empty, (int)(character.Attributes.FirstOrDefault(a => a.Definition?.Id == GameLogic.Attributes.Stats.Level.Id)?.Value ?? 0), online, skills);
    }

    private static HnmoSkillChangeStatus ToStatus(bool changed, bool learn)
        => !changed ? HnmoSkillChangeStatus.Unchanged : learn ? HnmoSkillChangeStatus.Learned : HnmoSkillChangeStatus.Removed;

    private async ValueTask<byte?> GetServerIdAsync(string loginName)
    {
        var snapshot = await this._loginServer.GetSnapshotAsync().ConfigureAwait(false);
        var entry = snapshot.FirstOrDefault(e => string.Equals(e.Key, loginName, StringComparison.OrdinalIgnoreCase));
        return entry.Key is null ? null : entry.Value;
    }

    private async ValueTask<HnmoSkillChangeResult> SetSkillOfLiveCharacterAsync(byte serverId, Character character, int skillNumber, bool learn, CancellationToken cancellationToken)
    {
        if (this._serverProvider.Servers.OfType<IGameServer>().FirstOrDefault(s => s.Id == serverId) is not { ServerState: ServerState.Started } server
            || server is not IGameServerAdministration administration)
        {
            return new(HnmoSkillChangeStatus.ServerUnavailable, true);
        }

        var plugInId = typeof(HnmoSkillsAdminOperationPlugIn).GUID;
        var arguments = new JsonObject
        {
            ["characterName"] = character.Name,
            ["characterId"] = character.Id.ToString(),
            ["skillNumber"] = skillNumber,
            ["learn"] = learn,
        }.ToJsonString();
        try
        {
            var preview = await administration.PreviewAdminOperationAsync(plugInId, HnmoSkillsAdminOperationPlugIn.OperationName, arguments, cancellationToken).ConfigureAwait(false);
            var result = await administration.ExecuteAdminOperationAsync(plugInId, HnmoSkillsAdminOperationPlugIn.OperationName, arguments, preview, cancellationToken).ConfigureAwait(false);
            var changed = result["changed"]?.GetValue<bool>() ?? false;
            return new(ToStatus(changed, learn), true);
        }
        catch (ArgumentException ex)
        {
            // E.g. the account is logged in, but the character is not in the game, or the operation is switched off.
            return new(HnmoSkillChangeStatus.NotInGame, true, ex.Message);
        }
    }

    private async ValueTask<HnmoSkillChangeResult> SetSkillOfStoredCharacterAsync(GameConfiguration configuration, string loginName, string characterName, int skillNumber, bool learn, CancellationToken cancellationToken)
    {
        var reservationId = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        if (!await this._loginServer.TryReserveAccountAsync(loginName, reservationId).ConfigureAwait(false))
        {
            return new(HnmoSkillChangeStatus.AccountBusy, false);
        }

        try
        {
            // Loaded again after the reservation: the game server may have saved the character just before.
            using var context = this._persistenceContextProvider.CreateNewPlayerContext(configuration);
            if (await context.GetAccountByCharacterNameAsync(characterName, cancellationToken).ConfigureAwait(false) is not { } account
                || account.Characters.FirstOrDefault(c => string.Equals(c.Name, characterName, StringComparison.OrdinalIgnoreCase)) is not { CharacterClass: { } characterClass } character
                || HnmoSkills.GetSkillsOf(configuration, characterClass).FirstOrDefault(s => s.Number == skillNumber) is not { } skill)
            {
                return new(HnmoSkillChangeStatus.CharacterNotFound, false);
            }

            var changed = await ApplyToStoredCharacterAsync(context, character, skill, learn).ConfigureAwait(false);
            if (changed && !await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The character could not be saved.");
            }

            return new(ToStatus(changed, learn), false);
        }
        finally
        {
            await this._loginServer.ReleaseAccountReservationAsync(loginName, reservationId).ConfigureAwait(false);
        }
    }
}
