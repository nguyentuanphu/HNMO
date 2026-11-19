// <copyright file="WorldsOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Vip;
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
/// <param name="loginServer">Reserves the account while one of its characters moves; refuses when it is online.</param>
public sealed class WorldsOperations(
    WorldRegistryService worldRegistry,
    IServerProvider serverProvider,
    IPersistenceContextProvider persistenceProvider,
    AdminOperationExecutor executor,
    ILoginServer? loginServer = null)
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
                // The account must be offline, and stays unable to log in while its character moves: a character
                // in play would be copied with stale progress, and a save after the move could bring it back.
                var login = accountLogin.Trim();
                var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
                if (loginServer is not null && !await loginServer.TryReserveAccountAsync(login, reservation).ConfigureAwait(false))
                {
                    throw new AdminOperationException("The account is logged in. Log it out before moving one of its characters.", AdminOperationErrorKind.Conflict);
                }

                try
                {
                    var record = await worldRegistry.TransferCharacterOnceAsync(sourceWorldId, targetWorldId, accountLogin, characterName, "admin", cancellationToken).ConfigureAwait(false);
                    return AdminReadResult.From(new { record.TransferId, record.SourceWorldId, record.TargetWorldId, record.CharacterName, record.TransferredAtUtc });
                }
                finally
                {
                    if (loginServer is not null)
                    {
                        await loginServer.ReleaseAccountReservationAsync(login, reservation).ConfigureAwait(false);
                    }
                }
            },
            cancellationToken);

    /// <summary>
    /// A world's rates are the rates of its own game servers (the same values the Servers &amp; Rates page edits):
    /// each server multiplies the game's base experience rate, and has its own drop and Zen rate. The game's base
    /// rates aren't touched, so saving one world never changes another world's rates.
    /// </summary>
    private async ValueTask ApplyLiveRatesIfHostedAsync(WorldDefinition world, CancellationToken cancellationToken)
    {
        if (world.ServerIds.Count == 0)
        {
            return;
        }

        using var context = persistenceProvider.CreateNewContext();
        var definitions = await context.GetAsync<GameServerDefinition>(cancellationToken).ConfigureAwait(false);
        var own = definitions.Where(d => world.ServerIds.Contains(d.ServerID)).ToList();
        if (own.Count == 0)
        {
            return;
        }

        var experience = (float)Math.Clamp(world.ExperienceMultiplier, 0.1, 1000.0);
        var drop = (float)Math.Clamp(world.DropMultiplier, 0.1, 50.0);
        var zen = (float)Math.Clamp(world.ZenMultiplier, 0.1, 50.0);
        foreach (var definition in own)
        {
            definition.ExperienceRate = experience;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var rates = GameServerRateStore.OpenDefault();
        foreach (var definition in own)
        {
            rates.SetRates(definition.ServerID, drop, zen);
        }
    }
}
