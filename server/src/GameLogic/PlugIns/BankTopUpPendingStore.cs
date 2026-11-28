// <copyright file="BankTopUpPendingStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// The top-ups players asked for (<see cref="BankTopUpPending"/>), in the state file, changed under its lock.
/// </summary>
public sealed class BankTopUpPendingStore
{
    /// <summary>How long a code shows as waiting.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>Waiting top-ups an account may have at once.</summary>
    public const int OpenPerAccount = 3;

    /// <summary>Waiting top-ups of all accounts at once.</summary>
    public const int OpenInAll = 1000;

    private const string SectionKey = "bankTopUps";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int KeptDone = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private BankTopUpPendingStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Why a top-up couldn't be created.</summary>
    public enum CreateRefusal
    {
        /// <summary>Created (or the same waiting one given again).</summary>
        None,

        /// <summary>The account has <see cref="OpenPerAccount"/> waiting top-ups.</summary>
        TooManyForAccount,

        /// <summary>The server has <see cref="OpenInAll"/> waiting top-ups.</summary>
        TooManyInAll,
    }

    /// <summary>Opens the store of the server's state file.</summary>
    /// <param name="filePath">The state file; null for the default.</param>
    /// <returns>The store.</returns>
    public static BankTopUpPendingStore Open(string? filePath = null)
    {
        var store = HnmoStateStore.Open(filePath ?? HnmoStateStore.StateFileName)
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-topups-{Guid.NewGuid():N}.json"));
        return new BankTopUpPendingStore(store);
    }

