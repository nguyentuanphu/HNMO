# Unit: client/src/source/GameLogic/Combat

Checked against: Web `skillCast.ts` & classic MU combat execution rules.

Scope: `client/src/source/GameLogic/Combat` (`16` files, `4587` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `SkillExecution.cpp:250-310` | Clears continuous auto-attack on buff/aura/single-cast skills (Elf non-bow skills, DK Swell Life, DL Critical/Summon, Wizard Nova, Summoner Berserker/Thorns/Weakness, RF buffs); disabled in Stadium & Chaos Castle | Season 6 client combat rules & Web `skillCast.ts` | Same |
