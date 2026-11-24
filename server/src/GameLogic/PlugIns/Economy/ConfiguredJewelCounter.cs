// <copyright file="ConfiguredJewelCounter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlayerShopCurrencies;

/// <summary>Counts real pieces using the world's jewel mix definitions and optional configured bank entries.</summary>
internal sealed class ConfiguredJewelCounter
{
    private readonly HashSet<(byte Group, short Number)> _bundles = [];
    private readonly HashSet<(byte Group, short Number)> _singles;

    /// <summary>Initializes a new instance of the <see cref="ConfiguredJewelCounter"/> class.</summary>
    /// <param name="mixes">The installed jewel mix definitions.</param>
    /// <param name="bankJewels">Additional installed jewel definitions from the bank.</param>
    internal ConfiguredJewelCounter(IEnumerable<JewelMix> mixes, IEnumerable<(byte Group, short Number)> bankJewels)
    {
        this._singles = bankJewels.ToHashSet();
        foreach (var mix in mixes)
        {
            if (mix.SingleJewel is { } single)
            {
                this._singles.Add((single.Group, single.Number));
            }

            if (mix.MixedJewel is { } bundle)
            {
                this._bundles.Add((bundle.Group, bundle.Number));
            }
        }
    }

    /// <summary>Determines whether this account contributes to public economy totals.</summary>
    /// <param name="account">The persisted account.</param>
    /// <returns>Whether the account is a player with no persisted ban.</returns>
    internal static bool IsEligible(Account account)
        => !account.IsBot && !account.IsTemplate
           && account.State is not (AccountState.Banned or AccountState.TemporarilyBanned)
           && !string.IsNullOrWhiteSpace(account.LoginName);

    /// <summary>Counts the pieces represented by a known stored item.</summary>
    /// <param name="item">The stored item from an inventory or vault.</param>
    /// <returns>Jewel pieces for the stored item, or zero for unknown or invalid items.</returns>
    internal int Count(Item item)
        => item.Definition is { } definition
            ? this.Count(definition.Group, definition.Number, item.Level, item.IsStackable() ? item.Durability : 1)
            : 0;

    /// <summary>Counts pieces from an inventory or activity record by the configured item identity.</summary>
    /// <param name="group">The current item group.</param>
    /// <param name="number">The current item number.</param>
    /// <param name="level">The item's level.</param>
    /// <param name="quantity">The real transferred quantity.</param>
    /// <returns>The recorded number of pieces.</returns>
    internal int Count(byte group, short number, byte level, double quantity)
    {
        if (quantity < 1 || !double.IsFinite(quantity))
        {
            return 0;
        }

        if (this._bundles.Contains((group, number)))
        {
            return level <= ItemShopCurrencyExchange.MaximumBundleLevel ? (level + 1) * ItemShopCurrencyExchange.PiecesPerBundleLevel : 0;
        }

        return this._singles.Contains((group, number)) ? (int)Math.Min(int.MaxValue, Math.Floor(quantity)) : 0;
    }
}
