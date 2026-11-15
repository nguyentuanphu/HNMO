// <copyright file="AccountSafetyEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Globalization;
using System.Net;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The actions which can need the second password.
/// </summary>
public enum SafetyAction
{
    /// <summary>Taking items or zen out of the vault.</summary>
    Vault,

    /// <summary>Starting or accepting a trade.</summary>
    Trade,

    /// <summary>Opening the personal store.</summary>
    PersonalStore,

    /// <summary>Dropping or selling a valuable item.</summary>
    ValuableItem,
}

/// <summary>
/// The result of a second password which was entered.
/// </summary>
public enum PinCheckResult
{
    /// <summary>It's right.</summary>
    Ok,

    /// <summary>It's wrong.</summary>
    Wrong,

    /// <summary>Too many wrong ones were entered: wait until the lockout ends.</summary>
    LockedOut,

    /// <summary>The account has no second password.</summary>
    NoPin,
}

/// <summary>
/// The result of a check of a second password, with the tries left and the end of a lockout.
/// </summary>
/// <param name="Result">The result.</param>
/// <param name="TriesLeft">The wrong tries left before the lockout.</param>
/// <param name="LockedOutUntilUtc">The end of the lockout.</param>
public readonly record struct PinCheck(PinCheckResult Result, int TriesLeft, DateTime? LockedOutUntilUtc);

/// <summary>
/// The result of locking or unlocking an item.
/// </summary>
public enum ItemLockResult
{
    /// <summary>The item is locked now.</summary>
    Locked,

    /// <summary>The item was locked already.</summary>
    AlreadyLocked,

    /// <summary>The item's asked unlock was cancelled: it stays locked.</summary>
    UnlockCancelled,

    /// <summary>The unlock was asked; it happens after the wait.</summary>
    UnlockStarted,

    /// <summary>The unlock was asked before; it still waits.</summary>
    UnlockWaiting,

    /// <summary>The item is unlocked now.</summary>
    Unlocked,

    /// <summary>The item isn't locked.</summary>
    NotLocked,

    /// <summary>The account locked as many items as it may.</summary>
    TooMany,

    /// <summary>The item lock is switched off.</summary>
    Off,
}

/// <summary>
/// The rules of HNMO's account safety, without the game: the second password (with its wrong-try limits), which
/// actions need it, the item locks with their unlock wait, and the login and trade history. Every method holds the
/// store's lock; the caller saves (<see cref="AccountSafetyStore.SaveIfChanged"/>).
/// </summary>
public sealed class AccountSafetyEngine
{
    private const int MaxLoginsKept = 500;
    private const int MaxTradesKept = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountSafetyEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public AccountSafetyEngine(AccountSafetyConfiguration configuration, AccountSafetyStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.TimeZone = new MissionClock(configuration.TimeZone, 0).TimeZone;
    }

    /// <summary>Gets or sets the BCrypt work factor of new second passwords (tests lower it).</summary>
    public static int WorkFactor { get; set; } = 8;

    /// <summary>Gets the configuration.</summary>
    public AccountSafetyConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public AccountSafetyStore Store { get; }

