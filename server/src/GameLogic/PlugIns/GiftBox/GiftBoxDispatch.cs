// <copyright file="GiftBoxDispatch.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;

/// <summary>Atomically freezes a host send and account-owned owed rewards in existing player-state rows.</summary>
/// <remarks>Authorization, recipient resolution, audit and actual delivery belong to the adapters.
/// This service never grants goods or marks a letter collected. It creates no save file.</remarks>
/// <param name="backend">The existing PostgreSQL player-state backend, or an isolated test backend.</param>
public sealed class GiftBoxDispatch(IHnmoPlayerStateRowBackend backend)
{
    /// <summary>The player-state feature containing immutable host send proofs.</summary>
    public const string SendFeature = "giftBox.send";

    /// <summary>The player-state feature containing individual account-owned owed letters.</summary>
    public const string LetterFeature = "giftBox.letter";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly ConditionalWeakTable<IHnmoPlayerStateRowBackend, object> Gates = new();

    /// <summary>Records one immutable host send and its resolved recipients, or replays the original send.</summary>
    /// <param name="id">The host operation identifier, retained across uncertain retries.</param>
    /// <param name="actor">The authenticated host identity; never a browser-supplied identity.</param>
    /// <param name="reason">The required private host reason, kept out of player letters.</param>
    /// <param name="accountIds">The already resolved recipient snapshot.</param>
    /// <param name="titleEn">The English title.</param>
    /// <param name="titleVi">The Vietnamese title.</param>
    /// <param name="messageEn">The English message.</param>
    /// <param name="messageVi">The Vietnamese message.</param>
    /// <param name="reward">The promised reward, copied before persistence.</param>
    /// <param name="nowUtc">The authoritative UTC send instant.</param>
    /// <param name="expiresAtUtc">An optional absolute UTC deadline, retained on retry.</param>
    /// <returns>The original recipient count; replay creates no additional letters.</returns>
    /// <exception cref="ArgumentException">A required field, recipient or reward is invalid.</exception>
    /// <exception cref="InvalidOperationException">An operation identifier was reused for different arguments.</exception>
    public int Send(Guid id, string actor, string reason, IEnumerable<Guid> accountIds, string titleEn, string titleVi, string messageEn, string messageVi, GiftCodeReward reward, DateTime nowUtc, DateTime? expiresAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        ArgumentNullException.ThrowIfNull(reward);
        var recipients = accountIds.Distinct().Order().ToArray();
        if (id == Guid.Empty || recipients.Length is < 1 or > 10000 || recipients.Contains(Guid.Empty)
            || !ValidText(actor, 256) || !ValidText(reason, 500) || !ValidText(titleEn, 100) || !ValidText(titleVi, 100)
            || !ValidText(messageEn, 2000) || !ValidText(messageVi, 2000)
            || nowUtc.Kind != DateTimeKind.Utc || expiresAtUtc is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException("The gift box send arguments are invalid.");
        }

        ValidateReward(reward);
        var request = new SendRequest(actor, reason, recipients, titleEn, titleVi, messageEn, messageVi, Clone(reward), expiresAtUtc);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions))));
        lock (Gates.GetValue(backend, _ => new()))
        {
            var key = id.ToString("N");
            var existing = this.FindRow(SendFeature, key);
            if (existing is not null)
            {
                var proof = JsonSerializer.Deserialize<SendProof>(existing.PayloadJson, JsonOptions)
                    ?? throw new InvalidOperationException("The gift box send proof is unreadable.");
                if (proof.Fingerprint != fingerprint)
                {
                    throw new InvalidOperationException("The gift box operation identifier belongs to another send.");
                }

                return proof.Recipients;
            }

            if (expiresAtUtc <= nowUtc)
            {
                throw new ArgumentException("The gift box deadline must be in the future.");
            }

            var proofRow = new HnmoFeatureRow(SendFeature, key, JsonSerializer.Serialize(new SendProof(fingerprint, recipients.Length, actor, reason, recipients), JsonOptions), nowUtc);
            var rows = new List<HnmoFeatureRow>(recipients.Length + 1) { proofRow };
            foreach (var accountId in recipients)
            {
                var letter = new GiftBoxLetter(id, accountId, titleEn, titleVi, messageEn, messageVi, request.Reward, nowUtc, expiresAtUtc);
                rows.Add(new(LetterFeature, $"{accountId:N}:{key}", JsonSerializer.Serialize(letter, JsonOptions), nowUtc));
            }

            // The backend atomically saves proof and letters. A thrown commit may have succeeded:
            // retain the stable id and inspect its proof on the next attempt.
            backend.UpsertBatch(rows, []);
            return recipients.Length;
        }
    }

    /// <summary>Reads detached letters for one account without consuming or hiding expired mail.</summary>
    /// <param name="accountId">The authenticated account identifier.</param>
    /// <returns>Newest first; private host identity/reason are not part of this contract.</returns>
    public IReadOnlyList<GiftBoxLetter> Read(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("A recipient account is required.", nameof(accountId));
        }

        lock (Gates.GetValue(backend, _ => new()))
        {
            var prefix = $"{accountId:N}:";
            return backend.LoadAll(LetterFeature).Where(row => row.ScopeId.StartsWith(prefix, StringComparison.Ordinal))
                .Select(row => JsonSerializer.Deserialize<GiftBoxLetter>(row.PayloadJson, JsonOptions)
                    ?? throw new InvalidOperationException("A gift box letter is unreadable."))
                .Where(letter => letter.AccountId == accountId).OrderByDescending(letter => letter.SentAtUtc).ThenBy(letter => letter.Id).ToArray();
        }
    }

    /// <summary>Reads the original frozen audience so a retry never silently picks a newer bulk cohort.</summary>
    /// <param name="sendId">The immutable host send identifier.</param>
    /// <returns>The detached original accounts, or null when this send has not been recorded.</returns>
    public IReadOnlyList<Guid>? ReadFrozenAudience(Guid sendId)
    {
        if (sendId == Guid.Empty)
        {
            throw new ArgumentException("A gift box send identifier is required.", nameof(sendId));
        }

        lock (Gates.GetValue(backend, _ => new()))
        {
            var key = sendId.ToString("N");
            var row = this.FindRow(SendFeature, key);
            if (row is null)
            {
                return null;
            }

            var proof = JsonSerializer.Deserialize<SendProof>(row.PayloadJson, JsonOptions)
                ?? throw new InvalidDataException("The original gift box audience proof is unreadable.");
            var accounts = proof.AccountIds;
            if (accounts is null)
            {
                // Compatibility with the published initial foundation, whose immutable letters still
                // contain the original audience. Incomplete/archived rows must never invent recipients.
                accounts = backend.LoadAll(LetterFeature).Where(letter => letter.ScopeId.EndsWith(":" + key, StringComparison.Ordinal))
                    .Select(letter => JsonSerializer.Deserialize<GiftBoxLetter>(letter.PayloadJson, JsonOptions)
                        ?? throw new InvalidDataException("An original gift box audience letter is unreadable."))
                    .Where(letter => letter.Id == sendId).Select(letter => letter.AccountId).Order().ToArray();
            }

            if (proof.Recipients is < 1 or > 10000 || accounts.Length != proof.Recipients
                || accounts.Contains(Guid.Empty) || accounts.Distinct().Count() != accounts.Length)
            {
                throw new InvalidDataException("The original gift box audience is incomplete; no new send may replace it.");
            }

            return accounts.Order().ToArray();
        }
    }

    /// <summary>Reads one owned letter without scanning unrelated mail on a query-capable backend.</summary>
    /// <param name="accountId">The authenticated account.</param>
    /// <param name="letterId">The immutable letter identifier.</param>
    /// <returns>The owned letter, or null.</returns>
    public GiftBoxLetter? ReadLetter(Guid accountId, Guid letterId)
    {
        if (accountId == Guid.Empty || letterId == Guid.Empty)
        {
            throw new ArgumentException("An account and letter identifier are required.");
        }

        lock (Gates.GetValue(backend, _ => new()))
        {
            var row = this.FindRow(LetterFeature, $"{accountId:N}:{letterId:N}");
            if (row is null)
            {
                return null;
            }

            var letter = JsonSerializer.Deserialize<GiftBoxLetter>(row.PayloadJson, JsonOptions)
                ?? throw new InvalidDataException("The owned gift box letter is unreadable.");
            if (letter.AccountId != accountId || letter.Id != letterId)
            {
                throw new InvalidDataException("The gift box letter does not match its account scope.");
            }

            return letter;
        }
    }

    /// <summary>Reads a bounded own-account inbox page without loading every account's messages.</summary>
    /// <param name="accountId">The authenticated account.</param>
    /// <param name="offset">The number of own letters to skip.</param>
    /// <param name="limit">The bounded row count, up to101 for a next-page sentinel.</param>
    /// <returns>The own letters, newest first.</returns>
    public IReadOnlyList<GiftBoxLetter> ReadPage(Guid accountId, int offset, int limit)
    {
        if (accountId == Guid.Empty || offset < 0 || limit is < 1 or > 101)
        {
            throw new ArgumentException("An account and bounded inbox page are required.");
        }

        lock (Gates.GetValue(backend, _ => new()))
        {
            var prefix = $"{accountId:N}:";
            var rows = backend is IHnmoPlayerStateRowQueryBackend query
                ? query.LoadScopePage(LetterFeature, prefix, offset, limit)
                : backend.LoadAll(LetterFeature).Where(row => row.ScopeId.StartsWith(prefix, StringComparison.Ordinal))
                    .OrderByDescending(row => row.UpdatedAtUtc).ThenBy(row => row.ScopeId, StringComparer.Ordinal).Skip(offset).Take(limit).ToArray();
            if (rows.Count > limit)
            {
                throw new InvalidDataException("The gift box backend exceeded its bounded page.");
            }

            var letters = rows.Select(row => JsonSerializer.Deserialize<GiftBoxLetter>(row.PayloadJson, JsonOptions)
                ?? throw new InvalidDataException("A gift box inbox letter is unreadable.")).ToArray();
            if (letters.Any(letter => letter.AccountId != accountId))
            {
                throw new InvalidDataException("The gift box backend returned another account's letter.");
            }

            return letters;
        }
    }

    private HnmoFeatureRow? FindRow(string feature, string scopeId) => backend is IHnmoPlayerStateRowQueryBackend query
        ? query.FindRow(feature, scopeId)
        : backend.LoadAll(feature).SingleOrDefault(row => row.ScopeId == scopeId);

    private static bool ValidText(string? text, int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length <= maximum && !text.Any(character => char.IsControl(character) && character is not '\n' and not '\r' and not '\t');

    private static GiftCodeReward Clone(GiftCodeReward reward) => JsonSerializer.Deserialize<GiftCodeReward>(JsonSerializer.Serialize(reward, JsonOptions), JsonOptions)!;

    private static void ValidateReward(GiftCodeReward reward)
    {
        if (!Enum.IsDefined(reward.CoinType) || reward.Coins is < 0 or > 1000000 || reward.Zen is < 0 or > 2000000000
            || reward.Items is null || reward.Items.Count > 50 || reward.IsEmpty
            || reward.Items.Any(item => item is null || item.Group is < 0 or > 15 || item.Number is < 0 or > 511
                || item.Level > 15 || item.Count is < 1 or > 50 || item.OptionLevel > 7 || item.ExcellentOptions > 63)
            || reward.Items.Sum(item => item.Count) > 50)
        {
            throw new ArgumentException("The gift box reward is invalid.", nameof(reward));
        }
    }

    private sealed record SendRequest(string Actor, string Reason, Guid[] AccountIds, string TitleEn, string TitleVi, string MessageEn, string MessageVi, GiftCodeReward Reward, DateTime? ExpiresAtUtc);

    private sealed record SendProof(string Fingerprint, int Recipients, string Actor, string Reason, Guid[]? AccountIds = null);
}
