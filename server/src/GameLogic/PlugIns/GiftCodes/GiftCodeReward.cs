// <copyright file="GiftCodeReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// The reward of a gift code: coins to the account, zen and items to the character.
/// </summary>
public class GiftCodeReward
{
    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the zen.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public List<GiftCodeItem> Items { get; set; } = new();

    /// <summary>Gets a value indicating whether the reward gives nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => this.Coins <= 0 && !this.HasGoods;

    /// <summary>Gets a value indicating whether the reward has zen or items (which go to a character's bag).</summary>
    [JsonIgnore]
    public bool HasGoods => this.Zen > 0 || this.Items.Any(i => i.Count > 0);
}
