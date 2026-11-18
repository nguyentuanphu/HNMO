// <copyright file="BannedWordsMatchResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BannedWords;

/// <summary>
/// Result of evaluating a chat message or name against banned word rules.
/// </summary>
/// <param name="IsMatched">Indicates whether at least one banned word or phrase matched.</param>
/// <param name="MatchedTerm">The first matched configured term, if any.</param>
/// <param name="FilteredText">The text with matched chat terms masked using asterisks.</param>
public readonly record struct BannedWordsMatchResult(
    bool IsMatched,
    string? MatchedTerm,
    string FilteredText);
