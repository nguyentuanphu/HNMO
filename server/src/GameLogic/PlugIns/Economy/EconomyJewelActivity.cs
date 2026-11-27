// <copyright file="EconomyJewelActivity.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Persistence;

/// <summary>Jewel holdings and recorded hunting activity captured with an existing economy snapshot.</summary>
/// <param name="InventoryAndVaultPieces">Configured jewel pieces in eligible persisted inventories and vaults.</param>
/// <param name="LogWindowCapped">Whether the source exceeded the capture's row limit.</param>
/// <param name="Hunters">Private candidates to be filtered by current visibility before publishing.</param>
public sealed record EconomyJewelActivity(long InventoryAndVaultPieces, bool LogWindowCapped, IReadOnlyList<EconomyJewelHunter> Hunters)
{
    private const int MaximumHunterCandidates = 50;

    /// <summary>Gets the recorded balance in the active bank, or null for a capture without bank data.</summary>
    public long? BankPieces { get; init; }

    /// <summary>Sums the Jewel Bank balances (from the database) of the accounts which count for the economy.</summary>
    /// <param name="context">A player context.</param>
    /// <param name="accounts">The accounts.</param>
    /// <returns>The banked pieces.</returns>
    public static async ValueTask<long> SumBankPiecesAsync(IPlayerContext context, IEnumerable<Account> accounts)
    {
        var total = 0L;
        foreach (var account in accounts.Where(ConfiguredJewelCounter.IsEligible))
        {
            var balances = await context.GetJewelBankBalancesAsync(account.GetId()).ConfigureAwait(false);
            total += balances.Values.Where(pieces => pieces > 0).Sum(pieces => (long)pieces);
        }

        return total;
    }

    internal static EconomyJewelActivity Capture(
        IReadOnlyList<Account> accounts,
        IReadOnlyList<ActivityLogEntry> activity,
        bool logWindowCapped,
        ConfiguredJewelCounter counter)
    {
        var eligible = accounts.Where(ConfiguredJewelCounter.IsEligible).ToArray();
        var loginKeys = eligible.Select(account => account.LoginName).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(login => login, StringComparer.OrdinalIgnoreCase);
        var held = eligible.SelectMany(account => account.Characters.Select(character => character.Inventory).Append(account.Vault))
            .OfType<ItemStorage>().SelectMany(storage => storage.Items).Sum(item => (long)counter.Count(item));

        var remaining = new Dictionary<Guid, int>();
        var totals = new Dictionary<(string Account, string Character), long>();
        foreach (var row in activity.OrderBy(entry => entry.TimestampUtc).ThenBy(entry => entry.Id, StringComparer.Ordinal))
        {
            if (row.Kind == ActivityKind.Loot)
            {
                foreach (var item in row.AfterItems ?? [])
                {
                    var count = counter.Count(item.Group, item.Number, item.Level, item.Quantity);
                    if (item.Serial != Guid.Empty && count > 0)
                    {
                        remaining.TryAdd(item.Serial, count);
                    }
                }
            }
            else if (row.Kind == ActivityKind.Pickup
                     && row.BeforeItems is [var picked]
                     && row.PickupQuantity is { } quantity
                     && remaining.TryGetValue(picked.Serial, out var available))
            {
                var taken = Math.Min(available, counter.Count(picked.Group, picked.Number, picked.Level, quantity));
                remaining[picked.Serial] = available - taken;
                if (taken > 0 && loginKeys.TryGetValue(row.Account, out var login))
                {
                    var key = (login, row.Character);
                    totals[key] = totals.GetValueOrDefault(key) + taken;
                }
            }
        }

        var hunters = totals.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key.Character, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Account, StringComparer.Ordinal).Take(MaximumHunterCandidates)
            .Select(pair => new EconomyJewelHunter(pair.Key.Account, pair.Key.Character, pair.Value)).ToArray();
        return new(held, logWindowCapped, hunters);
    }
}
