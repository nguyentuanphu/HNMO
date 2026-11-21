// <copyright file="GiftCodeOutcome.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// The outcome of entering a code.
/// </summary>
/// <param name="Result">The result.</param>
/// <param name="Code">The code, when one was found.</param>
/// <param name="Entry">The log entry of a redemption.</param>
/// <param name="Coins">The coins to give now (also kept as pending until they're given).</param>
/// <param name="Owed">The zen and items which wait for the character.</param>
/// <param name="TriesLeft">How many wrong codes the account may still enter in the window.</param>
/// <param name="RetryAtUtc">When the account may try again after <see cref="GiftCodeResult.TooManyTries"/>.</param>
public sealed record GiftCodeOutcome(GiftCodeResult Result, GiftCodeDefinition? Code = null, GiftCodeLogEntry? Entry = null, GiftCodePendingCoins? Coins = null, GiftCodeOwedReward? Owed = null, int TriesLeft = 0, DateTime? RetryAtUtc = null);
