// <copyright file="TopRaceWindow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A period of a race: its key and when it starts and ends.
/// </summary>
/// <param name="Key">The key, e.g. "d2026-11-15", "w2026-11-09", "m2026-11" or "c202611200900".</param>
/// <param name="StartUtc">The start.</param>
/// <param name="EndUtc">The end.</param>
public sealed record TopRaceWindow(string Key, DateTime StartUtc, DateTime EndUtc);
