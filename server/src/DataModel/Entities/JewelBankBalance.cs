// <copyright file="JewelBankBalance.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Entities;

/// <summary>
/// The count of one jewel kind an account keeps in the Jewel Bank.
/// </summary>
/// <remarks>
/// Changed only by relative, conditional updates in the same database transaction as the bag or the receipt of the
/// operation (<see cref="AssetOperation"/>), never by saving a loaded value back: the game server, the website and the
/// admin panel change it from different persistence contexts. Like <see cref="CashShopCoinGrant"/>, it isn't part of
/// the <see cref="Account"/> aggregate and refers to its account by identifier.
/// </remarks>
[AggregateRoot]
public class JewelBankBalance
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the identifier of the account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the key of the jewel kind, as configured in the Jewel Bank plug-in.</summary>
    public string JewelKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the count; never below zero.</summary>
    public int Count { get; set; }
}
