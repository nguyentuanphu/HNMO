// <copyright file="JewelBankEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

/// <summary>Outcome of a deposit or withdrawal operation.</summary>
public enum JewelBankResult
{
    /// <summary>Completed.</summary>
    Ok,

    /// <summary>Plug-in is disabled.</summary>
    Disabled,

    /// <summary>Jewel type is unknown or disabled.</summary>
    JewelNotAllowed,

    /// <summary>Requested quantity must be strictly positive.</summary>
    InvalidAmount,

    /// <summary>Account capacity limit reached.</summary>
    CapacityExceeded,

    /// <summary>Insufficient banked jewels.</summary>
    InsufficientBalance,
}

/// <summary>
/// Pure atomic engine for the Jewel Bank.
/// </summary>
public sealed class JewelBankEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JewelBankEngine"/> class.
    /// </summary>
    /// <param name="configuration">Configuration rules.</param>
    /// <param name="store">Persistent atomic store.</param>
    /// <param name="enabled">Whether plug-in is active.</param>
    public JewelBankEngine(JewelBankConfiguration configuration, JewelBankStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets the configuration.</summary>
    public JewelBankConfiguration Configuration { get; }

    /// <summary>Gets the atomic store.</summary>
    public JewelBankStore Store { get; }

    /// <summary>Gets a value indicating whether the Jewel Bank is active.</summary>
    public bool Enabled { get; }

    /// <summary>Finds an enabled jewel definition by key.</summary>
    /// <param name="key">Jewel key.</param>
    /// <returns>Definition or <c>null</c>.</returns>
    public JewelBankEntryConfig? FindByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return this.Configuration.Jewels.FirstOrDefault(j => j.Enabled && string.Equals(j.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Finds an enabled jewel definition matching item group and number.</summary>
    /// <param name="group">Item group.</param>
    /// <param name="number">Item number.</param>
    /// <returns>Definition or <c>null</c>.</returns>
    public JewelBankEntryConfig? FindByItem(byte group, short number) =>
        this.Configuration.Jewels.FirstOrDefault(j => j.Enabled && j.Group == group && j.Number == number);

    /// <summary>Gets all jewel balances for an account.</summary>
    /// <param name="loginName">Account login.</param>
    /// <returns>Balances keyed by jewel key.</returns>
    public Dictionary<string, int> GetBalances(string loginName) =>
        this.Store.Read(data =>
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var key = NormalizeLogin(data, loginName);
            if (key is null || !data.Accounts.TryGetValue(key, out var map))
            {
                return result;
            }

            foreach (var (j, count) in map)
            {
                if (count > 0)
                {
                    result[j] = count;
                }
            }

            return result;
        });

    /// <summary>Gets pending website withdrawals for an account.</summary>
    /// <param name="loginName">Account login.</param>
    /// <returns>Pending items.</returns>
    public IReadOnlyList<JewelBankPendingWithdrawal> PeekPending(string loginName) =>
        this.Store.Read(data => data.PendingWithdrawals
            .Where(p => string.Equals(p.LoginName, loginName, StringComparison.OrdinalIgnoreCase) && p.Quantity > 0)
            .ToList());

    /// <summary>Checks whether <paramref name="quantity"/> units of <paramref name="jewelKey"/> can fit without exceeding capacity.</summary>
    /// <param name="loginName">Account login.</param>
    /// <param name="jewelKey">Jewel key.</param>
    /// <param name="quantity">Desired quantity.</param>
    /// <returns><c>Ok</c> if room exists, else error.</returns>
    public JewelBankResult CanDeposit(string loginName, string jewelKey, int quantity)
    {
        if (!this.Enabled)
        {
            return JewelBankResult.Disabled;
        }

        var cfg = this.FindByKey(jewelKey);
        if (cfg is null)
        {
            return JewelBankResult.JewelNotAllowed;
        }

        if (quantity <= 0)
        {
            return JewelBankResult.InvalidAmount;
        }

        var current = this.GetBalance(loginName, cfg.Key);
        return (long)current + quantity > Math.Max(1, cfg.MaxCapacity)
            ? JewelBankResult.CapacityExceeded
            : JewelBankResult.Ok;
    }

    /// <summary>Deposits <paramref name="quantity"/> jewels atomically into the account's bank.</summary>
    /// <param name="loginName">Account login.</param>
    /// <param name="jewelKey">Jewel key.</param>
    /// <param name="quantity">Count.</param>
    /// <param name="source">Source (<c>game</c> or <c>website</c>).</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>Result code.</returns>
    public JewelBankResult Deposit(string loginName, string jewelKey, int quantity, string source, DateTime utcNow)
    {
        if (!this.Enabled)
        {
            return JewelBankResult.Disabled;
        }

        var cfg = this.FindByKey(jewelKey);
        if (cfg is null)
        {
            return JewelBankResult.JewelNotAllowed;
        }

        if (quantity <= 0)
        {
            return JewelBankResult.InvalidAmount;
        }

        var maxCap = Math.Max(1, cfg.MaxCapacity);
        return this.Store.Mutate(data =>
        {
            var acctKey = EnsureAccount(data, loginName);
            var map = data.Accounts[acctKey];
            map.TryGetValue(cfg.Key, out var current);
            if ((long)current + quantity > maxCap)
            {
                return (false, JewelBankResult.CapacityExceeded);
            }

            map[cfg.Key] = current + quantity;
            AddLog(data, new JewelBankLogEntry
            {
                AtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                LoginName = loginName,
                Action = "Deposit",
                Detail = cfg.Key,
                Amount = quantity,
                Source = source,
            });
            return (true, JewelBankResult.Ok);
        });
    }

    /// <summary>Atomically withdraws <paramref name="quantity"/> jewels from the account's bank.</summary>
    /// <param name="loginName">Account login.</param>
    /// <param name="jewelKey">Jewel key.</param>
    /// <param name="quantity">Count.</param>
    /// <param name="source">Origin.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <param name="enqueuePending">If <c>true</c>, adds a pending claim entry instead of immediate bag placement.</param>
    /// <returns>Result code.</returns>
    public JewelBankResult TryWithdraw(string loginName, string jewelKey, int quantity, string source, DateTime utcNow, bool enqueuePending = false)
    {
        if (!this.Enabled)
        {
            return JewelBankResult.Disabled;
        }

        var cfg = this.FindByKey(jewelKey);
        if (cfg is null)
        {
            return JewelBankResult.JewelNotAllowed;
        }

        if (quantity <= 0)
        {
            return JewelBankResult.InvalidAmount;
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return this.Store.Mutate(data =>
        {
            var acctKey = NormalizeLogin(data, loginName);
            if (acctKey is null || !data.Accounts.TryGetValue(acctKey, out var map) || !map.TryGetValue(cfg.Key, out var current) || current < quantity)
            {
                return (false, JewelBankResult.InsufficientBalance);
            }

            var next = current - quantity;
            if (next == 0)
            {
                map.Remove(cfg.Key);
            }
            else
            {
                map[cfg.Key] = next;
            }

            if (enqueuePending)
            {
                data.PendingWithdrawals.Add(new JewelBankPendingWithdrawal
                {
                    Id = Guid.NewGuid().ToString("N")[..12],
                    LoginName = loginName,
                    JewelKey = cfg.Key,
                    Quantity = quantity,
                    CreatedAtUtc = now,
                });
            }

            AddLog(data, new JewelBankLogEntry
            {
                AtUtc = now,
                LoginName = loginName,
                Action = "Withdraw",
                Detail = cfg.Key,
                Amount = quantity,
                Source = source,
            });
            return (true, JewelBankResult.Ok);
        });
    }

    /// <summary>Updates or deletes a pending withdrawal after partial/full delivery to bag.</summary>
    /// <param name="pendingId">Pending entry ID.</param>
    /// <param name="deliveredCount">Number placed into the bag.</param>
    public void ConsumePending(string pendingId, int deliveredCount)
    {
        if (deliveredCount <= 0)
        {
            return;
        }

        this.Store.Mutate(data =>
        {
            var entry = data.PendingWithdrawals.FirstOrDefault(p => p.Id == pendingId);
            if (entry is null)
            {
                return (false, false);
            }

            entry.Quantity -= deliveredCount;
            if (entry.Quantity <= 0)
            {
                data.PendingWithdrawals.Remove(entry);
            }

            return (true, true);
        });
    }

    /// <summary>Records an admin or website master-skill reset log line.</summary>
    /// <param name="loginName">Account login.</param>
    /// <param name="characterName">Character name.</param>
    /// <param name="refundedPoints">Master points refunded.</param>
    /// <param name="source">Origin (<c>game</c> or <c>website</c>).</param>
    /// <param name="utcNow">UTC timestamp.</param>
    public void RecordClearMaster(string loginName, string characterName, int refundedPoints, string source, DateTime utcNow)
    {
        this.Store.Mutate(data =>
        {
            AddLog(data, new JewelBankLogEntry
            {
                AtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                LoginName = loginName,
                Action = "ClearMaster",
                Detail = characterName,
                Amount = refundedPoints,
                Source = source,
            });
            return (true, true);
        });
    }

    private int GetBalance(string loginName, string jewelKey) =>
        this.Store.Read(data =>
        {
            var acct = NormalizeLogin(data, loginName);
            return acct is not null && data.Accounts.TryGetValue(acct, out var map) && map.TryGetValue(jewelKey, out var v) ? v : 0;
        });

    private static string? NormalizeLogin(JewelBankStoreData data, string loginName) =>
        data.Accounts.Keys.FirstOrDefault(k => string.Equals(k, loginName, StringComparison.OrdinalIgnoreCase));

    private static string EnsureAccount(JewelBankStoreData data, string loginName)
    {
        var existing = NormalizeLogin(data, loginName);
        if (existing is not null)
        {
            return existing;
        }

        data.Accounts[loginName] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return loginName;
    }

    private static void AddLog(JewelBankStoreData data, JewelBankLogEntry entry)
    {
        data.Log.Insert(0, entry);
        if (data.Log.Count > 500)
        {
            data.Log.RemoveRange(500, data.Log.Count - 500);
        }
    }
}
