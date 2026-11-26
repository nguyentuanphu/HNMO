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

    /// <summary>Gets the unfinished in-game deposits and withdrawals of an account.</summary>
    /// <param name="loginName">Account login.</param>
    /// <returns>The operations.</returns>
    public IReadOnlyList<JewelBankPendingOperation> PeekOperations(string loginName) =>
        this.Store.Read(data => data.PendingOperations
            .Where(o => string.Equals(o.LoginName, loginName, StringComparison.OrdinalIgnoreCase))
            .Select(Copy)
            .ToList());

    /// <summary>Writes a deposit before the bag without its jewels is saved; nothing is credited yet.</summary>
    /// <param name="loginName">Account login.</param>
    /// <param name="jewels">The jewels by key.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>The operation id.</returns>
    public string BeginDeposit(string loginName, IReadOnlyDictionary<string, int> jewels, DateTime utcNow)
    {
        var operation = new JewelBankPendingOperation
        {
            Id = Guid.NewGuid().ToString("N"),
            LoginName = loginName,
            Kind = JewelBankPendingOperation.DepositKind,
            Jewels = new Dictionary<string, int>(jewels, StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
        };
        this.Store.Mutate(data =>
        {
            data.PendingOperations.Add(operation);
            return (true, true);
        });
        return operation.Id;
    }

    /// <summary>
    /// Credits a deposit whose bag was saved and removes it, in one step. A jewel kind the bank has no room for any more
    /// (a website deposit filled it meanwhile) is not credited and is returned, to go back into the bag.
    /// </summary>
    /// <param name="operationId">The operation id.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>The refused jewels by key, or <c>null</c> when the operation was already finished.</returns>
    public Dictionary<string, int>? CommitDeposit(string operationId, DateTime utcNow) =>
        this.Store.Mutate(data =>
        {
            if (data.PendingOperations.FirstOrDefault(o => o.Id == operationId && o.Kind == JewelBankPendingOperation.DepositKind) is not { } operation)
            {
                return (false, (Dictionary<string, int>?)null);
            }

            data.PendingOperations.Remove(operation);
            var refused = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var map = data.Accounts[EnsureAccount(data, operation.LoginName)];
            foreach (var (jewelKey, count) in operation.Jewels)
            {
                var cfg = this.FindByKey(jewelKey);
                map.TryGetValue(jewelKey, out var current);
                if (cfg is null || count <= 0 || (long)current + count > Math.Max(1, cfg.MaxCapacity))
                {
                    if (count > 0)
                    {
                        refused[jewelKey] = count;
                    }

                    continue;
                }

                map[jewelKey] = current + count;
                AddLog(data, new JewelBankLogEntry
                {
                    AtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                    LoginName = operation.LoginName,
                    Action = "Deposit",
                    Detail = jewelKey,
                    Amount = count,
                    Source = "game",
                });
            }

            return (true, (Dictionary<string, int>?)refused);
        });

    /// <summary>
    /// Debits a withdrawal and writes it, in one step, before the bag with the jewels is saved.
    /// </summary>
    /// <param name="loginName">Account login.</param>
    /// <param name="jewelKey">Jewel key.</param>
    /// <param name="quantity">Count.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>The result and, when it is <see cref="JewelBankResult.Ok"/>, the operation id.</returns>
    public (JewelBankResult Result, string? OperationId) BeginWithdraw(string loginName, string jewelKey, int quantity, DateTime utcNow)
    {
        if (!this.Enabled)
        {
            return (JewelBankResult.Disabled, null);
        }

        if (this.FindByKey(jewelKey) is not { } cfg)
        {
            return (JewelBankResult.JewelNotAllowed, null);
        }

        if (quantity <= 0)
        {
            return (JewelBankResult.InvalidAmount, null);
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return this.Store.Mutate(data =>
        {
            var acctKey = NormalizeLogin(data, loginName);
            if (acctKey is null || !data.Accounts.TryGetValue(acctKey, out var map) || !map.TryGetValue(cfg.Key, out var current) || current < quantity)
            {
                return (false, (JewelBankResult.InsufficientBalance, (string?)null));
            }

            SetBalance(map, cfg.Key, current - quantity);
            var operation = new JewelBankPendingOperation
            {
                Id = Guid.NewGuid().ToString("N"),
                LoginName = loginName,
                Kind = JewelBankPendingOperation.WithdrawKind,
                Jewels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [cfg.Key] = quantity },
                CreatedAtUtc = now,
            };
            data.PendingOperations.Add(operation);
            AddLog(data, new JewelBankLogEntry { AtUtc = now, LoginName = loginName, Action = "Withdraw", Detail = cfg.Key, Amount = quantity, Source = "game" });
            return (true, (JewelBankResult.Ok, (string?)operation.Id));
        });
    }

    /// <summary>
    /// Keeps <paramref name="kept"/> jewels of each kind of a withdrawal and credits the rest back, in one step (the bag
    /// had room for fewer). With <paramref name="finish"/> the operation is also removed: <c>true</c> once the bag with the
    /// kept jewels is saved, or with nothing kept.
    /// </summary>
    /// <param name="operationId">The operation id.</param>
    /// <param name="kept">The count that stays withdrawn (0 credits everything back).</param>
    /// <param name="finish">Whether the operation is finished.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns><c>false</c> if the operation was already finished.</returns>
    public bool SettleWithdraw(string operationId, int kept, bool finish, DateTime utcNow) =>
        this.Store.Mutate(data =>
        {
            if (data.PendingOperations.FirstOrDefault(o => o.Id == operationId && o.Kind == JewelBankPendingOperation.WithdrawKind) is not { } operation)
            {
                return (false, false);
            }

            var map = data.Accounts[EnsureAccount(data, operation.LoginName)];
            foreach (var jewelKey in operation.Jewels.Keys.ToList())
            {
                var count = operation.Jewels[jewelKey];
                var back = Math.Max(0, count - Math.Max(0, kept));
                if (back > 0)
                {
                    // Credited back whatever the capacity: the jewels were in the bank a moment ago.
                    map.TryGetValue(jewelKey, out var current);
                    SetBalance(map, jewelKey, current + back);
                    AddLog(data, new JewelBankLogEntry
                    {
                        AtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                        LoginName = operation.LoginName,
                        Action = "Deposit",
                        Detail = jewelKey,
                        Amount = back,
                        Source = "rollback",
                    });
                }

                operation.Jewels[jewelKey] = count - back;
            }

            if (finish || operation.Jewels.Values.All(v => v <= 0))
            {
                data.PendingOperations.Remove(operation);
            }

            return (true, true);
        });

    /// <summary>Removes an operation without touching the balances (a deposit whose bag wasn't saved).</summary>
    /// <param name="operationId">The operation id.</param>
    public void DropOperation(string operationId) =>
        this.Store.Mutate(data => (data.PendingOperations.RemoveAll(o => o.Id == operationId) > 0, true));

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

    private static JewelBankPendingOperation Copy(JewelBankPendingOperation operation) => new()
    {
        Id = operation.Id,
        LoginName = operation.LoginName,
        Kind = operation.Kind,
        Jewels = new Dictionary<string, int>(operation.Jewels, StringComparer.OrdinalIgnoreCase),
        CreatedAtUtc = operation.CreatedAtUtc,
    };

    private static void SetBalance(Dictionary<string, int> map, string jewelKey, int value)
    {
        if (value <= 0)
        {
            map.Remove(jewelKey);
        }
        else
        {
            map[jewelKey] = value;
        }
    }

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

    private int GetBalance(string loginName, string jewelKey) =>
        this.Store.Read(data =>
        {
            var acct = NormalizeLogin(data, loginName);
            return acct is not null && data.Accounts.TryGetValue(acct, out var map) && map.TryGetValue(jewelKey, out var v) ? v : 0;
        });
}
