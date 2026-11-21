// <copyright file="ResetRankingEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A place in a reset ranking.
/// </summary>
/// <param name="Rank">The rank, from 1.</param>
/// <param name="CharacterId">The character's id.</param>
/// <param name="Name">The name.</param>
/// <param name="Class">The class number.</param>
/// <param name="Value">The resets of the month, or the relifes.</param>
/// <param name="Resets">The resets after the last reset or relife.</param>
/// <param name="Relifes">The relifes.</param>
public sealed record ResetRankingEntry(int Rank, string CharacterId, string Name, int Class, int Value, int Resets, int Relifes);
