# Agent 4 (monster decisions), final

## Done (all merged by main)
- #224 event monster stats: 77 monsters, 470 values. The BC7/8 swap update is removed.
- #225 Kundun: 5 M HP plus a configurable refill plug-in. The refill runs all the time, also during fights.
- #226 resistances: 231 monsters, 664 values; 86 Water→Lightning.
- #227 speeds and ranges: 40 monsters, 64 values. The client doesn't use them.
- #228 respawn times and field spawn counts.
- Final full test runs: Init 934 passed; HNMO 3654 passed.

## Chosen without a suite value
- Kundun refill: per second plus per interval, also while fighting.
- Chaos Castle 3 monster 166: min/max damage kept in the right order.
- Illusion Temple spirits keep their 10 s respawn.
- Imperial Guardian: only water→lightning resistance.

## Watch out
- Maya's body (364) now has 300 HP and still has 7 drops. Owner question: should her body stay unkillable?

## Left undone
- Decisions 8-10 were done by #235 (main) and #238. The local branch monster-ai-chase duplicates them and can be dropped.
- Kundun refill settings: their [Display] texts are plain English, not resource files in all languages. The 4-language texts are ready in docs/audit/findings/kundun-refill-texts.json. This is a follow-up PR.
