// <copyright file="LeftoverIngredient.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Input ingredient required by a leftover combine recipe.
/// </summary>
public sealed class LeftoverIngredient
{
    /// <summary>Gets or sets item group.</summary>
    [Range(0, 15)]
    public byte Group { get; set; }

    /// <summary>Gets or sets item number.</summary>
    [Range(0, 511)]
    public short Number { get; set; }

    /// <summary>Gets or sets exact required item level, or <c>-1</c> to accept any level.</summary>
    [Range(-1, 15)]
    public int ExactLevel { get; set; } = -1;

    /// <summary>Gets or sets quantity needed.</summary>
    [Range(1, 50)]
    public int Count { get; set; } = 1;

    /// <summary>Gets or sets English label.</summary>
    [StringLength(64)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese label.</summary>
    [StringLength(64)]
    public string NameVi { get; set; } = string.Empty;
}
