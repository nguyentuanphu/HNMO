# Unit Audit: `client/web/src/ui (files 4)`

- **Unit**: `client/web/src/ui (files 4)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `17`: `masterLevelWindow.ts`, `menuWindows.ts`, `miniMap.ts`, `missionsWindow.ts`, `mixWindow.ts`, `mobileHud.ts`, `mountChainWindow.ts`, `mountWidget.ts`, `moveWindow.ts`, `nativeText.ts`, `newsPanel.ts`, `npcWindows.ts`, `onboardingWindow.ts`, `partyList.ts`, `partyOptionsWindow.ts`, `partyWindow.ts`, `perfReportWindow.ts`
- **Status**: `Checked` (reclassified from `"No game rules"` because `masterLevelWindow.ts`, `mixWindow.ts`, `moveWindow.ts`, and `partyWindow.ts` enforce client rules)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Master Skill point client validation | `masterLevelWindow.ts:208-211,234-252` | Before `AddMasterSkillPoint`: checks `level < node.maxLevel`, `master.points >= node.requiredPoints`, `weaponSkillEquipped`, `checkParentSkill`, `checkRankPoint`, `checkBeforeSkill`; EXP `%` over `masterLevelStart`..`nextExperience` | Server `AddMasterSkillPointAction` & Desktop `CMasterLevelInterface` | Same |
| Chaos Machine tax math & pre-mix checks | `mixWindow.ts:97,215,404-415,447-468` | `withTax = zen + floor((zen * tax) / 100)`; checks tax-included Zen, `level >= recipe.requiredLevel`, bag empty slot fitting `recipe.width x height`, charm/chaos-charm mutual exclusion, and BC `4`/`DS 9` level error codes | Desktop `CMixInventory::Mix` & Server `ChaosMachineMixRequestAction` | Same |
| Warp window eligibility & Strife map flag | `moveWindow.ts:66-72,78-80` | Marks index `42` (Vulcanus) with `*` (`strife_text`), calls `canWarp(warp, check)` and `warpLevel(warp, check.classIndex)` per row | Desktop `CMoveCommandWindow` (caller `main.ts:1887` passes `outlaw: false`) | Same (`MoveWindow` itself; caller `main.ts` differs) |
| Party leader kick permission check | `partyWindow.ts:108-124` | Only party leader (`index === 0`) may kick members or self-leave via `party.canLeave` | Server `PartyKickRequest` | Same |
| Remaining presentation UI files | `menuWindows.ts`, `miniMap.ts`, `missionsWindow.ts`, `mobileHud.ts`, `mount*.ts`, `nativeText.ts`, `newsPanel.ts`, `npcWindows.ts`, `onboardingWindow.ts`, `partyList.ts`, `partyOptionsWindow.ts`, `perfReportWindow.ts` | Server-driven UI windows, mobile touch controls & layout/scale helpers | Presentation only | No game rules |

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- ba5139b1 feat(web): make chat, whisper and messenger windows resizable with localStorage persistence

Evidence:

- mobileHud.ts: the mobile chat drawer is resizable and remembered - web-only (mobile) convenience; no desktop counterpart.
