// <copyright file="ShopChangeSummary.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>Counts the independent changes in a shop draft.</summary>
/// <param name="Added">New entries.</param>
/// <param name="Removed">Removed entries.</param>
/// <param name="Moved">Existing entries moved to another slot.</param>
/// <param name="Updated">Existing entries with changed properties.</param>
public sealed record ShopChangeSummary(int Added, int Removed, int Moved, int Updated)
{
    /// <summary>Gets a value indicating whether any change exists.</summary>
    public bool HasChanges => this.Added + this.Removed + this.Moved + this.Updated > 0;
}