    /// <summary>Gets the time zone of the times shown (Sydney by default).</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>
    /// Reads a second password typed as text: exactly four digits (the game's vault PIN). Leading zeros count as in the
    /// game, which sends the PIN as a number.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The PIN as the game compares it; <c>null</c> when it isn't four digits.</returns>
    public static string? NormalizePin(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        return trimmed.Length == 4 && trimmed.All(char.IsAsciiDigit) ? int.Parse(trimmed, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// The PIN sent by the game as a number.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The PIN as text; <c>null</c> when it's out of range.</returns>
    public static string? PinOf(int value) => value is >= 0 and <= 9999 ? value.ToString(CultureInfo.InvariantCulture) : null;

    /// <summary>
    /// The stored form of a second password (a BCrypt hash).
    /// </summary>
    /// <param name="normalizedPin">The PIN from <see cref="NormalizePin"/> or <see cref="PinOf"/>.</param>
    /// <returns>The stored value.</returns>
    public static string HashPin(string normalizedPin) => BCrypt.Net.BCrypt.HashPassword(normalizedPin, WorkFactor);

    /// <summary>
    /// Gets a value indicating whether an account has a second password.
    /// </summary>
    /// <param name="stored">The account's vault password.</param>
    /// <returns><c>true</c>, if it has one.</returns>
    public static bool HasPin(string? stored) => !string.IsNullOrWhiteSpace(stored);

    /// <summary>
    /// Compares an entered PIN with the stored one (a hash, or the plain PIN saved by older versions).
    /// </summary>
    /// <param name="stored">The stored value.</param>
    /// <param name="normalizedPin">The entered PIN.</param>
    /// <returns><c>true</c>, if it matches.</returns>
    public static bool PinMatches(string? stored, string? normalizedPin)
    {
        if (!HasPin(stored) || string.IsNullOrEmpty(normalizedPin))
        {
            return false;
        }

        if (stored!.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(normalizedPin, stored);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        return string.Equals(stored.Trim(), normalizedPin, StringComparison.Ordinal);
    }

    /// <summary>
    /// Masks an address for the website: the first two parts of an IPv4 address, the first two groups of an IPv6 one.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The masked address.</returns>
    public static string Mask(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return "?";
        }

        if (IPAddress.TryParse(address, out var ip))
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var parts = ip.ToString().Split('.');
                return $"{parts[0]}.{parts[1]}.x.x";
            }

            var groups = ip.ToString().Split(':');
            return $"{groups[0]}:{(groups.Length > 1 ? groups[1] : string.Empty)}:x:x";
        }

        return address.Length <= 3 ? "x" : address[..3] + "…";
    }

    /// <summary>
    /// Gets a value indicating whether an item counts as valuable: excellent, ancient, or of the configured level.
    /// </summary>
    /// <param name="level">The item's level.</param>
    /// <param name="excellent">Whether it has excellent options.</param>
    /// <param name="ancient">Whether it's an ancient item.</param>
    /// <returns><c>true</c>, if it's valuable.</returns>
    public bool IsValuable(int level, bool excellent, bool ancient) =>
        excellent || ancient || (this.Configuration.ValuableMinimumLevel > 0 && level >= this.Configuration.ValuableMinimumLevel);

    /// <summary>
    /// Gets a value indicating whether an action needs the second password (when the account has one).
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns><c>true</c>, if it does.</returns>
    public bool IsRequired(SafetyAction action) => action switch
    {
        SafetyAction.Vault => this.Configuration.RequireForVault,
        SafetyAction.Trade => this.Configuration.RequireForTrade,
        SafetyAction.PersonalStore => this.Configuration.RequireForPersonalStore,
        _ => this.Configuration.RequireForValuableItems,
    };

