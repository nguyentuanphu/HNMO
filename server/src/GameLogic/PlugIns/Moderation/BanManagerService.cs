// <copyright file="BanManagerService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Moderation;

using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// Manages timed and permanent account/character bans, chat mutes, and salted network/device bans
/// backed by existing PostgreSQL feature rows (<see cref="IHnmoPlayerStateRowBackend"/>).
/// </summary>
/// <param name="backend">The player-state row backend.</param>
public sealed class BanManagerService(IHnmoPlayerStateRowBackend backend)
{
    /// <summary>Player-state feature key storing individual ban/mute records.</summary>
    public const string BanFeature = "moderation.ban";

    /// <summary>Player-state feature key storing the persistent 256-bit HMAC identity salt.</summary>
    public const string SaltFeature = "moderation.salt";

    private const string SaltScopeId = "identity";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly ConditionalWeakTable<IHnmoPlayerStateRowBackend, object> Gates = new();

    private static BanManagerService? defaultInstance;

    /// <summary>Gets the shared default moderation service bound to the host player-state backend.</summary>
    public static BanManagerService Default
        => defaultInstance ??= new BanManagerService(
            HnmoPlayerStateRowStoreRegistry.GetBackend(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName)));

    /// <summary>Formats a UTC expiration time into Australia/Sydney local time in EN or VI.</summary>
    /// <param name="expiresAtUtc">UTC expiry or <c>null</c> for permanent.</param>
    /// <param name="vietnamese">Whether to format Vietnamese text.</param>
    /// <returns>Human-readable expiry string in Sydney time.</returns>
    public static string FormatSydneyExpiry(DateTime? expiresAtUtc, bool vietnamese)
    {
        if (!expiresAtUtc.HasValue)
        {
            return vietnamese ? "Vĩnh viễn" : "Permanent";
        }

        var utc = DateTime.SpecifyKind(expiresAtUtc.Value, DateTimeKind.Utc);
        var sydney = TimeZoneInfo.ConvertTimeFromUtc(utc, ResolveSydneyZone());
        var stamp = sydney.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        return $"{stamp} (Sydney)";
    }

    /// <summary>Formats a single-language player notice containing reason and Sydney expiration time.</summary>
    /// <param name="record">The active ban or mute record.</param>
    /// <param name="vietnamese">Whether the player uses Vietnamese.</param>
    /// <returns>Localized notice safe to display at login or chat.</returns>
    public static string FormatPlayerNotice(BanRecord record, bool vietnamese)
    {
        ArgumentNullException.ThrowIfNull(record);
        var until = FormatSydneyExpiry(record.ExpiresAtUtc, vietnamese);
        var reason = vietnamese ? record.ReasonVi : record.ReasonEn;
        return record.Kind switch
        {
            BanTargetKind.AccountMute or BanTargetKind.CharacterMute => vietnamese
                ? $"Bạn đang bị cấm chat ({until}). Lý do: {reason}"
                : $"You are muted ({until}). Reason: {reason}",
            BanTargetKind.CharacterBan => vietnamese
                ? $"Nhân vật đang bị khóa ({until}). Lý do: {reason}"
                : $"Character is banned ({until}). Reason: {reason}",
            BanTargetKind.NetworkBan or BanTargetKind.DeviceBan => vietnamese
                ? $"Kết nối hoặc thiết bị này đang bị hạn chế ({until}). Lý do: {reason}"
                : $"This network or device is blocked ({until}). Reason: {reason}",
            _ => vietnamese
                ? $"Tài khoản đang bị khóa ({until}). Lý do: {reason}"
                : $"Account is banned ({until}). Reason: {reason}",
        };
    }

    /// <summary>Builds a plain-ASCII/UTF-8 JSON payload for packet <c>HnmoBanNotice</c> and portal login refusal responses.</summary>
    /// <param name="record">The enforced moderation record.</param>
    /// <returns>Serialized JSON payload.</returns>
    public static string BuildNoticeJson(BanRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return JsonSerializer.Serialize(
            new
            {
                kind = record.Kind.ToString(),
                reasonEn = record.ReasonEn,
                reasonVi = record.ReasonVi,
                expiresAtUtc = record.ExpiresAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                permanent = !record.ExpiresAtUtc.HasValue,
                sydneyEn = FormatSydneyExpiry(record.ExpiresAtUtc, vietnamese: false),
                sydneyVi = FormatSydneyExpiry(record.ExpiresAtUtc, vietnamese: true),
                messageEn = FormatPlayerNotice(record, vietnamese: false),
                messageVi = FormatPlayerNotice(record, vietnamese: true),
            },
            JsonOptions);
    }

    /// <summary>Issues a new timed or permanent ban or mute record.</summary>
    /// <param name="kind">Target scope.</param>
    /// <param name="targetInput">Exact account login, character name, raw IP/CIDR string, or raw device token (hashed immediately for Network/Device).</param>
    /// <param name="reasonEn">English player-facing reason.</param>
    /// <param name="reasonVi">Vietnamese player-facing reason.</param>
    /// <param name="issuedBy">Administrator login.</param>
    /// <param name="nowUtc">Authoritative UTC timestamp.</param>
    /// <param name="expiresAtUtc">UTC expiry or <c>null</c> for permanent.</param>
    /// <returns>The created and persisted record.</returns>
    public BanRecord Issue(BanTargetKind kind, string targetInput, string reasonEn, string reasonVi, string issuedBy, DateTime nowUtc, DateTime? expiresAtUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || expiresAtUtc is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException("Timestamps must be UTC.", nameof(nowUtc));
        }

        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= nowUtc)
        {
            throw new ArgumentException("Expiry must be in the future.", nameof(expiresAtUtc));
        }

        var trimmed = targetInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 256)
        {
            throw new ArgumentException("Target is required.", nameof(targetInput));
        }

        var cleanEn = string.IsNullOrWhiteSpace(reasonEn) ? reasonVi?.Trim() ?? string.Empty : reasonEn.Trim();
        var cleanVi = string.IsNullOrWhiteSpace(reasonVi) ? cleanEn : reasonVi.Trim();
        if (string.IsNullOrWhiteSpace(cleanEn) || cleanEn.Length > 300 || cleanVi.Length > 300)
        {
            throw new ArgumentException("A valid reason is required (1-300 characters).", nameof(reasonEn));
        }

        var actor = string.IsNullOrWhiteSpace(issuedBy) ? "admin" : issuedBy.Trim();
        lock (Gates.GetValue(backend, static _ => new()))
        {
            string targetKey;
            string targetDisplay;
            if (kind is BanTargetKind.NetworkBan or BanTargetKind.DeviceBan)
            {
                var salt = this.EnsureOrLoadSaltLocked(nowUtc);
                var prefix = kind == BanTargetKind.NetworkBan ? "ip:" : "dev:";
                var hash = HashIdentity(salt, prefix, NormalizeIdentity(trimmed, kind == BanTargetKind.NetworkBan));
                targetKey = prefix + hash;
                targetDisplay = $"{prefix}{hash[..12]}…";
            }
            else
            {
                targetKey = NormalizeTargetKey(kind, trimmed);
                targetDisplay = trimmed;
            }

            var id = Guid.NewGuid().ToString("N");
            var record = new BanRecord(
                id,
                kind,
                targetKey,
                targetDisplay,
                cleanEn,
                cleanVi,
                nowUtc,
                expiresAtUtc,
                actor,
                Active: true,
                LeavesCharacterStatus: kind == BanTargetKind.CharacterBan && expiresAtUtc.HasValue);

            var row = new HnmoFeatureRow(BanFeature, id, JsonSerializer.Serialize(record, JsonOptions), nowUtc);
            backend.UpsertBatch([row], []);
            return record;
        }
    }

    /// <summary>Lifts an active ban or mute record with a required reason.</summary>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="liftedBy">The administrator lifting the penalty.</param>
    /// <param name="unbanReason">Mandatory internal audit reason.</param>
    /// <param name="nowUtc">Authoritative UTC timestamp.</param>
    /// <returns>The updated record, or <c>null</c> if not found.</returns>
    public BanRecord? Lift(string recordId, string liftedBy, string unbanReason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(recordId) || string.IsNullOrWhiteSpace(unbanReason))
        {
            throw new ArgumentException("Record id and unban reason are required.");
        }

        lock (Gates.GetValue(backend, static _ => new()))
        {
            var existing = this.LoadAllLocked().FirstOrDefault(r => r.Id == recordId);
            if (existing is null)
            {
                return null;
            }

            if (!existing.Active)
            {
                return existing;
            }

            var updated = existing with
            {
                Active = false,
                LiftedAtUtc = nowUtc,
                LiftedBy = string.IsNullOrWhiteSpace(liftedBy) ? "admin" : liftedBy.Trim(),
                UnbanReason = unbanReason.Trim(),
            };
            backend.UpsertBatch([new HnmoFeatureRow(BanFeature, updated.Id, JsonSerializer.Serialize(updated, JsonOptions), nowUtc)], []);
            return updated;
        }
    }

    /// <summary>Searches moderation records ordered newest-first.</summary>
    /// <param name="query">Optional filter matching login/character/hash prefix/reason.</param>
    /// <param name="kindFilter">Optional target kind filter.</param>
    /// <param name="activeOnly">Whether to return only currently enforced records.</param>
    /// <param name="nowUtc">Current UTC timestamp for expiry checks.</param>
    /// <param name="offset">Zero-based offset.</param>
    /// <param name="limit">Maximum page size.</param>
    /// <returns>Matching page and total match count.</returns>
    public (IReadOnlyList<BanRecord> Items, int Total) Search(
        string? query,
        BanTargetKind? kindFilter,
        bool activeOnly,
        DateTime nowUtc,
        int offset = 0,
        int limit = 50)
    {
        var q = query?.Trim();
        var boundedOffset = Math.Max(0, offset);
        var boundedLimit = Math.Clamp(limit, 1, 200);
        lock (Gates.GetValue(backend, static _ => new()))
        {
            var filtered = this.LoadAllLocked()
                .Where(r => !kindFilter.HasValue || r.Kind == kindFilter.Value)
                .Where(r => !activeOnly || r.IsEnforcedAt(nowUtc))
                .Where(r => string.IsNullOrWhiteSpace(q)
                    || r.TargetDisplay.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || r.ReasonEn.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || r.ReasonVi.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || r.IssuedBy.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToArray();

            var items = filtered.Skip(boundedOffset).Take(boundedLimit).ToArray();
            return (items, filtered.Length);
        }
    }

    /// <summary>Finds an active login-blocking ban for account, IP address, or device token.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="ipAddress">Optional remote IP address (hashed against stored salt).</param>
    /// <param name="deviceId">Optional client device identifier (hashed against stored salt).</param>
    /// <param name="nowUtc">Current UTC timestamp.</param>
    /// <returns>Highest-priority enforced login block record, or <c>null</c>.</returns>
    public BanRecord? FindActiveLoginBlock(string? loginName, string? ipAddress, string? deviceId, DateTime nowUtc)
    {
        var cleanLogin = loginName?.Trim();
        lock (Gates.GetValue(backend, static _ => new()))
        {
            var all = this.LoadAllLocked();
            if (all.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(cleanLogin))
            {
                var acctKey = NormalizeTargetKey(BanTargetKind.AccountBan, cleanLogin);
                var acct = all.Where(r => r.Kind == BanTargetKind.AccountBan && r.TargetKey == acctKey && r.IsEnforcedAt(nowUtc))
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .FirstOrDefault();
                if (acct is not null)
                {
                    return acct;
                }
            }

            var hasNetworkOrDevice = all.Any(r => r.Kind is BanTargetKind.NetworkBan or BanTargetKind.DeviceBan && r.IsEnforcedAt(nowUtc));
            if (!hasNetworkOrDevice)
            {
                return null;
            }

            var salt = this.TryLoadSaltFailClosedLocked(requireIfAnyBanExists: true);
            if (!string.IsNullOrWhiteSpace(ipAddress) && salt is not null)
            {
                var netKey = "ip:" + HashIdentity(salt, "ip:", NormalizeIdentity(ipAddress, isNetwork: true));
                var net = all.Where(r => r.Kind == BanTargetKind.NetworkBan && r.TargetKey == netKey && r.IsEnforcedAt(nowUtc))
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .FirstOrDefault();
                if (net is not null)
                {
                    return net;
                }
            }

            if (!string.IsNullOrWhiteSpace(deviceId) && salt is not null)
            {
                var devKey = "dev:" + HashIdentity(salt, "dev:", NormalizeIdentity(deviceId, isNetwork: false));
                var dev = all.Where(r => r.Kind == BanTargetKind.DeviceBan && r.TargetKey == devKey && r.IsEnforcedAt(nowUtc))
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .FirstOrDefault();
                if (dev is not null)
                {
                    return dev;
                }
            }

            return null;
        }
    }

    /// <summary>Finds an active character-level ban.</summary>
    /// <param name="characterName">Exact character name.</param>
    /// <param name="nowUtc">Current UTC timestamp.</param>
    /// <returns>Enforced character ban, or <c>null</c>.</returns>
    public BanRecord? FindActiveCharacterBan(string? characterName, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        var key = NormalizeTargetKey(BanTargetKind.CharacterBan, characterName);
        lock (Gates.GetValue(backend, static _ => new()))
        {
            return this.LoadAllLocked()
                .Where(r => r.Kind == BanTargetKind.CharacterBan && r.TargetKey == key && r.IsEnforcedAt(nowUtc))
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
        }
    }

    /// <summary>Finds an active account or character chat mute.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="characterName">Selected character name.</param>
    /// <param name="nowUtc">Current UTC timestamp.</param>
    /// <returns>Enforced chat mute record, or <c>null</c>.</returns>
    public BanRecord? FindActiveChatMute(string? loginName, string? characterName, DateTime nowUtc)
    {
        var acctKey = string.IsNullOrWhiteSpace(loginName) ? null : NormalizeTargetKey(BanTargetKind.AccountMute, loginName);
        var charKey = string.IsNullOrWhiteSpace(characterName) ? null : NormalizeTargetKey(BanTargetKind.CharacterMute, characterName);
        if (acctKey is null && charKey is null)
        {
            return null;
        }

        lock (Gates.GetValue(backend, static _ => new()))
        {
            return this.LoadAllLocked()
                .Where(r => r.IsEnforcedAt(nowUtc))
                .Where(r => (acctKey is not null && r.Kind == BanTargetKind.AccountMute && r.TargetKey == acctKey)
                    || (charKey is not null && r.Kind == BanTargetKind.CharacterMute && r.TargetKey == charKey))
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
        }
    }

    /// <summary>Checks whether any active AccountBan remains for <paramref name="loginName"/> at <paramref name="nowUtc"/>.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="nowUtc">Current UTC timestamp.</param>
    /// <returns><c>true</c> if another active account ban exists.</returns>
    public bool HasOtherActiveAccountBan(string loginName, DateTime nowUtc)
    {
        var key = NormalizeTargetKey(BanTargetKind.AccountBan, loginName);
        lock (Gates.GetValue(backend, static _ => new()))
        {
            return this.LoadAllLocked().Any(r => r.Kind == BanTargetKind.AccountBan && r.TargetKey == key && r.IsEnforcedAt(nowUtc));
        }
    }

    /// <summary>Checks whether <paramref name="loginName"/> only has expired timed account ban records and no active account ban at <paramref name="nowUtc"/>.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="nowUtc">Current UTC timestamp.</param>
    /// <returns><c>true</c> if timed account ban records exist and all have expired.</returns>
    public bool HasExpiredAccountBanOnly(string loginName, DateTime nowUtc) =>
        this.HasExpiredOnlyBan(BanTargetKind.AccountBan, loginName, nowUtc);

    /// <summary>
    /// Checks whether a character's "banned" status came from a timed character ban which has expired, so it no longer
    /// counts. A timed ban issued now doesn't set the status at all (its record is enforced on its own), so this is only
    /// true for an expired timed ban of the earlier kind, which did. A "banned" status set by hand, or by a permanent
    /// ban, stays in force even after an unrelated timed ban ran out.
    /// </summary>
    /// <param name="characterName">Character name.</param>
    /// <param name="nowUtc">Current UTC timestamp.</param>
    /// <returns><c>true</c> if the character's banned status is left over from an expired timed ban.</returns>
    public bool HasExpiredCharacterBanOnly(string characterName, DateTime nowUtc) =>
        this.HasExpiredOnlyBan(BanTargetKind.CharacterBan, characterName, nowUtc, static r => !r.LeavesCharacterStatus);

    private static string NormalizeTargetKey(BanTargetKind kind, string target)
    {
        var prefix = kind switch
        {
            BanTargetKind.AccountBan or BanTargetKind.AccountMute => "acct:",
            BanTargetKind.CharacterBan or BanTargetKind.CharacterMute => "char:",
            BanTargetKind.NetworkBan => "ip:",
            BanTargetKind.DeviceBan => "dev:",
            _ => "other:",
        };
        return prefix + target.Trim().ToLowerInvariant();
    }

    private static string NormalizeIdentity(string value, bool isNetwork)
    {
        var trimmed = value.Trim();
        if (isNetwork && System.Net.IPAddress.TryParse(trimmed, out var ip))
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            return ip.ToString().ToLowerInvariant();
        }

        return trimmed.ToLowerInvariant();
    }

    private static string HashIdentity(string saltHex, string prefix, string normalized)
    {
        var keyBytes = Encoding.UTF8.GetBytes(saltHex);
        var payloadBytes = Encoding.UTF8.GetBytes(prefix + normalized);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, payloadBytes)).ToLowerInvariant();
    }

    private static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    private bool HasExpiredOnlyBan(BanTargetKind kind, string target, DateTime nowUtc, Func<BanRecord, bool>? setsStatus = null)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var key = NormalizeTargetKey(kind, target);
        lock (Gates.GetValue(backend, static _ => new()))
        {
            var matching = this.LoadAllLocked().Where(r => r.Kind == kind && r.TargetKey == key).ToList();
            if (matching.Count == 0 || matching.Any(r => r.IsEnforcedAt(nowUtc)))
            {
                return false;
            }

            return matching.Any(r => r.Active && r.ExpiresAtUtc is { } exp && exp <= nowUtc && (setsStatus is null || setsStatus(r)));
        }
    }

    private string? TryLoadSaltFailClosedLocked(bool requireIfAnyBanExists)
    {
        var existing = this.FindRow(SaltFeature, SaltScopeId);
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.PayloadJson))
        {
            return JsonSerializer.Deserialize<SaltRow>(existing.PayloadJson, JsonOptions)?.Salt;
        }

        if (requireIfAnyBanExists)
        {
            var hasSaltedRows = this.LoadAllLocked().Any(r => r.Kind is BanTargetKind.NetworkBan or BanTargetKind.DeviceBan);
            if (hasSaltedRows)
            {
                throw new InvalidDataException("Moderation network/device salt is missing while salted ban rows exist.");
            }
        }

        return null;
    }

    private string EnsureOrLoadSaltLocked(DateTime nowUtc)
    {
        if (this.TryLoadSaltFailClosedLocked(requireIfAnyBanExists: true) is { } existing)
        {
            return existing;
        }

        var created = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var row = new HnmoFeatureRow(SaltFeature, SaltScopeId, JsonSerializer.Serialize(new SaltRow(created), JsonOptions), nowUtc);
        backend.UpsertBatch([row], []);
        return created;
    }

    private List<BanRecord> LoadAllLocked()
    {
        var rows = backend.LoadAll(BanFeature);
        var list = new List<BanRecord>(rows.Count);
        foreach (var row in rows)
        {
            var parsed = JsonSerializer.Deserialize<BanRecord>(row.PayloadJson, JsonOptions);
            if (parsed is not null)
            {
                list.Add(parsed);
            }
        }

        return list;
    }

    private HnmoFeatureRow? FindRow(string feature, string scopeId) =>
        backend.LoadAll(feature).SingleOrDefault(row => row.ScopeId == scopeId);

    private sealed record SaltRow(string Salt);
}
