// <copyright file="WorldsOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Text.Json.Nodes;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.Worlds;

/// <summary>
/// Audited Admin Panel operations for managing multi-world databases, templates, Sydney opening dates,
/// and one-off character transfers (ROADMAP 15.7).
/// </summary>
/// <param name="worldRegistry">Shared world registry.</param>
/// <param name="serverProvider">Running server provider.</param>
/// <param name="persistenceProvider">Default persistence context provider.</param>
/// <param name="executor">Authorized audit executor.</param>
public sealed class WorldsOperations(
    WorldRegistryService worldRegistry,
    IServerProvider serverProvider,
    IPersistenceContextProvider persistenceProvider,
    AdminOperationExecutor executor)
{
    /// <summary>Gets all configured worlds synchronized with active servers.</summary>
    /// <returns>Worlds list.</returns>
    [NotExposedToAgents("World database topology management uses the interactive Admin Panel /worlds page.")]
    public IReadOnlyList<WorldDefinition> GetWorlds()
    {
        var liveServers = serverProvider.Servers.OfType<IGameServer>().ToArray();
        if (liveServers.Length > 0 && worldRegistry.GetWorldContextProvider("main") is null)
        {
            worldRegistry.RegisterWorldContextProvider("main", persistenceProvider);
        }

        worldRegistry.EnsureDefaultWorld(liveServers.Select(s => s.Id));
        var worlds = worldRegistry.GetWorlds();
        var liveLookup = liveServers.ToDictionary(s => s.Id);
        foreach (var world in worlds)
        {
            var matched = world.ServerIds
                .Where(id => liveLookup.ContainsKey(id))
                .Select(id => liveLookup[id])
                .ToArray();
            if (matched.Length > 0)
            {
                world.CachedOnlinePlayers = matched.Sum(s => s.CurrentConnections);
            }
        }

        return worlds;
    }

    /// <summary>Gets recorded one-off character transfers.</summary>
    /// <returns>Transfer audit records.</returns>
    [NotExposedToAgents("Transfer audit history is inspected directly on the Admin Panel /worlds page.")]
    public IReadOnlyList<WorldTransferRecord> GetTransfers() => worldRegistry.GetTransfers();

    /// <summary>Creates or updates a world's database/rates/opening settings and applies rates to live matching servers.</summary>
    /// <param name="world">World definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audited operation result.</returns>
    [NotExposedToAgents("Modifying world database endpoints and opening dates requires interactive Admin Panel review.")]
    public ValueTask<JsonObject> SaveWorldAsync(WorldDefinition world, CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(
            AdminRoles.Administrator,
            "world_upsert",
            new { world.WorldId, world.NameEn, world.DatabaseName, world.ExperienceMultiplier, world.DropMultiplier, world.ZenMultiplier, world.TransferEnabled },
            async _ =>
            {
                var saved = worldRegistry.UpsertWorld(world);
                await this.ApplyLiveRatesIfHostedAsync(saved, cancellationToken).ConfigureAwait(false);
                return AdminReadResult.From(new { saved.WorldId, saved.DatabaseName, saved.IsRunning, summary = $"Saved world '{saved.WorldId}'." });
            },
            cancellationToken);

    /// <summary>Starts or stops a world and its assigned game servers.</summary>
    /// <param name="worldId">World identifier.</param>
    /// <param name="start"><c>true</c> to start, <c>false</c> to stop.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audited operation result.</returns>
    [NotExposedToAgents("Starting or stopping an entire game world requires interactive Admin Panel confirmation.")]
    public ValueTask<JsonObject> SetWorldRunningAsync(string worldId, bool start, CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(
            AdminRoles.Operator,
            "world_toggle_run",
            new { worldId, start },
            async _ =>
            {
                var updated = await worldRegistry.SetWorldRunningAsync(worldId, start, serverProvider).ConfigureAwait(false);
                return AdminReadResult.From(new { updated.WorldId, updated.IsRunning, summary = $"World '{updated.WorldId}' running={updated.IsRunning}." });
            },
            cancellationToken);

    /// <summary>Creates a new world cloned from an existing template world.</summary>
    /// <param name="request">Template creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created world summary.</returns>
    [NotExposedToAgents("Provisioning a new world database from a template requires interactive Admin Panel confirmation.")]
    public ValueTask<JsonObject> CreateFromTemplateAsync(WorldTemplateCreateRequest request, CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(
            AdminRoles.Administrator,
            "world_create_template",
            new { request.SourceWorldId, request.NewWorldId, request.DatabaseName, request.Mode, request.OpeningSydneyText, request.TransferEnabled },
            async _ =>
            {
                if (worldRegistry.GetWorldContextProvider(request.SourceWorldId) is null && string.Equals(request.SourceWorldId, "main", StringComparison.OrdinalIgnoreCase))
                {
                    worldRegistry.RegisterWorldContextProvider("main", persistenceProvider);
                }

                var created = await worldRegistry.CreateWorldFromTemplateAsync(request).ConfigureAwait(false);
                return AdminReadResult.From(new { created.WorldId, created.DatabaseName, created.TemplateMode, openingSydney = created.FormatOpeningSydney() });
            },
            cancellationToken);

    /// <summary>Performs a one-off character transfer between two isolated world databases.</summary>
    /// <param name="sourceWorldId">Source world ID.</param>
    /// <param name="targetWorldId">Destination world ID.</param>
    /// <param name="accountLogin">Account login name.</param>
    /// <param name="characterName">Character name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transfer audit summary.</returns>
    [NotExposedToAgents("One-off cross-world character transfers modify separate world databases and require interactive confirmation.")]
    public ValueTask<JsonObject> TransferCharacterOnceAsync(
        string sourceWorldId,
        string targetWorldId,
        string accountLogin,
        string characterName,
        CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(
            AdminRoles.Administrator,
            "world_transfer_character",
            new { sourceWorldId, targetWorldId, accountLogin, characterName },
            async _ =>
            {
                var record = await worldRegistry.TransferCharacterOnceAsync(sourceWorldId, targetWorldId, accountLogin, characterName, "admin", cancellationToken).ConfigureAwait(false);
                return AdminReadResult.From(new { record.TransferId, record.SourceWorldId, record.TargetWorldId, record.CharacterName, record.TransferredAtUtc });
            },
            cancellationToken);

    private async ValueTask ApplyLiveRatesIfHostedAsync(WorldDefinition world, CancellationToken cancellationToken)
    {
        var hasHostedServer = serverProvider.Servers.OfType<IGameServer>().Any(s => world.ServerIds.Contains(s.Id));
        if (!hasHostedServer)
        {
            return;
        }

        using var context = persistenceProvider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>(cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        if (config is null)
        {
            return;
        }

        config.ExperienceRate = (float)Math.Clamp(world.ExperienceMultiplier, 0.1, 10000.0);
        config.MasterExperienceRate = (float)Math.Clamp(world.MasterExperienceMultiplier, 0.1, 10000.0);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
