// <copyright file="MerchantShopRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using HNMO.DataModel.Configuration;

/// <summary>The NPC and its actual map spawn locations in the shop list.</summary>
/// <param name="Merchant">The merchant definition.</param>
/// <param name="Locations">The map names/numbers and coordinates.</param>
/// <param name="Maps">The names of the maps the merchant stands on, for the map filter.</param>
public sealed record MerchantShopRow(MonsterDefinition Merchant, string Locations, IReadOnlyList<string> Maps)
{
    /// <summary>Gets the localized NPC name.</summary>
    public string Name => this.Merchant.Designation.ToString() ?? string.Empty;

    /// <summary>Gets the stocked entry count.</summary>
    public int Count => this.Merchant.MerchantStore?.Items.Count ?? 0;
}
