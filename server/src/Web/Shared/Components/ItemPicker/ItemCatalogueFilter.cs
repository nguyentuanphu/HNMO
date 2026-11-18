// <copyright file="ItemCatalogueFilter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Components.ItemPicker;

using System.Globalization;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;

/// <summary>Reusable catalogue filtering over actual definition groups and base item requirements.</summary>
public sealed class ItemCatalogueFilter
{
    /// <summary>Gets or sets the English/Vietnamese name or group:number search.</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>Gets or sets the group, or -1 for all groups.</summary>
    public int Category { get; set; } = -1;

    /// <summary>Gets or sets the qualified class number, or -1 for all classes.</summary>
    public int ClassNumber { get; set; } = -1;

    /// <summary>Gets or sets the lowest base required level.</summary>
    public int MinimumLevel { get; set; }

    /// <summary>Gets or sets the highest base required level.</summary>
    public int MaximumLevel { get; set; } = 1000;

    /// <summary>Gets or sets a value indicating whether definitions already stocked are excluded.</summary>
    public bool OnlyMissing { get; set; }

    /// <summary>Gets or sets the sort: name, group or level.</summary>
    public string Sort { get; set; } = "name";

    /// <summary>Gets the base required level rather than the unrelated drop level.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The level required by an unupgraded item.</returns>
    public static int RequiredLevel(ItemDefinition definition) => definition.Requirements
        .Where(r => r.Attribute?.Id == Stats.Level.Id && r.MinimumItemLevel.GetValueOrDefault() == 0)
        .Select(r => r.MinimumValue).DefaultIfEmpty(0).Max();

    /// <summary>Gets an explicit Vietnamese name, without inventing a missing translation.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The Vietnamese text, or an empty string.</returns>
    public static string VietnameseName(ItemDefinition definition) => definition.Name.GetTranslation(CultureInfo.GetCultureInfo("vi"), false) ?? string.Empty;

    /// <summary>Filters and sorts definitions, without touching any item or persistence state.</summary>
    /// <param name="definitions">All definitions.</param>
    /// <param name="stocked">Definition keys already in the shop.</param>
    /// <returns>The matching catalogue.</returns>
    public List<ItemDefinition> Apply(IEnumerable<ItemDefinition> definitions, ISet<(byte Group, short Number)> stocked)
    {
        var search = this.Search.Trim();
        var query = definitions.Where(d => this.Category < 0 || d.Group == this.Category)
            .Where(d => this.ClassNumber < 0 || d.QualifiedCharacters.Count == 0 || d.QualifiedCharacters.Any(c => c.Number == this.ClassNumber))
            .Where(d => !this.OnlyMissing || !stocked.Contains((d.Group, d.Number)))
            .Where(d => RequiredLevel(d) >= this.MinimumLevel && RequiredLevel(d) <= this.MaximumLevel);
        if (search.Length > 0)
        {
            query = query.Where(d => d.Name.ValueInNeutralLanguage.Contains(search, StringComparison.OrdinalIgnoreCase)
                || VietnameseName(d).Contains(search, StringComparison.OrdinalIgnoreCase)
                || FormattableString.Invariant($"{d.Group}:{d.Number}").Equals(search, StringComparison.OrdinalIgnoreCase));
        }

        return (this.Sort switch
        {
            "group" => query.OrderBy(d => d.Group).ThenBy(d => d.Number),
            "level" => query.OrderBy(RequiredLevel).ThenBy(d => d.Name.ValueInNeutralLanguage),
            _ => query.OrderBy(d => d.Name.ValueInNeutralLanguage, StringComparer.CurrentCultureIgnoreCase),
        }).ToList();
    }
}
