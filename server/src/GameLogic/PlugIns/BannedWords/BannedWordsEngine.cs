// <copyright file="BannedWordsEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BannedWords;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Pure rule engine for normalizing and filtering English and Vietnamese banned words in chat and names.
/// </summary>
public static class BannedWordsEngine
{
    private const int MinimumSubstringTermLength = 4;
    private const string WordCharClass = @"[\p{L}\p{N}_]";
    private const string NonWordBoundaryPrefix = @"(?<!" + WordCharClass + ")";
    private const string NonWordBoundarySuffix = @"(?!" + WordCharClass + ")";

    /// <summary>
    /// Parses a newline- or comma-separated editable word list into distinct normalized terms.
    /// </summary>
    /// <param name="raw">The raw text entered by an administrator.</param>
    /// <returns>The parsed distinct terms.</returns>
    public static List<string> ParseWordList(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = raw.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            var trimmed = entry.Trim().Normalize(NormalizationForm.FormC);
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates a chat message against the configured English and Vietnamese banned words.
    /// </summary>
    /// <param name="message">The raw chat message.</param>
    /// <param name="config">The filter configuration.</param>
    /// <returns>The match result including censored output text.</returns>
    public static BannedWordsMatchResult EvaluateChat(string? message, BannedWordsConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrEmpty(message))
        {
            return new BannedWordsMatchResult(false, null, message ?? string.Empty);
        }

        var terms = CollectNormalizedTerms(config);
        if (terms.Count == 0)
        {
            return new BannedWordsMatchResult(false, null, message);
        }

        var normalizedInput = message.Normalize(NormalizationForm.FormC);
        var foldedInput = config.IgnoreVietnameseDiacritics
            ? FoldVietnameseDiacriticsSameLength(normalizedInput)
            : normalizedInput;

        var matchedRanges = new List<(int Start, int Length)>();
        string? firstMatchedTerm = null;

        foreach (var term in terms)
        {
            var compareTerm = config.IgnoreVietnameseDiacritics
                ? FoldVietnameseDiacritics(term.Normalized)
                : term.Normalized;

            if (string.IsNullOrWhiteSpace(compareTerm))
            {
                continue;
            }

            var escaped = Regex.Escape(compareTerm).Replace(@"\ ", @"\s+");
            var pattern = NonWordBoundaryPrefix + escaped + NonWordBoundarySuffix;
            var matches = Regex.Matches(foldedInput, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach (Match m in matches)
            {
                if (!m.Success || m.Length == 0)
                {
                    continue;
                }

                firstMatchedTerm ??= term.Original;
                matchedRanges.Add((m.Index, m.Length));
            }
        }

        if (matchedRanges.Count == 0)
        {
            return new BannedWordsMatchResult(false, null, message);
        }

        var censored = ApplyAsteriskMasks(normalizedInput, matchedRanges);
        return new BannedWordsMatchResult(true, firstMatchedTerm, censored);
    }

    /// <summary>
    /// Evaluates a character or guild name against the configured banned words.
    /// </summary>
    /// <param name="name">The proposed character or guild name.</param>
    /// <param name="config">The filter configuration.</param>
    /// <returns>The match result indicating whether the name contains restricted language.</returns>
    public static BannedWordsMatchResult EvaluateName(string? name, BannedWordsConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(name))
        {
            return new BannedWordsMatchResult(false, null, name ?? string.Empty);
        }

        var terms = CollectNormalizedTerms(config);
        if (terms.Count == 0)
        {
            return new BannedWordsMatchResult(false, null, name);
        }

        var normalizedName = name.Trim().Normalize(NormalizationForm.FormC);
        var compareName = config.IgnoreVietnameseDiacritics
            ? FoldVietnameseDiacritics(normalizedName)
            : normalizedName;

        var tokens = SplitNameTokens(compareName);
        var compactName = CollapseSeparators(compareName);

        foreach (var term in terms)
        {
            var compareTerm = config.IgnoreVietnameseDiacritics
                ? FoldVietnameseDiacritics(term.Normalized)
                : term.Normalized;
            if (string.IsNullOrWhiteSpace(compareTerm))
            {
                continue;
            }

            var compactTerm = CollapseSeparators(compareTerm);
            if (tokens.Any(t => t.Equals(compareTerm, StringComparison.OrdinalIgnoreCase)
                             || t.Equals(compactTerm, StringComparison.OrdinalIgnoreCase)))
            {
                return new BannedWordsMatchResult(true, term.Original, new string('*', normalizedName.Length));
            }

            if (config.CheckNameSubstrings && compactTerm.Length >= MinimumSubstringTermLength
                && compactName.Contains(compactTerm, StringComparison.OrdinalIgnoreCase))
            {
                return new BannedWordsMatchResult(true, term.Original, new string('*', normalizedName.Length));
            }
        }

        return new BannedWordsMatchResult(false, null, name);
    }

    private static List<(string Original, string Normalized)> CollectNormalizedTerms(BannedWordsConfiguration config)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string Original, string Normalized)>();

        foreach (var raw in (config.EnglishWords ?? []).Concat(config.VietnameseWords ?? []))
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            var normalized = CollapseWhitespace(trimmed.Normalize(NormalizationForm.FormC));
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            list.Add((trimmed, normalized));
        }

        list.Sort((a, b) => b.Normalized.Length.CompareTo(a.Normalized.Length));
        return list;
    }

    private static string CollapseWhitespace(string text) =>
        Regex.Replace(text, @"\s+", " ");

    private static string CollapseSeparators(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static List<string> SplitNameTokens(string name)
    {
        var tokens = new List<string> { name };
        var current = new StringBuilder();

        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (!char.IsLetter(ch))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            if (i > 0 && current.Length > 0 && char.IsUpper(ch) && char.IsLower(name[i - 1]))
            {
                tokens.Add(current.ToString());
                current.Clear();
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static string FoldVietnameseDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(ch switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => ch,
            });
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Folds precomposed Vietnamese characters one-to-one so character indices stay aligned with FormC input.
    /// </summary>
    private static string FoldVietnameseDiacriticsSameLength(string formCInput)
    {
        var sb = new StringBuilder(formCInput.Length);
        foreach (var ch in formCInput)
        {
            if (ch == 'đ')
            {
                sb.Append('d');
                continue;
            }

            if (ch == 'Đ')
            {
                sb.Append('D');
                continue;
            }

            var decomposed = ch.ToString().Normalize(NormalizationForm.FormD);
            var baseChar = decomposed[0];
            foreach (var d in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark)
                {
                    baseChar = d;
                    break;
                }
            }

            sb.Append(baseChar);
        }

        return sb.ToString();
    }

    private static string ApplyAsteriskMasks(string original, List<(int Start, int Length)> ranges)
    {
        ranges.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));
        var merged = new List<(int Start, int End)>();
        foreach (var (start, length) in ranges)
        {
            var end = Math.Min(original.Length, start + length);
            if (start < 0 || end <= start)
            {
                continue;
            }

            if (merged.Count > 0 && start < merged[^1].End)
            {
                var prev = merged[^1];
                merged[^1] = (prev.Start, Math.Max(prev.End, end));
            }
            else
            {
                merged.Add((start, end));
            }
        }

        var sb = new StringBuilder(original.Length);
        var cursor = 0;
        foreach (var (start, end) in merged)
        {
            if (start > cursor)
            {
                sb.Append(original, cursor, start - cursor);
            }

            sb.Append('*', Math.Max(3, end - start));
            cursor = end;
        }

        if (cursor < original.Length)
        {
            sb.Append(original, cursor, original.Length - cursor);
        }

        return sb.ToString();
    }
}
