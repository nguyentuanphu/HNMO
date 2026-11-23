// <copyright file="ConfiguredMonsterDrop.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.DataModel.Configuration.Items;

/// <summary>A configured field drop group and its eligible item results before bonuses.</summary>
/// <param name="Group">The configured group.</param>
/// <param name="ChancePerRoll">The probability of selecting the group on an applicable baseline roll.</param>
/// <param name="Items">Eligible definitions with the maximum level at which the group can award them.</param>
public sealed record ConfiguredMonsterDrop(
    DropItemGroup Group,
    double ChancePerRoll,
    IEnumerable<(ItemDefinition Item, byte MaximumLevel)> Items);
