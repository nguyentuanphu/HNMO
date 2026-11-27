// <copyright file="PlayerInMemoryContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.InMemory;

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HNMO.Persistence.BasicModel;
using Nito.AsyncEx;

/// <summary>
/// In-memory context implementation for <see cref="IPlayerContext"/>.
/// </summary>
public class PlayerInMemoryContext : InMemoryContext, IPlayerContext
{
    // All contexts of one in-memory database share this lock.
    private static readonly ConditionalWeakTable<InMemoryRepositoryProvider, AsyncLock> LinkLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerInMemoryContext"/> class.
    /// </summary>
    /// <param name="provider">The manager which holds the memory repositories.</param>
    public PlayerInMemoryContext(InMemoryRepositoryProvider provider)
        : base(provider)
    {
    }

    /// <inheritdoc/>
    public async ValueTask<HNMO.DataModel.Entities.LetterBody?> GetLetterBodyByHeaderIdAsync(Guid headerId, CancellationToken cancellationToken = default)
    {
        var allLetters = await this.Provider.GetRepository<LetterBody>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allLetters.FirstOrDefault(body => body.Header.Id == headerId);
    }

    /// <inheritdoc/>
    public async ValueTask<HNMO.DataModel.Entities.AccountState?> AuthenticateAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        var account = allAccounts.FirstOrDefault(a => a.LoginName == loginName && BCrypt.Net.BCrypt.Verify(password, a.PasswordHash));
        return account?.State;
    }

    /// <inheritdoc/>
    public async ValueTask<HNMO.DataModel.Entities.Account?> GetAccountByLoginNameAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.FirstOrDefault(account => account.LoginName == loginName && BCrypt.Net.BCrypt.Verify(password, account.PasswordHash));
    }

    /// <inheritdoc/>
    public async ValueTask<HNMO.DataModel.Entities.Account?> GetAccountByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.FirstOrDefault(account => account.LoginName == loginName);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable<HNMO.DataModel.Entities.Account>> GetAccountsOrderedByLoginNameAsync(int skip, int count, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.OrderBy(a => a.LoginName).Skip(skip).Take(count);
    }

    /// <inheritdoc/>
    public async ValueTask<AccountStatistics> GetAccountStatisticsAsync(DateTime registeredSince, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        var accounts = allAccounts.Where(a => !a.IsTemplate && !a.IsBot).ToList();
        return new AccountStatistics(
            accounts.Count,
            accounts.Count(a => a.RegistrationDate >= registeredSince),
            accounts.Count(a => a.State is HNMO.DataModel.Entities.AccountState.Banned or HNMO.DataModel.Entities.AccountState.TemporarilyBanned),
            accounts.Sum(a => a.Characters.Count));
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<MiniGameRankingSummary>> GetMiniGameLeaderboardAsync(IReadOnlyCollection<Guid> miniGameIds, DateTime? since, int count, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        var characters = allAccounts
            .Where(a => !a.IsTemplate && !a.IsBot)
            .SelectMany(a => a.Characters)
            .ToDictionary(c => c.GetId());
        var entries = await this.Provider.GetRepository<MiniGameRankingEntry>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return entries
            .Where(e => e.MiniGame is { } miniGame && miniGameIds.Contains(miniGame.GetId()) && (since is null || e.Timestamp >= since))
            .Where(e => e.Character is not null)
            .GroupBy(e => e.Character!.GetId())
            .Where(g => characters.ContainsKey(g.Key))
            .Select(g =>
            {
                var character = characters[g.Key];
                return new MiniGameRankingSummary(g.Key, character.Name, character.CharacterClass?.GetId(), g.Sum(e => (long)e.Score), g.Max(e => e.Score), g.Count());
            })
            .OrderByDescending(s => s.TotalScore)
            .ThenByDescending(s => s.BestScore)
            .ThenBy(s => s.CharacterName)
            .Take(count)
            .ToList();
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable<HNMO.DataModel.Entities.Account>> SearchAccountsAsync(string searchTerm, int skip, int count, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts
            .Where(a => a.LoginName.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase)
                        || a.Characters.Any(c => c.Name.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase)))
            .OrderBy(a => a.LoginName)
            .Skip(skip)
            .Take(count);
    }

    /// <inheritdoc />
    public async ValueTask<(IReadOnlyList<DataModel.Entities.Account> Accounts, int Total)> SearchAccountPageAsync(string text, DataModel.Entities.AccountState? state, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var accounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        var matches = accounts.Where(a => (state == null || a.State == state)
            && (a.LoginName.Contains(text, StringComparison.OrdinalIgnoreCase) || a.EMail.Contains(text, StringComparison.OrdinalIgnoreCase)
                || a.Characters.Any(c => c.Name.Contains(text, StringComparison.OrdinalIgnoreCase)))).OrderBy(a => a.LoginName).ThenBy(a => a.GetId()).ToList();
        return (matches.Skip(offset).Take(limit).ToList(), matches.Count);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CanSaveLetterAsync(Interfaces.LetterHeader letterHeader, CancellationToken cancellationToken = default)
    {
        return true;
    }

    /// <inheritdoc />
    public async ValueTask<int> GetLetterCountAsync(string receiverName, CancellationToken cancellationToken = default)
    {
        var account = await this.GetAccountByCharacterNameAsync(receiverName, cancellationToken).ConfigureAwait(false);
        return account?.Characters.FirstOrDefault(c => c.Name == receiverName)?.Letters.Count ?? 0;
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.Account?> GetAccountByCharacterNameAsync(string characterName, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.FirstOrDefault(account => account.Characters.Any(c => c.Name == characterName));
    }

    /// <inheritdoc />
    public async ValueTask<Guid?> GetAccountIdByCharacterNameAsync(string characterName, CancellationToken cancellationToken = default)
    {
        var account = await this.GetAccountByCharacterNameAsync(characterName, cancellationToken).ConfigureAwait(false);
        return account?.GetId();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.CastleSiegePendingReward>> GetPendingCastleSiegeRewardsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var pendingRewards = await this.Provider.GetRepository<CastleSiegePendingReward>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return pendingRewards.Where(reward => reward.CharacterId == characterId).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.CashShopStorageItem>> GetCashShopStorageItemsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var items = await this.Provider.GetRepository<CashShopStorageItem>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return items
            .Where(item => item.AccountId == accountId)
            .OrderBy(item => item.AddedAt)
            .ThenBy(item => item.Id)
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.CashShopCoinGrant>> GetPendingCashShopCoinGrantsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var grants = await this.Provider.GetRepository<CashShopCoinGrant>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return grants
            .Where(grant => grant.AccountId == accountId && grant.AppliedAt is null)
            .OrderBy(grant => grant.CreatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.CashShopCoinGrant>> GetLatestCashShopCoinGrantsAsync(
        Guid accountId,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        var grants = await this.Provider.GetRepository<CashShopCoinGrant>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return grants
            .Where(grant => grant.AccountId == accountId)
            .OrderByDescending(grant => grant.CreatedAt)
            .Take(maximumCount)
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.CashShopCoinGrant>> GetCashShopCoinHistoryAsync(Guid accountId, int offset, int maximumCount, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumCount, 101);
        var grants = await this.Provider.GetRepository<CashShopCoinGrant>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return grants.Where(grant => grant.AccountId == accountId)
            .OrderByDescending(grant => grant.CreatedAt)
            .ThenByDescending(grant => grant.Id)
            .Skip(offset)
            .Take(maximumCount)
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.CashShopCoinGrant?> GetCashShopCoinGrantByReferenceAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        var grants = await this.Provider.GetRepository<CashShopCoinGrant>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return grants.FirstOrDefault(grant => grant.Reference == reference);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, int>> GetJewelBankBalancesAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var balances = await this.Provider.GetRepository<JewelBankBalance>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return balances.Where(balance => balance.AccountId == accountId && balance.Count > 0)
            .ToDictionary(balance => balance.JewelKey, balance => balance.Count, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.AssetOperation?> GetAssetOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operations = await this.Provider.GetRepository<AssetOperation>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return operations.FirstOrDefault(operation => operation.Id == operationId);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.AssetOperation>> GetPendingAssetOperationsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var operations = await this.Provider.GetRepository<AssetOperation>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return operations.Where(operation => operation.AccountId == accountId && operation.PendingCount > 0)
            .OrderBy(operation => operation.CreatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AssetOperationSummary>> GetRecentAssetOperationsAsync(int maximumCount, IReadOnlyCollection<string>? kinds = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCount, 1);
        var operations = await this.Provider.GetRepository<AssetOperation>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        var accounts = (await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false))
            .OfType<IIdentifiable>()
            .ToDictionary(a => a.Id, a => ((Account)a).LoginName);

        var kindFilter = kinds is { Count: > 0 } ? new HashSet<string>(kinds, StringComparer.OrdinalIgnoreCase) : null;
        return operations
            .Where(operation => kindFilter is null || kindFilter.Contains(operation.Kind))
            .OrderByDescending(operation => operation.CreatedAt)
            .ThenByDescending(operation => operation.Id)
            .Take(maximumCount)
            .Select(operation => new AssetOperationSummary(
                operation.Id,
                operation.AccountId,
                accounts.GetValueOrDefault(operation.AccountId) ?? operation.AccountId.ToString("N"),
                operation.Kind,
                operation.Detail,
                operation.PendingKey,
                operation.PendingCount,
                operation.Source,
                operation.CreatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<AssetCommitStatus> SaveChangesWithAssetOperationAsync(AssetOperationRequest request, CancellationToken cancellationToken = default)
    {
        // Everything is checked first and written after the save, under the lock all contexts of this database share:
        // the in-memory stand-in of the one transaction of the database.
        var linkLock = LinkLocks.GetValue(this.Provider, static _ => new AsyncLock());
        using var l = await linkLock.LockAsync(cancellationToken).ConfigureAwait(false);
        var operations = await this.Provider.GetRepository<AssetOperation>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (operations.FirstOrDefault(operation => operation.Id == request.OperationId) is { } earlier)
        {
            return earlier.PayloadHash == request.PayloadHash && earlier.AccountId == request.AccountId
                ? AssetCommitStatus.AlreadyCommitted
                : AssetCommitStatus.Conflict;
        }

        var balances = (await this.Provider.GetRepository<JewelBankBalance>().GetAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(balance => balance.AccountId == request.AccountId)
            .ToList();
        foreach (var delta in request.JewelDeltas)
        {
            var current = balances.FirstOrDefault(balance => balance.JewelKey == delta.JewelKey)?.Count ?? 0;
            if (current + delta.Delta < 0 || current + delta.Delta > delta.Capacity)
            {
                return AssetCommitStatus.Refused;
            }
        }

        var delivered = request.DeliveredOperationId is { } deliveredId && request.DeliveredCount > 0
            ? operations.FirstOrDefault(operation => operation.Id == deliveredId && operation.AccountId == request.AccountId)
            : null;
        if (request.DeliveredCount > 0 && (delivered is null || delivered.PendingCount < request.DeliveredCount))
        {
            return AssetCommitStatus.Refused;
        }

        if (!await this.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
        {
            return AssetCommitStatus.Refused;
        }

        foreach (var delta in request.JewelDeltas)
        {
            if (balances.FirstOrDefault(balance => balance.JewelKey == delta.JewelKey) is { } balance)
            {
                balance.Count += delta.Delta;
            }
            else
            {
                var created = this.CreateNew<DataModel.Entities.JewelBankBalance>();
                created.AccountId = request.AccountId;
                created.JewelKey = delta.JewelKey;
                created.Count = delta.Delta;
            }
        }

        if (delivered is not null)
        {
            delivered.PendingCount -= request.DeliveredCount;
        }

        var receipt = this.CreateNew<DataModel.Entities.AssetOperation>();
        receipt.Id = request.OperationId;
        receipt.AccountId = request.AccountId;
        receipt.Kind = request.Kind;
        receipt.PayloadHash = request.PayloadHash;
        receipt.Source = request.Source;
        receipt.Detail = request.Detail;
        receipt.PendingKey = request.PendingKey;
        receipt.PendingCount = Math.Max(0, request.PendingCount);
        receipt.CreatedAt = DateTime.UtcNow;
        if (request.FeatureState is { } fs)
        {
            var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(fs.StateFileKey);
            backend.EnsureSchema();
            backend.UpsertBatch([new HnmoFeatureRow(fs.Feature, fs.ScopeId, fs.PayloadJson, fs.UpdatedAtUtc, fs.ExpiresAtUtc)], []);
        }

        return AssetCommitStatus.Committed;
    }

    /// <inheritdoc />
    public void AcceptChangesOfCommittedOperation()
    {
        // The in-memory objects are always saved.
    }

    /// <inheritdoc />
    public async ValueTask<Guid?> GetAccountIdByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
    {
        var accounts = await this.Provider.GetRepository<Account>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return accounts.FirstOrDefault(account => account.LoginName == loginName)?.Id;
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.CharacterStatus?> GetCharacterStatusAsync(string characterName, CancellationToken cancellationToken = default)
    {
        var characters = await this.Provider.GetRepository<Character>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return characters.FirstOrDefault(character => character.Name == characterName)?.CharacterStatus;
    }

    /// <inheritdoc />
    public async ValueTask<DateTime?> GetAccountChatBanUntilAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var accounts = await this.Provider.GetRepository<Account>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return accounts.FirstOrDefault(account => account.Id == accountId)?.ChatBanUntil;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.GuildChatBinding>> GetGuildChatBindingsAsync(
        Guid guildId,
        Guid allianceMasterGuildId,
        CancellationToken cancellationToken = default)
    {
        var bindings = await this.Provider.GetRepository<GuildChatBinding>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return bindings
            .Where(binding => (binding.GuildId == guildId && binding.Scope == DataModel.Entities.GuildChatScope.Guild)
                              || (binding.GuildId == allianceMasterGuildId && binding.Scope == DataModel.Entities.GuildChatScope.Alliance))
            .Cast<DataModel.Entities.GuildChatBinding>()
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<(Guid AccountId, string? CharacterName, string? PreviousUserId)?> TryLinkAccountAsync(
        string provider,
        string codeHash,
        DateTime nowUtc,
        string externalUserId,
        string externalUserName,
        CancellationToken cancellationToken)
    {
        using var l = await LinkLocks.GetValue(this.Provider, _ => new AsyncLock()).LockAsync(cancellationToken).ConfigureAwait(false);
        if (await this.GetAccountExternalLinkByCodeAsync(provider, codeHash, cancellationToken).ConfigureAwait(false) is not { } link
            || link.CodeExpiresAt is not { } expiresAt || expiresAt < nowUtc)
        {
            return null;
        }

        if (await this.GetAccountExternalLinkByUserAsync(provider, externalUserId, cancellationToken).ConfigureAwait(false) is { } previous
            && previous.AccountId != link.AccountId)
        {
            await this.DeleteAsync(previous).ConfigureAwait(false);
        }

        var previousUserId = link.ExternalUserId;
        link.ExternalUserId = externalUserId;
        link.ExternalUserName = externalUserName;
        link.LinkedAt = nowUtc;
        link.CodeHash = null;
        link.CodeExpiresAt = null;
        await this.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (link.AccountId, link.CharacterName, previousUserId == externalUserId ? null : previousUserId);
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.AccountExternalLink?> GetAccountExternalLinkAsync(
        Guid accountId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var links = await this.Provider.GetRepository<AccountExternalLink>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return links.FirstOrDefault(link => link.AccountId == accountId && link.Provider == provider);
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.AccountExternalLink?> GetAccountExternalLinkByUserAsync(
        string provider,
        string externalUserId,
        CancellationToken cancellationToken = default)
    {
        var links = await this.Provider.GetRepository<AccountExternalLink>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return links.FirstOrDefault(link => link.Provider == provider && link.ExternalUserId == externalUserId);
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.AccountExternalLink?> GetAccountExternalLinkByCodeAsync(
        string provider,
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        var links = await this.Provider.GetRepository<AccountExternalLink>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return links.FirstOrDefault(link => link.Provider == provider && link.CodeHash == codeHash);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, Guid>> GetAccountIdsByCharacterNamesAsync(
        IReadOnlyCollection<string> characterNames,
        CancellationToken cancellationToken = default)
    {
        var accounts = await this.Provider.GetRepository<Account>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new Dictionary<string, Guid>();
        foreach (var account in accounts)
        {
            foreach (var character in account.Characters.Where(c => characterNames.Contains(c.Name)))
            {
                result[character.Name] = account.Id;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.AccountExternalLink>> GetAccountExternalLinksAsync(
        string provider,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken = default)
    {
        var links = await this.Provider.GetRepository<AccountExternalLink>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return links.Where(link => link.Provider == provider && accountIds.Contains(link.AccountId)).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.AccountExternalLink>> GetAccountExternalLinksAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var links = await this.Provider.GetRepository<AccountExternalLink>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return links.Where(link => link.AccountId == accountId).OrderBy(link => link.Provider).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.GensMember?> GetGensMemberAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var members = await this.Provider.GetRepository<GensMember>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return members.FirstOrDefault(member => member.CharacterId == characterId);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<CharacterSummary>> GetCharacterRankingAsync(
        Guid levelAttributeId,
        Guid masterLevelAttributeId,
        Guid resetsAttributeId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var accounts = await this.Provider.GetRepository<Account>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        return accounts
            .Where(account => !account.IsBot && !account.IsTemplate)
            .SelectMany(account => account.Characters)
            .Where(character => character.CharacterStatus == DataModel.Entities.CharacterStatus.Normal)
            .OrderByDescending(character => GetValue(character, resetsAttributeId))
            .ThenByDescending(character => GetValue(character, masterLevelAttributeId))
            .ThenByDescending(character => GetValue(character, levelAttributeId))
            .ThenByDescending(character => character.MasterExperience)
            .ThenByDescending(character => character.Experience)
            .Take(count)
            .Select(character => ToSummary(character, levelAttributeId, masterLevelAttributeId, resetsAttributeId))
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<CharacterSummary?> GetCharacterSummaryAsync(
        string characterName,
        Guid levelAttributeId,
        Guid masterLevelAttributeId,
        Guid resetsAttributeId,
        CancellationToken cancellationToken = default)
    {
        var accounts = await this.Provider.GetRepository<Account>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        return accounts
            .SelectMany(account => account.Characters)
            .Where(character => character.Name == characterName)
            .Select(character => ToSummary(character, levelAttributeId, masterLevelAttributeId, resetsAttributeId))
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.GensAbuse?> GetGensAbuseAsync(
        Guid killerId,
        Guid victimId,
        CancellationToken cancellationToken = default)
    {
        var abuses = await this.Provider.GetRepository<GensAbuse>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return abuses.FirstOrDefault(abuse => abuse.KillerId == killerId && abuse.VictimId == victimId);
    }

    private static float GetValue(DataModel.Entities.Character character, Guid attributeId)
        => character.Attributes.FirstOrDefault(a => a.Definition?.Id == attributeId)?.Value ?? 0;

    private static CharacterSummary ToSummary(DataModel.Entities.Character character, Guid levelAttributeId, Guid masterLevelAttributeId, Guid resetsAttributeId)
        => new(
            character.Name,
            (string?)character.CharacterClass?.Name ?? string.Empty,
            (int)GetValue(character, levelAttributeId),
            (int)GetValue(character, masterLevelAttributeId),
            (int)GetValue(character, resetsAttributeId));
}
