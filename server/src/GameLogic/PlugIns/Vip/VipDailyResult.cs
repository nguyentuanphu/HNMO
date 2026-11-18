// <copyright file="VipDailyResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>A localized-by-the-caller daily reward outcome; recorded credits use the existing pending-grant pipeline.</summary>
/// <param name="Status">The machine-readable result.</param>
/// <param name="Amount">The recorded bonus.</param>
public sealed record VipDailyResult(string Status, int Amount = 0);
