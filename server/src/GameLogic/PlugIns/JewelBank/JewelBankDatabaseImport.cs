// <copyright file="JewelBankDatabaseImport.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.Security.Cryptography;
using System.Text;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// Moves the balances and the waiting website withdrawals of the Jewel Bank's JSON state file into the database.
/// </summary>
/// <remarks>
/// Each balance and each waiting withdrawal is one operation with an identifier derived from the account's identifier
/// and the entry, committed on its own: a second run, or a run after a stop in the middle, finds what was imported and
/// imports the rest, never twice. A waiting withdrawal was already taken from the JSON balance, so it becomes a delivery
/// only. The JSON file is left as it is (for audit); nothing writes it any more.
/// </remarks>
public static class JewelBankDatabaseImport
{
    /// <summary>The kind of an imported balance.</summary>
    public const string BalanceKind = "jewel-import-balance";

    /// <summary>
    /// Imports the JSON entries (of one account, or all).
    /// </summary>
    /// <param name="provider">The persistence context provider.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="store">The JSON state file of the Jewel Bank.</param>
    /// <param name="dryRun">With <c>true</c>, nothing is written; the report says what would be.</param>
    /// <param name="loginName">Only this account, or <c>null</c> for all.</param>
    /// <param name="accountId">The identifier of the account of <paramref name="loginName"/>, when the caller has it.</param>
    /// <returns>The report.</returns>
    public static async ValueTask<JewelBankImportReport> RunAsync(IPersistenceContextProvider provider, GameConfiguration configuration, JewelBankStore store, bool dryRun, string? loginName = null, Guid? accountId = null)
    {
        var (accounts, pending, checksum) = store.Read(data =>
        {
            var accountsCopy = data.Accounts
                .Where(a => loginName is null || string.Equals(a.Key, loginName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                .Select(a => (Login: a.Key, Balances: a.Value.Where(b => b.Value > 0).OrderBy(b => b.Key, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
            var pendingCopy = data.PendingWithdrawals
                .Where(p => p.Quantity > 0 && (loginName is null || string.Equals(p.LoginName, loginName, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p.Id, StringComparer.Ordinal)
                .Select(p => (p.Id, p.LoginName, p.JewelKey, p.Quantity))
                .ToList();
            var canonical = new StringBuilder();
            foreach (var (login, balances) in accountsCopy)
            {
                canonical.Append(login.ToLowerInvariant()).Append('|');
                canonical.AppendJoin(',', balances.Select(b => $"{b.Key}:{b.Value}")).Append('\n');
            }

            foreach (var p in pendingCopy)
            {
                canonical.Append("pending|").Append(p.Id).Append('|').Append(p.LoginName.ToLowerInvariant()).Append('|').Append(p.JewelKey).Append(':').Append(p.Quantity).Append('\n');
            }

            return (accountsCopy, pendingCopy, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))));
        });

        var report = new JewelBankImportReport { DryRun = dryRun, InputChecksum = checksum };
        using var context = provider.CreateNewPlayerContext(configuration);
        var accountIds = new Dictionary<string, Guid?>(StringComparer.OrdinalIgnoreCase);
        if (loginName is not null && accountId is { } knownId)
        {
            accountIds[loginName] = knownId;
        }

        async ValueTask<Guid?> AccountIdAsync(string login)
        {
            if (!accountIds.TryGetValue(login, out var id))
            {
                id = await context.GetAccountIdByLoginNameAsync(login).ConfigureAwait(false);
                accountIds[login] = id;
                if (id is null)
                {
                    report.UnknownAccounts.Add(login);
                }
            }

            return id;
        }

        foreach (var (login, balances) in accounts)
        {
            if (await AccountIdAsync(login).ConfigureAwait(false) is not { } id)
            {
                continue;
            }

            foreach (var (key, count) in balances)
            {
                report.JsonTotals[key] = report.JsonTotals.GetValueOrDefault(key) + count;
                var payload = $"import-balance|{key}:{count}";
                var request = new AssetOperationRequest(
                    OperationId(id, "balance", key),
                    id,
                    BalanceKind,
                    JewelBankPlugIn.PayloadHash(payload),
                    "import",
                    payload,
                    [new JewelBankDelta(key, count, int.MaxValue)]);
                await ApplyAsync(context, request, report, dryRun, $"{login} {key} {count}").ConfigureAwait(false);
            }
        }

        foreach (var (pendingId, login, key, quantity) in pending)
        {
            if (await AccountIdAsync(login).ConfigureAwait(false) is not { } id)
            {
                continue;
            }

            report.JsonPending[key] = report.JsonPending.GetValueOrDefault(key) + quantity;
            var payload = $"import-pending|{pendingId}|{key}:{quantity}";
            var request = new AssetOperationRequest(
                OperationId(id, "pending", pendingId),
                id,
                JewelBankPlugIn.WebsiteWithdrawKind,
                JewelBankPlugIn.PayloadHash(payload),
                "import",
                payload,
                [],
                key,
                quantity);
            await ApplyAsync(context, request, report, dryRun, $"{login} pending {pendingId} {key} {quantity}").ConfigureAwait(false);
        }

        return report;
    }

    /// <summary>The identifier of the import operation of one entry: the same on every run, different per account.</summary>
    /// <param name="accountId">The account's identifier.</param>
    /// <param name="what">The entry kind.</param>
    /// <param name="key">The entry key.</param>
    /// <returns>The identifier.</returns>
    internal static Guid OperationId(Guid accountId, string what, string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"hnmo-jewel-import|{accountId:N}|{what}|{key.ToLowerInvariant()}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static async ValueTask ApplyAsync(IPlayerContext context, AssetOperationRequest request, JewelBankImportReport report, bool dryRun, string line)
    {
        if (await context.GetAssetOperationAsync(request.OperationId).ConfigureAwait(false) is { } earlier)
        {
            if (earlier.PayloadHash == request.PayloadHash)
            {
                report.AlreadyImported++;
            }
            else
            {
                // The JSON entry changed after it was imported: nothing more is imported for it; an operator decides.
                report.Conflicts.Add(line);
            }

            return;
        }

        if (dryRun)
        {
            report.WouldImport.Add(line);
            return;
        }

        var status = await context.SaveChangesWithAssetOperationAsync(request).ConfigureAwait(false);
        switch (status)
        {
            case AssetCommitStatus.Committed:
                report.Imported.Add(line);
                break;
            case AssetCommitStatus.AlreadyCommitted:
                report.AlreadyImported++;
                break;
            default:
                report.Conflicts.Add($"{line} ({status})");
                break;
        }
    }
}
