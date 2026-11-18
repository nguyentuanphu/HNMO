// <copyright file="ItemClientMetadata.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ItemDefinitions;

using System;

/// <summary>
/// Web and desktop client metadata managed alongside <see cref="HNMO.DataModel.Configuration.Items.ItemDefinition"/>.
/// </summary>
public sealed class ItemClientMetadata
{
    /// <summary>
    /// Gets or sets the item group (0-15).
    /// </summary>
    public byte Group { get; set; }

    /// <summary>
    /// Gets or sets the item number within the group.
    /// </summary>
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the English name.
    /// </summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Vietnamese name.
    /// </summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the inventory cell width.
    /// </summary>
    public byte Width { get; set; } = 1;

    /// <summary>
    /// Gets or sets the inventory cell height.
    /// </summary>
    public byte Height { get; set; } = 1;

    /// <summary>
    /// Gets or sets the item drop / tier level shown on items.
    /// </summary>
    public byte DropLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum item enhancement level (0-15).
    /// </summary>
    public byte MaximumItemLevel { get; set; } = 15;

    /// <summary>
    /// Gets or sets the base durability at level 0.
    /// </summary>
    public byte Durability { get; set; } = 20;

    /// <summary>
    /// Gets or sets the base buy/worth price in zen.
    /// </summary>
    public int BuyPrice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether monsters can drop this item.
    /// </summary>
    public bool DropsFromMonsters { get; set; }

    /// <summary>
    /// Gets or sets the minimum character level requirement.
    /// </summary>
    public int LevelRequirement { get; set; }

    /// <summary>
    /// Gets or sets the minimum strength requirement.
    /// </summary>
    public int StrengthRequirement { get; set; }

    /// <summary>
    /// Gets or sets the minimum agility requirement.
    /// </summary>
    public int AgilityRequirement { get; set; }

    /// <summary>
    /// Gets or sets the minimum vitality requirement.
    /// </summary>
    public int VitalityRequirement { get; set; }

    /// <summary>
    /// Gets or sets the minimum energy requirement.
    /// </summary>
    public int EnergyRequirement { get; set; }

    /// <summary>
    /// Gets or sets the minimum leadership (command) requirement.
    /// </summary>
    public int LeadershipRequirement { get; set; }

    /// <summary>
    /// Gets or sets the glow color name (from <c>GlowColors.json</c>) or <c>#rrggbb</c> hex value.
    /// </summary>
    public string GlowColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets custom English tooltip lines (newline separated).
    /// </summary>
    public string TooltipTextEn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets custom Vietnamese tooltip lines (newline separated).
    /// </summary>
    public string TooltipTextVi { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional source item group whose 3D model is reused in the web game client.
    /// </summary>
    public int? ModelSourceGroup { get; set; }

    /// <summary>
    /// Gets or sets an optional source item number whose 3D model is reused in the web game client.
    /// </summary>
    public int? ModelSourceNumber { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the last modification.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the canonical key (<c>group:number</c>).
    /// </summary>
    public string Key => FormatKey(this.Group, this.Number);

    /// <summary>
    /// Formats a canonical key for an item group and number.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The <c>group:number</c> key.</returns>
    public static string FormatKey(byte group, short number) => $"{group}:{number}";
}
