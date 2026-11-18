// <copyright file="GiftBoxAudience.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

/// <summary>Resolves a reviewed one/list/all/recent audience without changing or guessing account identity.</summary>
public static class GiftBoxAudience
{
    /// <summary>Resolves exact logins or an explicitly selected all-accounts audience.</summary>
    /// <param name="accounts">The authoritative persisted account and live-activity snapshot.</param>
    /// <param name="pastedLogins">One or pasted logins; null explicitly means all, while blank is refused.</param>
    /// <param name="activeDays">An optional recent-activity filter, in elapsed UTC days.</param>
    /// <param name="nowUtc">The authoritative UTC review instant.</param>
    /// <returns>A detached, stable recipient snapshot, bounded to one atomic send.</returns>
    /// <exception cref="ArgumentException">The selection is invalid or contains any unknown exact login.</exception>
    public static IReadOnlyList<GiftBoxRecipient> Resolve(IEnumerable<GiftBoxRecipient> accounts, string? pastedLogins, int? activeDays, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        if (nowUtc.Kind != DateTimeKind.Utc || activeDays is < 1 or > 3650 || pastedLogins is { Length: > 200000 })
        {
            throw new ArgumentException("The gift box audience selection is invalid.");
        }

        var source = accounts.ToArray();
        if (source.Any(account => account.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(account.Login)
            || account.LastActiveUtc is { Kind: not DateTimeKind.Utc })
            || source.Select(account => account.AccountId).Distinct().Count() != source.Length
            || source.Select(account => account.Login).Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            throw new ArgumentException("The gift box account snapshot is invalid.", nameof(accounts));
        }

        IEnumerable<GiftBoxRecipient> selected = source;
        if (pastedLogins is not null)
        {
            var known = source.Select(account => account.Login).ToHashSet(StringComparer.Ordinal);
            var requested = pastedLogins.Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            if (requested.Count == 0 || requested.Any(login => !known.Contains(login)))
            {
                throw new ArgumentException("Supply exact existing account logins; the whole selection was refused.", nameof(pastedLogins));
            }

            selected = source.Where(account => requested.Contains(account.Login));
        }

        if (activeDays is { } days)
        {
            var cutoff = nowUtc.AddDays(-days);
            selected = selected.Where(account => account.Online || (account.LastActiveUtc is { } last && last >= cutoff && last <= nowUtc));
        }

        var result = selected.OrderBy(account => account.AccountId).ToArray();
        if (result.Length > 10000)
        {
            throw new ArgumentException("The reviewed audience exceeds one atomic send.", nameof(accounts));
        }

        return result;
    }
}
