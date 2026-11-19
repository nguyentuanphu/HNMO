// <copyright file="TopRaceStanding.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A place in a running race.
/// </summary>
/// <param name="Rank">The rank, from 1.</param>
/// <param name="Entry">The entry.</param>
public sealed record TopRaceStanding(int Rank, TopRaceEntry Entry);
