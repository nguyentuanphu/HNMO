// <copyright file="EventRewardsOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using PersistenceIdentifiable = HNMO.Persistence.IIdentifiable;

/// <summary>
/// Admin operations for inspecting and editing mini-game event rewards per event level (ROADMAP 14.7)
/// and listing known Chaos Machine recipes for <see cref="HNMO.GameLogic.PlugIns.GameplayRules.ChaosRatesPlugIn"/>.
/// </summary>
/// <param name="persistence">Persistence context provider.</param>
/// <param name="configurations">Game configuration data source.</param>
/// <param name="executor">Authorization and audit executor.</param>
public sealed class EventRewardsOperations(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    AdminOperationExecutor executor)
{
    /// <summary>Loads all known Chaos Machine recipes `(Number, Name)` from NPC craftings.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recipe number/name pairs sorted by number.</returns>
    [NotExposedToAgents("Admin panel Chaos Machine rate picker only.")]
    public async ValueTask<IReadOnlyList<(byte Number, string Name)>> LoadChaosRecipesAsync(CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var owner = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return owner.Monsters
            .SelectMany(m => m.ItemCraftings)
            .GroupBy(c => c.Number)
            .Select(g => (Number: g.Key, Name: g.First().Name.ValueInNeutralLanguage ?? $"Recipe #{g.Key}"))
            .OrderBy(r => r.Number)
            .ToList();
    }

    /// <summary>Loads mini-game definitions and their configured rewards sorted by type and level.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Definitions with their reward rows and available drop item groups.</returns>
    [NotExposedToAgents("Admin panel event rewards editor UI only.")]
    public async ValueTask<(IReadOnlyList<(Guid Id, MiniGameType Type, byte Level, string Name)> Definitions, IReadOnlyList<MiniGameRewardEntryDto> Rewards, IReadOnlyList<(Guid Id, string Label)> DropGroups)> LoadRewardsAsync(
        CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var owner = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var defs = owner.MiniGameDefinitions
            .OrderBy(d => d.Type)
            .ThenBy(d => d.GameLevel)
            .Select(d => (
                Id: (d as PersistenceIdentifiable)?.Id ?? Guid.Empty,
                d.Type,
                d.GameLevel,
                Name: d.Name.ValueInNeutralLanguage ?? $"{d.Type} #{d.GameLevel}"))
            .ToList();

        var rows = new List<MiniGameRewardEntryDto>();
        foreach (var def in owner.MiniGameDefinitions.OrderBy(d => d.Type).ThenBy(d => d.GameLevel))
        {
            var defId = (def as PersistenceIdentifiable)?.Id ?? Guid.Empty;
            var defName = def.Name.ValueInNeutralLanguage ?? $"{def.Type} #{def.GameLevel}";
            foreach (var r in def.Rewards)
            {
                rows.Add(new MiniGameRewardEntryDto
                {
                    MiniGameDefinitionId = defId,
                    Type = def.Type,
                    GameLevel = def.GameLevel,
                    EventName = defName,
                    RewardId = (r as PersistenceIdentifiable)?.Id ?? Guid.Empty,
                    Rank = r.Rank,
                    RewardType = r.RewardType,
                    RewardAmount = r.RewardAmount,
                    RequiredSuccess = r.RequiredSuccess,
                    ItemRewardGroupId = (r.ItemReward as PersistenceIdentifiable)?.Id,
                    ItemRewardDescription = r.ItemReward?.Description.Value ?? string.Empty,
                });
            }
        }

        var groups = owner.DropItemGroups
            .Where(g => g is PersistenceIdentifiable id && id.Id != Guid.Empty)
            .Select(g => (Id: ((PersistenceIdentifiable)g).Id, Label: string.IsNullOrWhiteSpace(g.Description.Value) ? ((PersistenceIdentifiable)g).Id.ToString("N") : g.Description.Value))
            .OrderBy(g => g.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (defs, rows, groups);
    }

    /// <summary>Saves edited reward rows for a single <see cref="MiniGameDefinition"/> level and writes an audit entry.</summary>
    /// <param name="miniGameDefinitionId">Target mini-game definition ID.</param>
    /// <param name="updatedRows">Replacement reward rows for that mini-game level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of saved rewards.</returns>
    [NotExposedToAgents("Admin panel event rewards editor UI only.")]
    public ValueTask<int> SaveRewardsForDefinitionAsync(
        Guid miniGameDefinitionId,
        IReadOnlyList<MiniGameRewardEntryDto> updatedRows,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            AdminRoles.Administrator,
            "event_rewards_save",
            new { miniGameDefinitionId, count = updatedRows.Count },
            async _ =>
            {
                using var context = persistence.CreateNewConfigurationContext();
                var configs = (await context.GetAsync<GameConfiguration>(cancellationToken).ConfigureAwait(false)).ToList();
                var owner = configs.FirstOrDefault()
                    ?? await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                var def = owner.MiniGameDefinitions.FirstOrDefault(d => d is PersistenceIdentifiable id && id.Id == miniGameDefinitionId)
                    ?? throw new InvalidOperationException($"MiniGameDefinition {miniGameDefinitionId} was not found.");

                var existingToDelete = def.Rewards.ToList();
                foreach (var old in existingToDelete)
                {
                    def.Rewards.Remove(old);
                    await context.DeleteAsync(old).ConfigureAwait(false);
                }

                foreach (var dto in updatedRows)
                {
                    var reward = context.CreateNew<MiniGameReward>();
                    reward.Rank = dto.Rank;
                    reward.RewardType = dto.RewardType;
                    reward.RewardAmount = dto.RewardAmount;
                    reward.RequiredSuccess = dto.RequiredSuccess;
                    if (dto.ItemRewardGroupId is { } groupId && groupId != Guid.Empty)
                    {
                        reward.ItemReward = owner.DropItemGroups.FirstOrDefault(g => g is PersistenceIdentifiable gid && gid.Id == groupId);
                    }

                    def.Rewards.Add(reward);
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // Keep cached owner synchronized in memory if owner instance differs from configuration context.
                var cachedOwner = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                var cachedDef = cachedOwner.MiniGameDefinitions.FirstOrDefault(d => d is PersistenceIdentifiable id && id.Id == miniGameDefinitionId);
                if (cachedDef is not null && !ReferenceEquals(cachedDef, def))
                {
                    cachedDef.Rewards.Clear();
                    foreach (var r in def.Rewards)
                    {
                        cachedDef.Rewards.Add(r);
                    }
                }

                return def.Rewards.Count;
            },
            cancellationToken);
}
