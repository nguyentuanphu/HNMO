# Unit: client/src/source/UI/HUD/Skills

Checked against: Server `MasterExperienceFormula` & Web `masterLevel.ts`.

Scope: `client/src/source/UI/HUD/Skills` (`8` files, `1126` lines).

## Findings

| File & Line | Rule / Formula | Parity Reference | Result |
|---|---|---|---|
| `MasterSkillTreeLayout.cpp:14-44` | `kNormalMaxLevel = 400`, `kSecondCurveLevel = 255`, `kFirstMasterLevelExperience = 3892250000`, `((9+total)*total^2*10 + (9+over)*over^2*1000 - 3892250000)/2` | Server `MasterExperienceFormula` & Web `masterLevel.ts:122-127` | Same |
| `SkillTooltipModel.cpp:365-390` | Applies Master Tree skill bonus percentages to Heal/Defense/Attack tooltips | Server Master Skill effects | Same |
