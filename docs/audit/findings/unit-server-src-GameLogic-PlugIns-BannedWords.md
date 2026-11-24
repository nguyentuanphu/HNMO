# Audit Unit: `server/src/GameLogic/PlugIns/BannedWords`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 5 files read in full (`BannedWordChatAction.cs`, `BannedWordsConfiguration.cs`, `BannedWordsEngine.cs`, `BannedWordsMatchResult.cs`, `BannedWordsPlugIn.cs`).
- **Rule Category**: Custom HNMO feature — EN/VI chat & name profanity filter.

## Findings

1. **Matching & index alignment**: `BannedWordsEngine.EvaluateChat` normalizes to `FormC` and uses `FoldVietnameseDiacriticsSameLength` when `IgnoreVietnameseDiacritics` is enabled so masked `*` ranges align 1:1 with the original string. `EvaluateName` splits CamelCase/separator tokens and checks substrings of length `>= 4` (`CheckNameSubstrings`).
2. **Feature toggle & GM exemption**: `BannedWordsPlugIn` is `IDisabledByDefault`, supports `Censor` and `Block` chat actions, and exempts GMs on chat and guild names when `ExemptGameMasters` is true. No defects found.
