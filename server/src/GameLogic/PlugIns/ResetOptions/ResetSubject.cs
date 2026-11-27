// <copyright file="ResetSubject.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A character which is checked for a reset or relife.
/// </summary>
public sealed class ResetSubject
{
    /// <summary>Gets or sets the character's id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the character class number.</summary>
    public int Class { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the resets.</summary>
    public int Resets { get; set; }

    /// <summary>Gets or sets the zen in the bag.</summary>
    public long Zen { get; set; }

    /// <summary>Gets or sets the account's coins.</summary>
    public IReadOnlyDictionary<CashShopCoinType, int> Coins { get; set; } = new Dictionary<CashShopCoinType, int>();

    /// <summary>Gets or sets the account's VIP value.</summary>
    public int VipLevel { get; set; }

    /// <summary>Gets or sets how many pieces of an item the bag has; <c>null</c> counts none.</summary>
    public Func<ResetItemCost, int>? CountItems { get; set; }
}
