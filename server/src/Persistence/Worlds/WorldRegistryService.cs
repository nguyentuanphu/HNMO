// <copyright file="WorldRegistryService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Manages multiple isolated game worlds, template provisioning, Sydney opening locks,
/// one-off character transfers across world databases, and grouped player-facing snapshots (ROADMAP 15.7).
/// </summary>
public sealed class WorldRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly SemaphoreSlim _asyncGate = new(1, 1);
    private readonly ConcurrentDictionary<string, IPersistenceContextProvider> _worldProviders = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _storagePath;
    private WorldRegistryState _state = new();

    /// <summary>Initializes a new instance of the <see cref="WorldRegistryService"/> class.</summary>
    /// <param name="storagePath">Optional JSON persistence path (<c>null</c> runs purely in memory for tests).</param>
    public WorldRegistryService(string? storagePath = null)
    {
        this._storagePath = storagePath;
        this.LoadFromDisk();
    }

    /// <summary>Registers a persistence context provider dedicated to <paramref name="worldId"/>.</summary>
    /// <param name="worldId">World slug identifier.</param>
    /// <param name="provider">Separate world database context provider.</param>
    public void RegisterWorldContextProvider(string worldId, IPersistenceContextProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);
        ArgumentNullException.ThrowIfNull(provider);
        this._worldProviders[worldId.Trim()] = provider;
    }

    /// <summary>Gets the registered persistence context provider for <paramref name="worldId"/>, or <c>null</c>.</summary>
    /// <param name="worldId">World slug identifier.</param>
    /// <returns>Provider if registered.</returns>
    public IPersistenceContextProvider? GetWorldContextProvider(string worldId)
    {
        return string.IsNullOrWhiteSpace(worldId)
            ? null
            : this._worldProviders.GetValueOrDefault(worldId.Trim());
    }

    /// <summary>Ensures at least one default world exists covering active servers.</summary>
    /// <param name="knownServerIds">Known game server IDs.</param>
    public void EnsureDefaultWorld(IEnumerable<int> knownServerIds)
    {
        ArgumentNullException.ThrowIfNull(knownServerIds);
        lock (this._sync)
        {
            if (this._state.Worlds.Count > 0)
            {
                return;
            }

            var ids = knownServerIds.Distinct().OrderBy(id => id).ToList();
            if (ids.Count == 0)
            {
                ids.Add(0);
            }

            this._state.Worlds.Add(new WorldDefinition
            {
                WorldId = "main",
                NameEn = "World 1 — Origin",
                NameVi = "Thế giới 1 — Khởi Nguyên",
                DatabaseName = "hnmo_world_1",
                ServerIds = ids,
                ExperienceMultiplier = 1.0,
                MasterExperienceMultiplier = 1.0,
                DropMultiplier = 1.0,
                ZenMultiplier = 1.0,
                EventPresetEn = "Standard schedule",
                EventPresetVi = "Lịch chuẩn",
                IsDefault = true,
                IsRunning = true,
                TransferEnabled = true,
            });
            this.SaveToDisk();
        }
    }

    /// <summary>Returns a deep snapshot of all configured worlds.</summary>
    /// <returns>Worlds list.</returns>
    public IReadOnlyList<WorldDefinition> GetWorlds()
    {
        lock (this._sync)
        {
            return this._state.Worlds.Select(CloneWorld).ToList();
        }
    }

    /// <summary>Returns all recorded one-off character transfers.</summary>
    /// <returns>Transfer records newest first.</returns>
    public IReadOnlyList<WorldTransferRecord> GetTransfers()
    {
        lock (this._sync)
        {
            return this._state.Transfers
                .OrderByDescending(t => t.TransferredAtUtc)
                .Select(CloneTransfer)
                .ToList();
        }
    }

    /// <summary>Creates or updates a world definition.</summary>
    /// <param name="world">World definition.</param>
    /// <returns>Saved copy.</returns>
    public WorldDefinition UpsertWorld(WorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (string.IsNullOrWhiteSpace(world.WorldId))
        {
            throw new ArgumentException("WorldId is required.", nameof(world));
        }

        lock (this._sync)
        {
            var id = world.WorldId.Trim();
            var idx = this._state.Worlds.FindIndex(w => string.Equals(w.WorldId, id, StringComparison.OrdinalIgnoreCase));
            var copy = CloneWorld(world);
            copy.WorldId = id;
            if (string.IsNullOrWhiteSpace(copy.NameEn))
            {
                copy.NameEn = id;
            }

            if (string.IsNullOrWhiteSpace(copy.NameVi))
            {
                copy.NameVi = copy.NameEn;
            }

            if (string.IsNullOrWhiteSpace(copy.DatabaseName))
            {
                copy.DatabaseName = $"hnmo_{id.Replace('-', '_')}";
            }

            if (copy.IsDefault)
            {
                foreach (var existing in this._state.Worlds)
                {
                    existing.IsDefault = false;
                }
            }

            if (idx >= 0)
            {
                this._state.Worlds[idx] = copy;
            }
            else
            {
                if (this._state.Worlds.Count == 0)
                {
                    copy.IsDefault = true;
                }

                this._state.Worlds.Add(copy);
            }

            this.SaveToDisk();
            return CloneWorld(copy);
        }
    }

    /// <summary>Starts or stops a world and any matching in-process <see cref="IGameServer"/> instances.</summary>
    /// <param name="worldId">Target world ID.</param>
    /// <param name="start"><c>true</c> to start, <c>false</c> to stop.</param>
    /// <param name="serverProvider">Optional live server provider.</param>
    /// <returns>Updated world copy.</returns>
    public async ValueTask<WorldDefinition> SetWorldRunningAsync(string worldId, bool start, IServerProvider? serverProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);
        WorldDefinition updated;
        lock (this._sync)
        {
            var world = this.FindWorldUnsafe(worldId) ?? throw new KeyNotFoundException($"World '{worldId}' not found.");
            world.IsRunning = start;
            updated = CloneWorld(world);
            this.SaveToDisk();
        }

        if (serverProvider is not null)
        {
            var targets = serverProvider.Servers
                .OfType<IGameServer>()
                .Where(s => updated.ServerIds.Contains(s.Id))
                .ToArray();
            foreach (var server in targets)
            {
                if (start && server.ServerState is not (ServerState.Started or ServerState.Starting))
                {
                    await server.StartAsync().ConfigureAwait(false);
                }
                else if (!start && server.ServerState is not (ServerState.Stopped or ServerState.Stopping))
                {
                    await server.ShutdownAsync().ConfigureAwait(false);
                }
            }
        }

        return updated;
    }

    /// <summary>Creates a new world from an existing template world, optionally copying characters.</summary>
    /// <param name="request">Template creation request.</param>
    /// <returns>Created world definition.</returns>
    public async ValueTask<WorldDefinition> CreateWorldFromTemplateAsync(WorldTemplateCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.NewWorldId))
        {
            throw new ArgumentException("NewWorldId is required.", nameof(request));
        }

        var newId = request.NewWorldId.Trim();
        var opensUtc = request.OpeningDateUtc ?? WorldDefinition.ParseSydneyToUtc(request.OpeningSydneyText);

        await this._asyncGate.WaitAsync().ConfigureAwait(false);
        try
        {
            WorldDefinition templateCopy;
            WorldDefinition created;
            lock (this._sync)
            {
                if (this._state.Worlds.Any(w => string.Equals(w.WorldId, newId, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"World '{newId}' already exists.");
                }

                var source = this.FindWorldUnsafe(request.SourceWorldId)
                    ?? throw new KeyNotFoundException($"Template world '{request.SourceWorldId}' not found.");
                templateCopy = CloneWorld(source);

                created = new WorldDefinition
                {
                    WorldId = newId,
                    NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? newId : request.NameEn.Trim(),
                    NameVi = string.IsNullOrWhiteSpace(request.NameVi) ? (string.IsNullOrWhiteSpace(request.NameEn) ? newId : request.NameEn.Trim()) : request.NameVi.Trim(),
                    DatabaseName = string.IsNullOrWhiteSpace(request.DatabaseName) ? $"hnmo_{newId.Replace('-', '_')}" : request.DatabaseName.Trim(),
                    DatabaseConnectionString = request.DatabaseConnectionString?.Trim() ?? string.Empty,
                    ServerIds = request.ServerIds.Count > 0 ? request.ServerIds.Distinct().OrderBy(id => id).ToList() : [],
                    ExperienceMultiplier = request.ExperienceMultiplier ?? templateCopy.ExperienceMultiplier,
                    MasterExperienceMultiplier = request.MasterExperienceMultiplier ?? templateCopy.MasterExperienceMultiplier,
                    DropMultiplier = request.DropMultiplier ?? templateCopy.DropMultiplier,
                    ZenMultiplier = request.ZenMultiplier ?? templateCopy.ZenMultiplier,
                    EventPresetEn = string.IsNullOrWhiteSpace(request.EventPresetEn) ? templateCopy.EventPresetEn : request.EventPresetEn.Trim(),
                    EventPresetVi = string.IsNullOrWhiteSpace(request.EventPresetVi) ? templateCopy.EventPresetVi : request.EventPresetVi.Trim(),
                    TemplateSourceWorldId = templateCopy.WorldId,
                    TemplateMode = request.Mode,
                    OpeningDateUtc = opensUtc,
                    TransferEnabled = request.TransferEnabled,
                    IsRunning = opensUtc is null || opensUtc <= DateTimeOffset.UtcNow,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                };
            }

            var sourceProvider = this.GetWorldContextProvider(templateCopy.WorldId);
            var targetProvider = this.GetWorldContextProvider(created.WorldId);
            if (sourceProvider is not null && targetProvider is not null)
            {
                await CopyConfigurationAndOptionalRosterAsync(sourceProvider, targetProvider, created).ConfigureAwait(false);
            }

            lock (this._sync)
            {
                this._state.Worlds.Add(created);
                this.SaveToDisk();
                return CloneWorld(created);
            }
        }
        finally
        {
            this._asyncGate.Release();
        }
    }

    /// <summary>
    /// Performs an atomic one-off character transfer from <paramref name="sourceWorldId"/> to <paramref name="targetWorldId"/>.
    /// Rejects duplicate transfers for a character that has already transferred once.
    /// </summary>
    /// <param name="sourceWorldId">Source world ID.</param>
    /// <param name="targetWorldId">Target world ID.</param>
    /// <param name="accountLogin">Account login name.</param>
    /// <param name="characterName">Character name.</param>
    /// <param name="operatorName">Authorizing admin operator name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transfer audit record.</returns>
    public async ValueTask<WorldTransferRecord> TransferCharacterOnceAsync(
        string sourceWorldId,
        string targetWorldId,
        string accountLogin,
        string characterName,
        string operatorName = "admin",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceWorldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetWorldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountLogin);
        ArgumentException.ThrowIfNullOrWhiteSpace(characterName);

        if (string.Equals(sourceWorldId.Trim(), targetWorldId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Source and target world must be different.");
        }

        await this._asyncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorldDefinition srcWorld;
            WorldDefinition dstWorld;
            lock (this._sync)
            {
                srcWorld = CloneWorld(this.FindWorldUnsafe(sourceWorldId) ?? throw new KeyNotFoundException($"Source world '{sourceWorldId}' not found."));
                dstWorld = CloneWorld(this.FindWorldUnsafe(targetWorldId) ?? throw new KeyNotFoundException($"Target world '{targetWorldId}' not found."));
                if (!dstWorld.TransferEnabled)
                {
                    throw new InvalidOperationException($"Target world '{dstWorld.WorldId}' does not allow transfers.");
                }

                if (this._state.Transfers.Any(t =>
                    string.Equals(t.AccountLogin, accountLogin.Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(t.CharacterName, characterName.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Character '{characterName}' has already used its one-off world transfer.");
                }
            }

            var sourceProvider = this.GetWorldContextProvider(srcWorld.WorldId)
                ?? throw new InvalidOperationException($"No database provider registered for source world '{srcWorld.WorldId}'.");
            var targetProvider = this.GetWorldContextProvider(dstWorld.WorldId)
                ?? throw new InvalidOperationException($"No database provider registered for target world '{dstWorld.WorldId}'.");

            using var srcContext = sourceProvider.CreateNewContext();
            using var dstContext = targetProvider.CreateNewContext();

            var srcAccounts = (await srcContext.GetAsync<Account>(cancellationToken).ConfigureAwait(false)).ToList();
            var srcAccount = srcAccounts.FirstOrDefault(a => string.Equals(a.LoginName, accountLogin.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Account '{accountLogin}' not found on world '{srcWorld.WorldId}'.");
            var srcCharacter = srcAccount.Characters.FirstOrDefault(c => string.Equals(c.Name, characterName.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Character '{characterName}' not found on account '{accountLogin}' in world '{srcWorld.WorldId}'.");

            lock (this._sync)
            {
                if (this._state.Transfers.Any(t => t.SourceCharacterId == srcCharacter.Id || t.TargetCharacterId == srcCharacter.Id))
                {
                    throw new InvalidOperationException($"Character '{characterName}' has already used its one-off world transfer.");
                }
            }

            var dstAccounts = (await dstContext.GetAsync<Account>(cancellationToken).ConfigureAwait(false)).ToList();
            if (dstAccounts.SelectMany(a => a.Characters).Any(c => string.Equals(c.Name, srcCharacter.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A character named '{srcCharacter.Name}' already exists on target world '{dstWorld.WorldId}'.");
            }

            var dstConfig = (await dstContext.GetAsync<GameConfiguration>(cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            var maxSlots = dstConfig?.MaximumCharactersPerAccount > 0 ? dstConfig.MaximumCharactersPerAccount : (byte)5;

            var dstAccount = dstAccounts.FirstOrDefault(a => string.Equals(a.LoginName, srcAccount.LoginName, StringComparison.OrdinalIgnoreCase));
            if (dstAccount is null)
            {
                dstAccount = dstContext.CreateNew<Account>();
                dstAccount.LoginName = srcAccount.LoginName;
                dstAccount.PasswordHash = srcAccount.PasswordHash;
                dstAccount.SecurityCode = srcAccount.SecurityCode;
                dstAccount.EMail = srcAccount.EMail;
                dstAccount.LanguageIsoCode = srcAccount.LanguageIsoCode;
                dstAccount.State = srcAccount.State;
            }
            else if (dstAccount.Characters.Count >= maxSlots)
            {
                throw new InvalidOperationException($"Account '{accountLogin}' has no free character slots on '{dstWorld.WorldId}'.");
            }

            var copiedChar = CloneCharacterToContext(srcCharacter, dstAccount, dstContext, dstConfig);
            var savedTarget = await dstContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (!savedTarget)
            {
                throw new IOException("Failed to persist transferred character to target world database.");
            }

            var sourceCharId = srcCharacter.Id;
            srcAccount.Characters.Remove(srcCharacter);
            await srcContext.DeleteAsync(srcCharacter).ConfigureAwait(false);
            await srcContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var record = new WorldTransferRecord
            {
                SourceCharacterId = sourceCharId,
                TargetCharacterId = copiedChar.Id,
                CharacterName = copiedChar.Name,
                AccountLogin = srcAccount.LoginName,
                SourceWorldId = srcWorld.WorldId,
                TargetWorldId = dstWorld.WorldId,
                TransferredAtUtc = DateTimeOffset.UtcNow,
                OperatorName = string.IsNullOrWhiteSpace(operatorName) ? "admin" : operatorName.Trim(),
            };

            lock (this._sync)
            {
                this._state.Transfers.Add(record);
                this.SaveToDisk();
                return CloneTransfer(record);
            }
        }
        finally
        {
            this._asyncGate.Release();
        }
    }

    /// <summary>Builds player-facing grouped world summaries from registered worlds and running servers.</summary>
    /// <param name="gameServers">Running game servers.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Grouped world summaries.</returns>
    public IReadOnlyList<WorldPublicSummary> BuildGroupedSnapshot(IEnumerable<IGameServer> gameServers, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(gameServers);
        var serverList = gameServers.OrderBy(s => s.Id).ToArray();
        this.EnsureDefaultWorld(serverList.Select(s => s.Id));

        IReadOnlyList<WorldDefinition> worlds;
        lock (this._sync)
        {
            worlds = this._state.Worlds.Select(CloneWorld).ToList();
        }

        var serverLookup = serverList.ToDictionary(s => s.Id);
        var assignedServerIds = new HashSet<int>();
        var result = new List<WorldPublicSummary>(worlds.Count);

        foreach (var world in worlds)
        {
            var matched = new List<WorldServerEntry>();
            foreach (var sid in world.ServerIds)
            {
                if (serverLookup.TryGetValue(sid, out var live))
                {
                    assignedServerIds.Add(sid);
                    matched.Add(new WorldServerEntry
                    {
                        Id = live.Id,
                        Players = live.CurrentConnections,
                        Capacity = live.MaximumConnections,
                        IsRunning = live.ServerState == ServerState.Started,
                    });
                }
            }

            if (world.IsDefault)
            {
                foreach (var live in serverList)
                {
                    if (assignedServerIds.Add(live.Id))
                    {
                        matched.Add(new WorldServerEntry
                        {
                            Id = live.Id,
                            Players = live.CurrentConnections,
                            Capacity = live.MaximumConnections,
                            IsRunning = live.ServerState == ServerState.Started,
                        });
                    }
                }
            }

            result.Add(new WorldPublicSummary
            {
                WorldId = world.WorldId,
                NameEn = world.NameEn,
                NameVi = world.NameVi,
                IsRunning = world.IsRunning,
                IsOpen = world.IsOpenAt(utcNow),
                OpeningDateUtc = world.OpeningDateUtc,
                OpeningDateSydney = world.FormatOpeningSydney(),
                ExperienceMultiplier = world.ExperienceMultiplier,
                MasterExperienceMultiplier = world.MasterExperienceMultiplier,
                DropMultiplier = world.DropMultiplier,
                ZenMultiplier = world.ZenMultiplier,
                EventPresetEn = world.EventPresetEn,
                EventPresetVi = world.EventPresetVi,
                TransferEnabled = world.TransferEnabled,
                TotalPlayers = matched.Count > 0 ? matched.Sum(s => s.Players) : world.CachedOnlinePlayers,
                TotalCapacity = matched.Sum(s => s.Capacity),
                Servers = matched,
            });
        }

        return result;
    }

    private static async ValueTask CopyConfigurationAndOptionalRosterAsync(
        IPersistenceContextProvider sourceProvider,
        IPersistenceContextProvider targetProvider,
        WorldDefinition newWorld)
    {
        using var srcContext = sourceProvider.CreateNewContext();
        using var dstContext = targetProvider.CreateNewContext();

        var srcConfig = (await srcContext.GetAsync<GameConfiguration>().ConfigureAwait(false)).FirstOrDefault();
        var dstConfig = (await dstContext.GetAsync<GameConfiguration>().ConfigureAwait(false)).FirstOrDefault();
        if (srcConfig is not null)
        {
            dstConfig ??= dstContext.CreateNew<GameConfiguration>();
            dstConfig.ExperienceRate = (float)(srcConfig.ExperienceRate * Math.Max(0.1, newWorld.ExperienceMultiplier));
            dstConfig.MasterExperienceRate = (float)(srcConfig.MasterExperienceRate * Math.Max(0.1, newWorld.MasterExperienceMultiplier));
            dstConfig.MaximumLevel = srcConfig.MaximumLevel;
            dstConfig.MaximumMasterLevel = srcConfig.MaximumMasterLevel;
            dstConfig.MaximumPartySize = srcConfig.MaximumPartySize;
            dstConfig.MaximumCharactersPerAccount = srcConfig.MaximumCharactersPerAccount;

            foreach (var srcPlugIn in srcConfig.PlugInConfigurations)
            {
                var existing = dstConfig.PlugInConfigurations.FirstOrDefault(p => p.TypeId == srcPlugIn.TypeId);
                if (existing is null)
                {
                    existing = dstContext.CreateNew<PlugInConfiguration>();
                    existing.TypeId = srcPlugIn.TypeId;
                    dstConfig.PlugInConfigurations.Add(existing);
                }

                existing.IsActive = srcPlugIn.IsActive;
                existing.CustomConfiguration = srcPlugIn.CustomConfiguration;
                existing.CustomPlugInSource = srcPlugIn.CustomPlugInSource;
            }
        }

        if (newWorld.TemplateMode == WorldTemplateMode.CopiedCharacters)
        {
            var srcAccounts = (await srcContext.GetAsync<Account>().ConfigureAwait(false)).ToList();
            var existingTargets = (await dstContext.GetAsync<Account>().ConfigureAwait(false))
                .ToDictionary(a => a.LoginName, StringComparer.OrdinalIgnoreCase);

            foreach (var srcAcc in srcAccounts)
            {
                if (!existingTargets.TryGetValue(srcAcc.LoginName, out var dstAcc))
                {
                    dstAcc = dstContext.CreateNew<Account>();
                    dstAcc.LoginName = srcAcc.LoginName;
                    dstAcc.PasswordHash = srcAcc.PasswordHash;
                    dstAcc.SecurityCode = srcAcc.SecurityCode;
                    dstAcc.EMail = srcAcc.EMail;
                    dstAcc.LanguageIsoCode = srcAcc.LanguageIsoCode;
                    dstAcc.State = srcAcc.State;
                    existingTargets[dstAcc.LoginName] = dstAcc;
                }

                foreach (var srcChar in srcAcc.Characters)
                {
                    if (dstAcc.Characters.Any(c => string.Equals(c.Name, srcChar.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    CloneCharacterToContext(srcChar, dstAcc, dstContext, dstConfig);
                }
            }
        }

        await dstContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private static Character CloneCharacterToContext(
        Character srcChar,
        Account targetAccount,
        IContext targetContext,
        GameConfiguration? targetConfig)
    {
        var copy = targetContext.CreateNew<Character>();
        copy.Id = Guid.NewGuid();
        copy.Name = srcChar.Name;
        copy.Experience = srcChar.Experience;
        copy.MasterExperience = srcChar.MasterExperience;
        copy.LevelUpPoints = srcChar.LevelUpPoints;
        copy.MasterLevelUpPoints = srcChar.MasterLevelUpPoints;
        copy.CharacterSlot = srcChar.CharacterSlot;
        copy.PositionX = srcChar.PositionX;
        copy.PositionY = srcChar.PositionY;
        copy.InventoryExtensions = srcChar.InventoryExtensions;
        if (targetConfig is not null && srcChar.CharacterClass is not null)
        {
            copy.CharacterClass = targetConfig.CharacterClasses.FirstOrDefault(c => c.Number == srcChar.CharacterClass.Number)
                ?? targetConfig.CharacterClasses.FirstOrDefault();
        }

        if (targetConfig is not null && srcChar.CurrentMap is not null)
        {
            copy.CurrentMap = targetConfig.Maps.FirstOrDefault(m => m.Number == srcChar.CurrentMap.Number)
                ?? targetConfig.Maps.FirstOrDefault();
        }

        targetAccount.Characters.Add(copy);
        return copy;
    }

    private static WorldDefinition CloneWorld(WorldDefinition source) => new()
    {
        WorldId = source.WorldId,
        NameEn = source.NameEn,
        NameVi = source.NameVi,
        DatabaseName = source.DatabaseName,
        DatabaseConnectionString = source.DatabaseConnectionString,
        ServerIds = [.. source.ServerIds],
        ExperienceMultiplier = source.ExperienceMultiplier,
        MasterExperienceMultiplier = source.MasterExperienceMultiplier,
        DropMultiplier = source.DropMultiplier,
        ZenMultiplier = source.ZenMultiplier,
        EventPresetEn = source.EventPresetEn,
        EventPresetVi = source.EventPresetVi,
        TemplateSourceWorldId = source.TemplateSourceWorldId,
        TemplateMode = source.TemplateMode,
        OpeningDateUtc = source.OpeningDateUtc,
        TransferEnabled = source.TransferEnabled,
        IsRunning = source.IsRunning,
        IsDefault = source.IsDefault,
        CachedOnlinePlayers = source.CachedOnlinePlayers,
        CreatedAtUtc = source.CreatedAtUtc,
    };

    private static WorldTransferRecord CloneTransfer(WorldTransferRecord source) => new()
    {
        TransferId = source.TransferId,
        SourceCharacterId = source.SourceCharacterId,
        TargetCharacterId = source.TargetCharacterId,
        CharacterName = source.CharacterName,
        AccountLogin = source.AccountLogin,
        SourceWorldId = source.SourceWorldId,
        TargetWorldId = source.TargetWorldId,
        TransferredAtUtc = source.TransferredAtUtc,
        OperatorName = source.OperatorName,
    };

    private WorldDefinition? FindWorldUnsafe(string worldId) =>
        this._state.Worlds.FirstOrDefault(w => string.Equals(w.WorldId, worldId.Trim(), StringComparison.OrdinalIgnoreCase));

    private void LoadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(this._storagePath) || !File.Exists(this._storagePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(this._storagePath);
            var loaded = JsonSerializer.Deserialize<WorldRegistryState>(json);
            if (loaded is not null)
            {
                this._state = loaded;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void SaveToDisk()
    {
        if (string.IsNullOrWhiteSpace(this._storagePath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(this._storagePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(this._storagePath, JsonSerializer.Serialize(this._state, JsonOptions));
    }
}