    /// <summary>The hash under which a browser's ticket is kept (the ticket itself never is).</summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The lower-case hex SHA-256.</returns>
    public static string HashTicket(string ticket) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));

    /// <summary>
    /// Finds a transfer code (prefix + 6 letters/digits) in a transfer message, however the bank wrote it
    /// ("HNMO7K3QX2", "hnmo 7k3qx2", "MBVCB.123.HNMO.7K3QX2.CT tu ...").
    /// </summary>
    /// <param name="message">The transfer message.</param>
    /// <param name="prefix">The transfer word.</param>
    /// <returns>The candidate codes in upper case, in order.</returns>
    public static IEnumerable<string> CodesIn(string? message, string prefix)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(prefix))
        {
            yield break;
        }

        var word = prefix.Trim().ToUpperInvariant();
        foreach (Match match in Regex.Matches(
                     message,
                     $@"(?<![A-Za-z0-9]){Regex.Escape(prefix.Trim())}[\s.\-_:]*([A-Za-z0-9]{{6}})(?![A-Za-z0-9])",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                     TimeSpan.FromMilliseconds(100)))
        {
            yield return word + match.Groups[1].Value.ToUpperInvariant();
        }
    }

    /// <summary>
    /// Creates a waiting top-up, or gives the same waiting one again when the account asks for the same amount
    /// at the same bank account.
    /// </summary>
    /// <param name="login">The account's login name (checked by the caller).</param>
    /// <param name="amountVnd">The amount.</param>
    /// <param name="coins">The coins it buys.</param>
    /// <param name="account">The host's bank account.</param>
    /// <param name="prefix">The transfer word.</param>
    /// <param name="ticket">The browser's ticket for following it.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The top-up, or why not.</returns>
    public (BankTopUpPending? TopUp, CreateRefusal Refusal) Create(string login, long amountVnd, int coins, BankTopUpAccount account, string prefix, string ticket, DateTime nowUtc)
    {
        var ticketHash = HashTicket(ticket);
        var key = login.Trim().ToLowerInvariant();
        return this.Mutate(items =>
        {
            var open = items.Where(p => p.IsOpen(nowUtc)).ToList();
            var same = open.FirstOrDefault(p => p.Login.Equals(key, StringComparison.OrdinalIgnoreCase)
                && p.AmountVnd == amountVnd && p.Bin == account.Bin && p.Number == account.Number.Trim());
            if (same is not null)
            {
                if (!same.TicketHashes.Contains(ticketHash) && same.TicketHashes.Count < 5)
                {
                    same.TicketHashes.Add(ticketHash);
                }

                return (true, (same, CreateRefusal.None));
            }

            if (open.Count(p => p.Login.Equals(key, StringComparison.OrdinalIgnoreCase)) >= OpenPerAccount)
            {
                return (false, ((BankTopUpPending?)null, CreateRefusal.TooManyForAccount));
            }

            if (open.Count >= OpenInAll)
            {
                return (false, ((BankTopUpPending?)null, CreateRefusal.TooManyInAll));
            }

            var word = prefix.Trim().ToUpperInvariant();
            string code;
            do
            {
                code = word + RandomNumberGenerator.GetString(CodeAlphabet, 6);
            }
            while (items.Any(p => p.Code == code));

            var created = new BankTopUpPending
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = code,
                Login = key,
                AmountVnd = amountVnd,
                Coins = coins,
                Bin = account.Bin,
                Number = account.Number.Trim(),
                CreatedUtc = nowUtc,
                ExpiresUtc = nowUtc + Lifetime,
                TicketHashes = [ticketHash],
            };
            items.Add(created);
            Trim(items);
            return (true, ((BankTopUpPending?)created, CreateRefusal.None));
        });
    }

    /// <summary>All top-ups, newest first.</summary>
    /// <returns>The top-ups.</returns>
    public List<BankTopUpPending> All() => this.Read(items => items.OrderByDescending(p => p.CreatedUtc).ToList());

    /// <summary>The top-up of an id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The top-up, or null.</returns>
    public BankTopUpPending? Find(string id) => this.Read(items => items.FirstOrDefault(p => p.Id == id));

    /// <summary>The top-up of a transfer code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The top-up, or null.</returns>
    public BankTopUpPending? FindByCode(string code) =>
        this.Read(items => items.FirstOrDefault(p => p.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)));

    /// <summary>The top-ups a browser follows.</summary>
    /// <param name="tickets">The browser's tickets (at most 10 are looked at).</param>
    /// <returns>The top-ups, newest first.</returns>
    public List<BankTopUpPending> ForTickets(IEnumerable<string> tickets)
    {
        var hashes = tickets.Where(t => t.Length is >= 16 and <= 100).Take(10).Select(HashTicket).ToHashSet();
        return this.Read(items => items.Where(p => p.TicketHashes.Any(hashes.Contains)).OrderByDescending(p => p.CreatedUtc).ToList());
    }

    /// <summary>Marks a top-up credited or rejected once: a top-up already done keeps its first outcome.</summary>
    /// <param name="id">The id.</param>
    /// <param name="status">Credited or rejected.</param>
    /// <param name="by">Who.</param>
    /// <param name="receivedVnd">The amount a service reported, if any.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The status before, or null when there's no such top-up.</returns>
    public BankTopUpPendingStatus? Finish(string id, BankTopUpPendingStatus status, string by, long? receivedVnd, DateTime nowUtc) =>
        this.Mutate(items =>
        {
            if (items.FirstOrDefault(p => p.Id == id) is not { } item)
            {
                return (false, (BankTopUpPendingStatus?)null);
            }

            var before = item.Status;

            // A transfer that arrives for a rejected top-up still credits it (the money is there).
            if (before == BankTopUpPendingStatus.Credited || (before == BankTopUpPendingStatus.Rejected && status == BankTopUpPendingStatus.Rejected))
            {
                return (false, (BankTopUpPendingStatus?)before);
            }

            item.Status = status;
            item.DoneUtc = nowUtc;
            item.DoneBy = by;
            item.ReceivedVnd = receivedVnd ?? item.ReceivedVnd;
            return (true, (BankTopUpPendingStatus?)before);
        });

    private static void Trim(List<BankTopUpPending> items)
    {
        var done = items.Where(p => p.Status != BankTopUpPendingStatus.Pending).OrderBy(p => p.CreatedUtc).ToList();
        foreach (var old in done.Take(Math.Max(0, done.Count - KeptDone)))
        {
            items.Remove(old);
        }
    }

    private TResult Read<TResult>(Func<List<BankTopUpPending>, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            return reader(this._store.ReadSection<List<BankTopUpPending>>(SectionKey, JsonOptions) ?? []);
        }
    }

    private TResult Mutate<TResult>(Func<List<BankTopUpPending>, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var items = this._store.ReadSection<List<BankTopUpPending>>(SectionKey, JsonOptions) ?? [];
            var (changed, result) = mutator(items);
            if (changed)
            {
                this._store.WriteSection(SectionKey, items, JsonOptions);
            }

            return result;
        }
    }
}
