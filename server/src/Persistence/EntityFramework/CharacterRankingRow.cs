// <copyright file="CharacterRankingRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using HNMO.Interfaces;

/// <summary>
/// A row of the query of the character ranking.
/// </summary>
/// <param name="Name">The name of the character.</param>
/// <param name="ClassName">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The resets.</param>
internal sealed record CharacterRankingRow(string Name, LocalizedString ClassName, float Level, float MasterLevel, float Resets);