    /// <summary>
    /// Formats a time in the configured time zone (Sydney).
    /// </summary>
    /// <param name="utc">The time.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <returns>The text.</returns>
    public string Local(DateTime utc, bool vietnamese) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), this.TimeZone)
            .ToString("ddd d MMM HH:mm", CultureInfo.GetCultureInfo(vietnamese ? "vi" : "en-AU"));

    /// <summary>
    /// Gets the lockout of an account's second password, if one runs.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="now">The time.</param>
    /// <returns>The end of the lockout; <c>null</c> when none runs.</returns>
    public DateTime? LockedOutUntil(string login, DateTime now)
    {
        lock (this.Store.Lock)
        {
            return this.Find(login) is { LockedOutUntilUtc: { } until } && until > now ? until : null;
        }
    }

    /// <summary>
    /// Checks an entered second password and counts it when it's wrong: after <see cref="AccountSafetyConfiguration.MaxWrongTries"/>
    /// wrong ones, the account can't enter one for <see cref="AccountSafetyConfiguration.LockoutMinutes"/>.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="stored">The account's stored second password.</param>
    /// <param name="normalizedPin">The entered PIN (<c>null</c> when it wasn't four digits: counts as wrong).</param>
    /// <param name="now">The time.</param>
    /// <returns>The result.</returns>
    public PinCheck CheckPin(string login, string? stored, string? normalizedPin, DateTime now)
    {
        var maximum = Math.Max(1, this.Configuration.MaxWrongTries);
        if (!HasPin(stored))
        {
            return new PinCheck(PinCheckResult.NoPin, maximum, null);
        }

        lock (this.Store.Lock)
        {
            var record = this.Record(login);
            if (record.LockedOutUntilUtc is { } until)
            {
                if (until > now)
                {
                    return new PinCheck(PinCheckResult.LockedOut, 0, until);
                }

                record.LockedOutUntilUtc = null;
                record.WrongTries = 0;
                this.Store.Changed();
            }

            if (PinMatches(stored, normalizedPin))
            {
                if (record.WrongTries > 0)
                {
                    record.WrongTries = 0;
                    this.Store.Changed();
                }

                return new PinCheck(PinCheckResult.Ok, maximum, null);
            }

            record.WrongTries++;
            this.Store.Changed();
            if (record.WrongTries >= maximum)
            {
                record.WrongTries = 0;
                record.LockedOutUntilUtc = now.AddMinutes(Math.Max(1, this.Configuration.LockoutMinutes));
                return new PinCheck(PinCheckResult.LockedOut, 0, record.LockedOutUntilUtc);
            }

            return new PinCheck(PinCheckResult.Wrong, maximum - record.WrongTries, null);
        }
    }

    /// <summary>
    /// Ends the lockout and forgets the wrong tries of an account (the admin panel).
    /// </summary>
    /// <param name="login">The login name.</param>
    public void ClearLockout(string login)
    {
        lock (this.Store.Lock)
        {
            if (this.Find(login) is { } record)
            {
                record.WrongTries = 0;
                record.LockedOutUntilUtc = null;
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Notes that the second password of an account was set, changed or cleared.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="now">The time.</param>
    public void PinChanged(string login, DateTime now)
    {
        lock (this.Store.Lock)
        {
            var record = this.Record(login);
            record.PinChangedAtUtc = now;
            record.WrongTries = 0;
            record.LockedOutUntilUtc = null;
            this.Store.Changed();
        }
    }

    /// <summary>
    /// Gets when the second password of an account was last changed.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <returns>The time; <c>null</c> when it wasn't changed with HNMO's account safety.</returns>
    public DateTime? PinChangedAt(string login)
    {
        lock (this.Store.Lock)
        {
            return this.Find(login)?.PinChangedAtUtc;
        }
    }

    /// <summary>
    /// Gets a value indicating whether an item is locked: it is, until its asked unlock time passed (then the lock goes).
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="now">The time.</param>
    /// <returns><c>true</c>, if it's locked.</returns>
    public bool IsLocked(string login, Guid itemId, DateTime now)
    {
        if (!this.Configuration.ItemLockEnabled || itemId == Guid.Empty)
        {
            return false;
        }

        lock (this.Store.Lock)
        {
            if (this.Find(login) is not { } record)
            {
                return false;
            }

            this.PruneLocks(record, now);
            return record.Locks.Any(l => l.ItemId == itemId);
        }
    }

    /// <summary>
    /// Gets the locked items of an account (the ones whose unlock time passed are gone).
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="now">The time.</param>
    /// <returns>Copies of the locks.</returns>
    public IReadOnlyList<SafetyItemLock> Locks(string login, DateTime now)
    {
        lock (this.Store.Lock)
        {
            if (this.Find(login) is not { } record)
            {
                return Array.Empty<SafetyItemLock>();
            }

            this.PruneLocks(record, now);
            return record.Locks.Select(l => new SafetyItemLock { ItemId = l.ItemId, Description = l.Description, Character = l.Character, LockedAtUtc = l.LockedAtUtc, UnlockAtUtc = l.UnlockAtUtc }).ToList();
        }
    }

    /// <summary>
    /// Locks an item; locking an item whose unlock was asked cancels the unlock.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="description">The item's text.</param>
    /// <param name="character">The character which has it (empty for the vault).</param>
    /// <param name="now">The time.</param>
    /// <returns>The result.</returns>
    public ItemLockResult Lock(string login, Guid itemId, string description, string character, DateTime now)
    {
        if (!this.Configuration.ItemLockEnabled)
        {
            return ItemLockResult.Off;
        }

        lock (this.Store.Lock)
        {
            var record = this.Record(login);
            this.PruneLocks(record, now);
            if (record.Locks.FirstOrDefault(l => l.ItemId == itemId) is { } existing)
            {
                if (existing.UnlockAtUtc is null)
                {
                    return ItemLockResult.AlreadyLocked;
                }

                existing.UnlockAtUtc = null;
                this.Store.Changed();
                return ItemLockResult.UnlockCancelled;
            }

            if (record.Locks.Count >= Math.Max(1, this.Configuration.MaxLockedItems))
            {
                return ItemLockResult.TooMany;
            }

            record.Locks.Add(new SafetyItemLock { ItemId = itemId, Description = description, Character = character, LockedAtUtc = now });
            this.Store.Changed();
            return ItemLockResult.Locked;
        }
    }

    /// <summary>
    /// Asks the unlock of an item (the caller checked the second password): at once without a wait, else after
    /// <see cref="AccountSafetyConfiguration.UnlockWaitHours"/>.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="now">The time.</param>
    /// <returns>The result and the unlock time.</returns>
    public (ItemLockResult Result, DateTime? UnlockAtUtc) Unlock(string login, Guid itemId, DateTime now)
    {
        lock (this.Store.Lock)
        {
            if (this.Find(login) is not { } record)
            {
                return (ItemLockResult.NotLocked, null);
            }

            this.PruneLocks(record, now);
            if (record.Locks.FirstOrDefault(l => l.ItemId == itemId) is not { } existing)
            {
                return (ItemLockResult.NotLocked, null);
            }

            if (existing.UnlockAtUtc is { } waiting)
            {
                return (ItemLockResult.UnlockWaiting, waiting);
            }

            if (this.Configuration.UnlockWaitHours <= 0 || !this.Configuration.ItemLockEnabled)
            {
                record.Locks.Remove(existing);
                this.Store.Changed();
                return (ItemLockResult.Unlocked, null);
            }

            existing.UnlockAtUtc = now.AddHours(this.Configuration.UnlockWaitHours);
            this.Store.Changed();
            return (ItemLockResult.UnlockStarted, existing.UnlockAtUtc);
        }
    }

    /// <summary>
    /// Removes the lock of an item at once (the admin panel).
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="itemId">The item's id.</param>
    /// <returns><c>true</c>, if it was locked.</returns>
    public bool RemoveLock(string login, Guid itemId)
    {
        lock (this.Store.Lock)
        {
            if (this.Find(login) is { } record && record.Locks.RemoveAll(l => l.ItemId == itemId) > 0)
            {
                this.Store.Changed();
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Records a login. A failed login is only recorded for accounts the store knows (which logged in before), so that
    /// made-up names don't fill the file.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="address">The address.</param>
    /// <param name="source">"game" or "website".</param>
    /// <param name="success">Whether the password was right.</param>
    /// <param name="now">The time.</param>
    /// <returns>The account's previous successful login (before this one), if any.</returns>
    public SafetyLoginEntry? RecordLogin(string login, string? address, string source, bool success, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        lock (this.Store.Lock)
        {
            var record = success ? this.Record(login) : this.Find(login);
            if (record is null)
            {
                return null;
            }

            var previous = record.Logins.FirstOrDefault(l => l.Success);
            record.Logins.Insert(0, new SafetyLoginEntry { AtUtc = now, Address = address ?? string.Empty, Source = source, Success = success });
            var oldest = now.AddDays(-Math.Max(1, this.Configuration.LoginHistoryDays));
            record.Logins.RemoveAll(l => l.AtUtc < oldest);
            if (record.Logins.Count > MaxLoginsKept)
            {
                record.Logins.RemoveRange(MaxLoginsKept, record.Logins.Count - MaxLoginsKept);
            }

            this.Store.Changed();
            return previous;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a login came from another address than the previous successful one.
    /// </summary>
    /// <param name="previous">The previous successful login.</param>
    /// <param name="address">The address of this login.</param>
    /// <returns><c>true</c>, if the address differs.</returns>
    public static bool IsNewAddress(SafetyLoginEntry? previous, string? address) =>
        previous is not null && !string.IsNullOrEmpty(previous.Address) && !string.IsNullOrEmpty(address)
        && !string.Equals(previous.Address, address, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the newest logins of an account.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="count">How many.</param>
    /// <returns>The logins, newest first.</returns>
    public IReadOnlyList<SafetyLoginEntry> Logins(string login, int count)
    {
        lock (this.Store.Lock)
        {
            return this.Find(login)?.Logins.Take(Math.Max(0, count)).Select(l => new SafetyLoginEntry { AtUtc = l.AtUtc, Address = l.Address, Source = l.Source, Success = l.Success }).ToList()
                ?? (IReadOnlyList<SafetyLoginEntry>)Array.Empty<SafetyLoginEntry>();
        }
    }

    /// <summary>
    /// Records a trade or a store sale of an account's character.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="entry">The entry; its time is set to <paramref name="now"/>.</param>
    /// <param name="now">The time.</param>
    public void RecordTrade(string login, SafetyTradeEntry entry, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        lock (this.Store.Lock)
        {
            var record = this.Record(login);
            entry.AtUtc = now;
            record.Trades.Insert(0, entry);
            var oldest = now.AddDays(-Math.Max(1, this.Configuration.TradeHistoryDays));
            record.Trades.RemoveAll(t => t.AtUtc < oldest);
            if (record.Trades.Count > MaxTradesKept)
            {
                record.Trades.RemoveRange(MaxTradesKept, record.Trades.Count - MaxTradesKept);
            }

            this.Store.Changed();
        }
    }

    /// <summary>
    /// Gets the newest trades of an account.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="count">How many.</param>
    /// <returns>The trades, newest first.</returns>
    public IReadOnlyList<SafetyTradeEntry> Trades(string login, int count)
    {
        lock (this.Store.Lock)
        {
            return this.Find(login)?.Trades.Take(Math.Max(0, count)).Select(t => new SafetyTradeEntry
            {
                AtUtc = t.AtUtc, Kind = t.Kind, Character = t.Character, Partner = t.Partner, PartnerLogin = t.PartnerLogin,
                Gave = t.Gave.ToList(), Got = t.Got.ToList(), ZenGave = t.ZenGave, ZenGot = t.ZenGot,
            }).ToList() ?? (IReadOnlyList<SafetyTradeEntry>)Array.Empty<SafetyTradeEntry>();
        }
    }

    /// <summary>
    /// Gets the login names the store knows, for the admin panel's search.
    /// </summary>
    /// <param name="filter">A part of the name; empty for all.</param>
    /// <param name="count">How many.</param>
    /// <returns>The names (lower case).</returns>
    public IReadOnlyList<string> KnownAccounts(string? filter, int count)
    {
        lock (this.Store.Lock)
        {
            var text = filter?.Trim().ToLowerInvariant() ?? string.Empty;
            return this.Store.Data.Accounts.Keys.Where(k => text.Length == 0 || k.Contains(text, StringComparison.Ordinal)).Order(StringComparer.Ordinal).Take(count).ToList();
        }
    }

    private static string Key(string login) => login.Trim().ToLowerInvariant();

    private SafetyAccountRecord? Find(string login) => this.Store.Data.Accounts.GetValueOrDefault(Key(login));

    private SafetyAccountRecord Record(string login)
    {
        var key = Key(login);
        if (!this.Store.Data.Accounts.TryGetValue(key, out var record))
        {
            record = new SafetyAccountRecord();
            this.Store.Data.Accounts[key] = record;
            this.Store.Changed();
        }

        return record;
    }

    private void PruneLocks(SafetyAccountRecord record, DateTime now)
    {
        if (record.Locks.RemoveAll(l => l.UnlockAtUtc is { } at && at <= now) > 0)
        {
            this.Store.Changed();
        }
    }
}
