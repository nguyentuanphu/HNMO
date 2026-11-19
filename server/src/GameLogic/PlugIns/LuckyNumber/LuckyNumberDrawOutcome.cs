// <copyright file="LuckyNumberDrawOutcome.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>Summary returned after running any due daily draw.</summary>
/// <param name="DateKey">The drawn date key (<c>yyyy-MM-dd</c>).</param>
/// <param name="DrawnNumber">The winning number.</param>
/// <param name="Winners">The winning picks matched for that round.</param>
public sealed record LuckyNumberDrawOutcome(string DateKey, int DrawnNumber, IReadOnlyList<LuckyNumberWinnerEntry> Winners);
