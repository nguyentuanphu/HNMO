// <copyright file="CharacterToolsOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;

/// <summary>Audited Character Tools backend (unstuck move, buffs, and offline-only level/stats/Zen/class edits).</summary>
/// <param name="persistence">The persistence context provider.</param>
/// <param name="configurations">The configuration source.</param>
/// <param name="login">The login server coordinator.</param>
/// <param name="servers">The server registry for live actions.</param>
/// <param name="executor">The authorization and audit executor.</param>
/// <param name="settings">The admin write settings.</param>
public sealed class CharacterToolsOperations(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    ILoginServer login,
    IServerProvider servers,
    AdminOperationExecutor executor,
    AdminWriteSettings settings)
{
    /// <summary>Gets or sets an optional activity log state store key used by isolated unit tests.</summary>
    public string? ActivityLogFileName { get; set; }

    /// <summary>Loads a character snapshot plus online status for the Character Tools UI.</summary>
    /// <param name="characterName">The exact character name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The character snapshot.</returns>
    public async ValueTask<CharacterToolSnapshot> LoadCharacterAsync(string characterName, CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Operator, cancellationToken).ConfigureAwait(false);
        var trimmed = ValidateCharacterName(characterName);
        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(config);
        var account = await context.GetAccountByCharacterNameAsync(trimmed, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminOperationException("Character not found.", AdminOperationErrorKind.InvalidInput);
        var character = account.Characters.Single(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        var online = await login.GetSnapshotAsync().ConfigureAwait(false);
        var onlineEntry = online.FirstOrDefault(kv => string.Equals(kv.Key, account.LoginName, StringComparison.OrdinalIgnoreCase));
        var isOnline = onlineEntry.Key is not null;

        return BuildSnapshot(character, account, isOnline, isOnline ? onlineEntry.Value : null);
    }

    /// <summary>Loads available town maps, classes, and buff definitions from the active configuration.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Maps, character classes, and buff effects.</returns>
    public async ValueTask<(IReadOnlyList<GameMapDefinition> Towns, IReadOnlyList<CharacterClass> Classes, IReadOnlyList<MagicEffectDefinition> Buffs)> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Operator, cancellationToken).ConfigureAwait(false);
        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var towns = config.Maps
            .Where(m => m.IsActive && m.ExitGates.Any(g => g.IsSpawnGate))
            .OrderBy(m => m.Number)
            .ToList();
        var classes = config.CharacterClasses
            .OrderBy(c => c.Number)
            .ToList();
        var buffs = config.MagicEffects
            .Where(e => e.PowerUpDefinitions.Any(p => p.Boost is not null && p.TargetAttribute is not null)
                && (e.Duration?.ConstantValue?.Value ?? 0) > 0)
            .OrderBy(e => e.Number)
            .ToList();
        return (towns, classes, buffs);
    }

    /// <summary>Moves a stuck character (online or offline) to a safe town coordinate with a required reason.</summary>
    /// <param name="characterName">The exact character name.</param>
    /// <param name="targetMapNumber">The target map number, or <c>null</c> to use the character's home town.</param>
    /// <param name="reason">The required operator reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A summary JSON payload.</returns>
    public async ValueTask<JsonObject> MoveToTownAsync(string characterName, ushort? targetMapNumber, string reason, CancellationToken cancellationToken)
    {
        await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Operator, cancellationToken).ConfigureAwait(false);
        var trimmedName = ValidateCharacterName(characterName);
        var cleanReason = ValidateReason(reason);

        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var lookup = persistence.CreateNewPlayerContext(config);
        var account = await lookup.GetAccountByCharacterNameAsync(trimmedName, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminOperationException("Character not found.", AdminOperationErrorKind.InvalidInput);
        var ch = account.Characters.Single(c => string.Equals(c.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

        var destination = targetMapNumber is { } num
            ? config.Maps.FirstOrDefault(m => m.Number == num && m.IsActive)
                ?? throw new AdminOperationException("Target map was not found or is inactive.", AdminOperationErrorKind.InvalidInput)
            : config.GetActiveSpawnMap(ch.CharacterClass?.HomeMap)
                ?? throw new AdminOperationException("No active town spawn map is configured.", AdminOperationErrorKind.InvalidInput);

        var (destX, destY) = ResolveSafeTownCoordinates(destination);

        return await executor.ExecuteAsync(
            AdminRoles.Operator,
            "character_tools_move_town",
            new { characterName = ch.Name, account = account.LoginName, mapNumber = destination.Number, destX, destY, reason = cleanReason },
            async actor =>
            {
                var snapshot = await login.GetSnapshotAsync().ConfigureAwait(false);
                var onlineEntry = snapshot.FirstOrDefault(kv => string.Equals(kv.Key, account.LoginName, StringComparison.OrdinalIgnoreCase));
                if (onlineEntry.Key is not null)
                {
                    var liveServer = this.ResolveLiveServer(onlineEntry.Value);
                    var live = await liveServer.GetPlayerAsync(ch.Name, cancellationToken).ConfigureAwait(false)
                        ?? throw new AdminOperationException("Character is not in an active live map session. Wait or retry.");
                    var result = await liveServer.TeleportAsync(live.Character, live.CharacterId, checked((ushort)destination.Number), destX, destY, cancellationToken).ConfigureAwait(false);
                    if (!result.Succeeded)
                    {
                        throw new AdminOperationException(result.Error ?? "Failed to move live character.");
                    }
                }
                else
                {
                    await this.WithOfflineReservationAsync(
                        account.LoginName,
                        async () =>
                        {
                            using var edit = persistence.CreateNewPlayerContext(config);
                            var lockedAccount = await edit.GetAccountByCharacterNameAsync(ch.Name, cancellationToken).ConfigureAwait(false)
                                ?? throw new AdminOperationException("Character no longer exists.");
                            var lockedChar = lockedAccount.Characters.Single(c => string.Equals(c.Name, ch.Name, StringComparison.OrdinalIgnoreCase));
                            lockedChar.CurrentMap = config.Maps.Single(m => m.GetId() == destination.GetId());
                            lockedChar.PositionX = destX;
                            lockedChar.PositionY = destY;
                            await AdminChangeExecution.SaveAsync(edit, cancellationToken).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                }

                this.RecordActivity(account.LoginName, ch.Name, actor.Name, 0, $"MoveToTown -> {destination.Name} ({destX},{destY}) | Reason: {cleanReason}");
                return new JsonObject
                {
                    ["characterName"] = ch.Name,
                    ["mapNumber"] = destination.Number,
                    ["mapName"] = destination.Name.ToString(),
                    ["x"] = destX,
                    ["y"] = destY,
                    ["online"] = onlineEntry.Key is not null,
                    ["reason"] = cleanReason,
                };
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies a timed buff to an online character with a required reason.</summary>
    /// <param name="characterName">The exact character name.</param>
    /// <param name="effectNumber">The magic effect definition number.</param>
    /// <param name="durationSeconds">The duration in seconds (10..86400).</param>
    /// <param name="reason">The required operator reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary JSON payload.</returns>
    public async ValueTask<JsonObject> ApplyBuffAsync(string characterName, short effectNumber, int durationSeconds, string reason, CancellationToken cancellationToken)
    {
        await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Operator, cancellationToken).ConfigureAwait(false);
        var trimmedName = ValidateCharacterName(characterName);
        var cleanReason = ValidateReason(reason);
        if (durationSeconds is < 10 or > 86_400)
        {
            throw new AdminOperationException("Buff duration must be between 10 and 86,400 seconds.", AdminOperationErrorKind.InvalidInput);
        }

        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var effectDef = config.MagicEffects.FirstOrDefault(e => e.Number == effectNumber)
            ?? throw new AdminOperationException("Selected buff definition not found.", AdminOperationErrorKind.InvalidInput);

        using var lookup = persistence.CreateNewPlayerContext(config);
        var account = await lookup.GetAccountByCharacterNameAsync(trimmedName, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminOperationException("Character not found.", AdminOperationErrorKind.InvalidInput);
        var ch = account.Characters.Single(c => string.Equals(c.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

        return await executor.ExecuteAsync(
            AdminRoles.Operator,
            "character_tools_apply_buff",
            new { characterName = ch.Name, account = account.LoginName, effectNumber, durationSeconds, reason = cleanReason },
            async actor =>
            {
                var snapshot = await login.GetSnapshotAsync().ConfigureAwait(false);
                var onlineEntry = snapshot.FirstOrDefault(kv => string.Equals(kv.Key, account.LoginName, StringComparison.OrdinalIgnoreCase));
                if (onlineEntry.Key is null)
                {
                    throw new AdminOperationException("Character must be online to apply a live buff.", AdminOperationErrorKind.InvalidInput);
                }

                var liveServer = this.ResolveLiveServer(onlineEntry.Value);
                var live = await liveServer.GetPlayerAsync(ch.Name, cancellationToken).ConfigureAwait(false)
                    ?? throw new AdminOperationException("Character is not currently active in a map session.");
                var result = await liveServer.ApplyBuffAsync(live.Character, live.CharacterId, effectNumber, durationSeconds, cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    throw new AdminOperationException(result.Error ?? "Failed to apply buff.");
                }

                this.RecordActivity(account.LoginName, ch.Name, actor.Name, 0, $"ApplyBuff #{effectNumber} ({effectDef.Name}) {durationSeconds}s | Reason: {cleanReason}");
                return new JsonObject
                {
                    ["characterName"] = ch.Name,
                    ["effectNumber"] = effectNumber,
                    ["effectName"] = effectDef.Name.ToString(),
                    ["durationSeconds"] = durationSeconds,
                    ["reason"] = cleanReason,
                };
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Clears active timed buffs from an online character with a required reason.</summary>
    /// <param name="characterName">The exact character name.</param>
    /// <param name="reason">The required operator reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary JSON payload.</returns>
    public async ValueTask<JsonObject> ClearBuffsAsync(string characterName, string reason, CancellationToken cancellationToken)
    {
        await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Operator, cancellationToken).ConfigureAwait(false);
        var trimmedName = ValidateCharacterName(characterName);
        var cleanReason = ValidateReason(reason);

        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var lookup = persistence.CreateNewPlayerContext(config);
        var account = await lookup.GetAccountByCharacterNameAsync(trimmedName, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminOperationException("Character not found.", AdminOperationErrorKind.InvalidInput);
        var ch = account.Characters.Single(c => string.Equals(c.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

        return await executor.ExecuteAsync(
            AdminRoles.Operator,
            "character_tools_clear_buffs",
            new { characterName = ch.Name, account = account.LoginName, reason = cleanReason },
            async actor =>
            {
                var snapshot = await login.GetSnapshotAsync().ConfigureAwait(false);
                var onlineEntry = snapshot.FirstOrDefault(kv => string.Equals(kv.Key, account.LoginName, StringComparison.OrdinalIgnoreCase));
                if (onlineEntry.Key is null)
                {
                    throw new AdminOperationException("Character must be online to clear live buffs.", AdminOperationErrorKind.InvalidInput);
                }

                var liveServer = this.ResolveLiveServer(onlineEntry.Value);
                var live = await liveServer.GetPlayerAsync(ch.Name, cancellationToken).ConfigureAwait(false)
                    ?? throw new AdminOperationException("Character is not currently active in a map session.");
                var result = await liveServer.ClearBuffsAsync(live.Character, live.CharacterId, cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    throw new AdminOperationException(result.Error ?? "Failed to clear buffs.");
                }

                this.RecordActivity(account.LoginName, ch.Name, actor.Name, 0, $"ClearBuffs | Reason: {cleanReason}");
                return new JsonObject
                {
                    ["characterName"] = ch.Name,
                    ["cleared"] = true,
                    ["reason"] = cleanReason,
                };
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Edits level, master level, resets, base stats, Zen, or class for an OFFLINE character with a required reason.
    /// Refuses unsafe edits while the account is online.
    /// </summary>
    /// <param name="characterName">The exact character name.</param>
    /// <param name="request">The requested edits and reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary JSON payload.</returns>
    public async ValueTask<JsonObject> EditOfflineCharacterAsync(string characterName, CharacterEditRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Operator, cancellationToken).ConfigureAwait(false);
        var trimmedName = ValidateCharacterName(characterName);
        var cleanReason = ValidateReason(request.Reason);

        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var lookup = persistence.CreateNewPlayerContext(config);
        var account = await lookup.GetAccountByCharacterNameAsync(trimmedName, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminOperationException("Character not found.", AdminOperationErrorKind.InvalidInput);

        var online = await login.GetSnapshotAsync().ConfigureAwait(false);
        if (online.Keys.Any(k => string.Equals(k, account.LoginName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AdminOperationException("Unsafe edit refused: character is currently online. Disconnect them before changing level, stats, Zen, or class.", AdminOperationErrorKind.InvalidInput);
        }

        CharacterClass? requestedClass = null;
        if (request.CharacterClassId is { } classId)
        {
            requestedClass = config.CharacterClasses.SingleOrDefault(c => c.GetId() == classId)
                ?? throw new AdminOperationException("Selected character class was not found in configuration.", AdminOperationErrorKind.InvalidInput);
        }

        return await executor.ExecuteAsync(
            AdminRoles.Operator,
            "character_tools_edit_offline",
            new
            {
                characterName = trimmedName,
                account = account.LoginName,
                request.Level,
                request.MasterLevel,
                request.Resets,
                request.Strength,
                request.Agility,
                request.Vitality,
                request.Energy,
                request.Leadership,
                request.Zen,
                request.CharacterClassId,
                reason = cleanReason,
            },
            async actor =>
            {
                var zenDelta = 0L;
                var summary = string.Empty;
                await this.WithOfflineReservationAsync(
                    account.LoginName,
                    async () =>
                    {
                        using var edit = persistence.CreateNewPlayerContext(config);
                        var lockedAccount = await edit.GetAccountByCharacterNameAsync(trimmedName, cancellationToken).ConfigureAwait(false)
                            ?? throw new AdminOperationException("Character no longer exists.");
                        var lockedChar = lockedAccount.Characters.Single(c => string.Equals(c.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

                        var beforeLevel = ReadStat(lockedChar, Stats.Level);
                        var beforeZen = lockedChar.Inventory?.Money ?? 0;
                        var beforeClass = lockedChar.CharacterClass?.Name.ToString() ?? string.Empty;

                        ApplyStatIfSet(lockedChar, Stats.Level, request.Level, 1, config.MaximumLevel, nameof(request.Level));
                        ApplyStatIfSet(lockedChar, Stats.MasterLevel, request.MasterLevel, 0, config.MaximumMasterLevel, nameof(request.MasterLevel));
                        ApplyStatIfSet(lockedChar, Stats.Resets, request.Resets, 0, int.MaxValue, nameof(request.Resets));
                        ApplyStatIfSet(lockedChar, Stats.BaseStrength, request.Strength, 0, short.MaxValue, nameof(request.Strength));
                        ApplyStatIfSet(lockedChar, Stats.BaseAgility, request.Agility, 0, short.MaxValue, nameof(request.Agility));
                        ApplyStatIfSet(lockedChar, Stats.BaseVitality, request.Vitality, 0, short.MaxValue, nameof(request.Vitality));
                        ApplyStatIfSet(lockedChar, Stats.BaseEnergy, request.Energy, 0, short.MaxValue, nameof(request.Energy));
                        ApplyStatIfSet(lockedChar, Stats.BaseLeadership, request.Leadership, 0, short.MaxValue, nameof(request.Leadership));

                        if (request.Zen is { } targetZen)
                        {
                            if (targetZen < 0 || targetZen > config.MaximumInventoryMoney)
                            {
                                throw new AdminOperationException($"Zen must be between 0 and {config.MaximumInventoryMoney:N0}.", AdminOperationErrorKind.InvalidInput);
                            }

                            var inv = lockedChar.Inventory ?? throw new AdminOperationException("Character inventory is missing.");
                            zenDelta = targetZen - inv.Money;
                            inv.Money = targetZen;
                        }

                        if (requestedClass is not null)
                        {
                            lockedChar.CharacterClass = config.CharacterClasses.Single(c => c.GetId() == requestedClass.GetId());
                        }

                        await AdminChangeExecution.SaveAsync(edit, cancellationToken).ConfigureAwait(false);
                        summary = $"EditOffline L:{beforeLevel}->{ReadStat(lockedChar, Stats.Level)} Z:{beforeZen}->{lockedChar.Inventory?.Money ?? 0} C:{beforeClass}->{lockedChar.CharacterClass?.Name} | Reason: {cleanReason}";
                    }).ConfigureAwait(false);

                this.RecordActivity(account.LoginName, trimmedName, actor.Name, zenDelta, summary);
                return new JsonObject
                {
                    ["characterName"] = trimmedName,
                    ["updated"] = true,
                    ["reason"] = cleanReason,
                    ["summary"] = summary,
                };
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static string ValidateCharacterName(string characterName)
    {
        var trimmed = characterName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 10)
        {
            throw new AdminOperationException("A valid character name (1-10 characters) is required.", AdminOperationErrorKind.InvalidInput);
        }

        return trimmed;
    }

    private static string ValidateReason(string reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length < 3)
        {
            throw new AdminOperationException("An operator reason (at least 3 characters) is required.", AdminOperationErrorKind.InvalidInput);
        }

        return trimmed;
    }

    private static int ReadStat(Character character, AttributeDefinition def)
        => (int)(character.Attributes.FirstOrDefault(a => a.Definition?.Id == def.Id)?.Value ?? 0);

    private static void ApplyStatIfSet(Character character, AttributeDefinition def, int? target, int min, int max, string label)
    {
        if (target is not { } value)
        {
            return;
        }

        if (value < min || value > max)
        {
            throw new AdminOperationException($"{label} must be between {min:N0} and {max:N0}.", AdminOperationErrorKind.InvalidInput);
        }

        var existing = character.Attributes.SingleOrDefault(a => a.Definition?.Id == def.Id)
            ?? throw new AdminOperationException($"Stat attribute '{label}' is missing on this character.", AdminOperationErrorKind.InvalidInput);
        existing.Value = value;
    }

    private static CharacterToolSnapshot BuildSnapshot(Character ch, Account account, bool isOnline, int? serverId)
        => new(
            ch.GetId(),
            ch.Name,
            account.LoginName,
            isOnline,
            serverId,
            ch.CharacterStatus.ToString(),
            ch.CharacterClass?.GetId() ?? Guid.Empty,
            ch.CharacterClass?.Name.ToString() ?? "-",
            (ushort)(ch.CurrentMap?.Number ?? 0),
            ch.CurrentMap?.Name.ToString() ?? "-",
            ch.PositionX,
            ch.PositionY,
            ReadStat(ch, Stats.Level),
            ReadStat(ch, Stats.MasterLevel),
            ReadStat(ch, Stats.Resets),
            ReadStat(ch, Stats.BaseStrength),
            ReadStat(ch, Stats.BaseAgility),
            ReadStat(ch, Stats.BaseVitality),
            ReadStat(ch, Stats.BaseEnergy),
            ReadStat(ch, Stats.BaseLeadership),
            ch.Inventory?.Money ?? 0);

    private static (byte X, byte Y) ResolveSafeTownCoordinates(GameMapDefinition map)
    {
        var terrain = new GameMapTerrain(map);
        var gate = map.GetSafezoneGate(terrain);
        if (gate is not null && terrain.WalkMap[gate.X1, gate.Y1])
        {
            return (gate.X1, gate.Y1);
        }

        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                if (terrain.SafezoneMap[x, y] && terrain.WalkMap[x, y])
                {
                    return ((byte)x, (byte)y);
                }
            }
        }

        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                if (terrain.WalkMap[x, y])
                {
                    return ((byte)x, (byte)y);
                }
            }
        }

        return (125, 125);
    }

    private IGameServerAdministration ResolveLiveServer(int serverId)
        => servers.Servers.OfType<IGameServer>().SingleOrDefault(s => s.Id == serverId) is { ServerState: ServerState.Started } server && server is IGameServerAdministration administration
            ? administration
            : throw new AdminOperationException("The owning game server or live administration channel is unavailable.", AdminOperationErrorKind.Unavailable);

    private async ValueTask WithOfflineReservationAsync(string loginName, Func<ValueTask> work)
    {
        var reservationId = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        if (!await login.TryReserveAccountAsync(loginName, reservationId).ConfigureAwait(false))
        {
            throw new AdminOperationException("Account is online or reserved. Disconnect the account before editing.", AdminOperationErrorKind.InvalidInput);
        }

        Exception? failure = null;
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        try
        {
            await login.ReleaseAccountReservationAsync(loginName, reservationId).ConfigureAwait(false);
        }
        catch (Exception releaseEx)
        {
            throw new AdminOperationException(
                "Offline operation completed, but releasing login reservation failed.",
                AdminOperationErrorKind.Uncertain,
                failure is null ? releaseEx : new AggregateException(failure, releaseEx));
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void RecordActivity(string accountLogin, string characterName, string adminActor, long zenDelta, string details)
    {
        var entry = new ActivityLogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Kind = ActivityKind.Administration,
            Account = accountLogin,
            Character = characterName,
            PartnerCharacter = adminActor,
            ItemSummary = "CharacterTools",
            ZenDelta = zenDelta,
            Details = details,
        };
        ActivityLogPlugIn.RecordExternal(entry, this.ActivityLogFileName);
    }
}
