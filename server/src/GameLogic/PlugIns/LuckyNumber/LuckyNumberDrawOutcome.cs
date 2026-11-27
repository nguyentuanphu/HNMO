// <copyright file="LuckyNumberDrawOutcome.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>Summary returned after running any due daily draw.</summary>
/// <param name="DateKey">The drawn date key (<c>yyyy-MM-dd</c>).</param>
/// <param name="DrawnNumber">The winning number.</param>
/// <param name="Winners">The winning picks matched for that round.</param>
public sealed record LuckyNumberDrawOutcome(string DateKey, int DrawnNumber, IReadOnlyList<LuckyNumberWinnerEntry> Winners);
