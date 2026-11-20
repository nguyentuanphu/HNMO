# Feature audit: shops, money and events (20 Nov 2026)

Scope: (A) shops and money, (B) events. Each check is compared with classic MU Season 6 behaviour.
Where the classic rule is unclear, the check is listed as an owner decision and nothing was changed.

Not repeated here (already in PRs #147 to #150): zen vs EXP rate, drop-bonus zen, MG/DL/RF creation level, event EXP vs rate and master EXP.

Result key: **OK** = matches classic or is a sound HNMO rule; **Fixed** = bug, fixed in the linked draft PR with a test that fails before the fix; **Owner** = the owner decided (see the end).

## A. Shops and money

| Check | Result | Notes |
|---|---|---|
| NPC buy price (`ItemPriceCalculator`, castle store tax) | OK | Classic drop-level formula, option and excellent modifiers, rounding. |
| NPC sell price (buy price / 3, durability penalty up to 60 %) | OK | Matches classic. The divisor 3 is fixed in code (no setting exists). |
| Repair cost, NPC and self-repair | OK | `3 * sqrt(p) * sqrt(sqrt(p)) * missing + 1`, base capped at 400 M, x1.4 destroyed (x2 pets), x2.5 without NPC. |
| Repair all without an open NPC | Owner: keep | Logged as a cheat but still repairs at the self-repair price (O1). |
| Durability loss | OK (spot check) | Not audited in depth this round. |
| `MaximumInventoryMoney` in `TryAddMoney` / sell / pickup / rewards | OK | |
| `MaximumInventoryMoney` when a trade finishes | **Fixed** | [#152](https://github.com/nguyentuanphu/HNMO/pull/152): the received trade zen was added without the limit and could wrap negative. |
| Changing the trade zen offer | **Fixed** | [#153](https://github.com/nguyentuanphu/HNMO/pull/153): the zen already offered did not count, so the offer could not be lowered or raised with it. |
| `MaximumVaultMoney` on deposit, inventory limit on withdraw | OK | |
| `ClampMoneyOnPickup` | OK | Read in `MoneyDistribution` (single place for all pickup paths). |
| Money pickup and party split (`MoneyDistribution`) | OK | Split by EXP share, largest-remainder rounding, forfeited shares redistributed. |
| Zen of kills inside mini games | **Fixed** | [#159](https://github.com/nguyentuanphu/HNMO/pull/159): no zen in Devil Square and Chaos Castle (classic); Blood Castle keeps it, paid directly (O2). |
| Vault deposit / usage fee | Owner: keep | No vault fee (O3). |
| Personal shop zen and other currencies | OK | Buyer charged first, refunded if the seller is at the limit. |
| PK clear cost (`/pkclear`) | **Fixed** | [#161](https://github.com/nguyentuanphu/HNMO/pull/161): cost = max(1, kills) x `ZenCostPerKill`; a PK state with 0 kills was free (O4). |
| Chaos mix zen cost | OK | `Money + MoneyPerFinalSuccessPercentage * rate`, plus castle chaos tax. |
| Chaos mix success rate from item prices | **Fixed** | By [#151](https://github.com/nguyentuanphu/HNMO/pull/151) (`crafting-rate-no-wrap`): the per-item price divisor wrapped at 256 % (second/third wings, socket mixes). #154 made the same change and was closed in favour of #151. |
| Cash shop purchase price | OK | Coins checked and restored on a failed save. |
| Zen lost on death | OK | Configurable per level; not in mini games; talisman protects. |

## B. Events

| Check | Result | Notes |
|---|---|---|
| Level ranges BC 1-8, DS 1-7, CC 1-7 (normal and MG/DL/RF) | OK | Match the classic Season 6 tables. |
| Level ranges IT 1-6 | Owner: keep | IT5 ends at 399 and IT6 needs a master class, so a level-400 non-master can enter no temple (O5). |
| Tickets (level must match, one durability used) | OK | Regular ticket preferred over a free ticket. |
| Entrance fee (CC, IT), refund when too few players | OK | Bots enter without a fee but get the refund too (server bots only; minor). |
| PK players | OK | PK stage 1 and above refused unless the event allows it. |
| Party rules | OK | Events without parties kick the entering player from the party. |
| Timetables, start plug-ins | OK | Configurable; start uses the server time zone. |
| Countdown at the event NPCs | **Fixed** | [#155](https://github.com/nguyentuanphu/HNMO/pull/155): read the timetable as UTC and counted to midnight after the last start. |
| BC rewards (EXP per goal, per second, winner/loser zen, Jewel of Chaos) | OK; Owner: keep | Losers get the loser zen even when nobody won (O6). |
| DS / CC rewards by rank | OK | |
| Money reward with a full inventory | **Fixed** | [#160](https://github.com/nguyentuanphu/HNMO/pull/160): pays up to the inventory maximum; the score table and the given money show what was paid; the message stays when not everything fit (O7). |
| IT rewards | OK | EXP at the end, item when the winner closes the result dialog (classic). |
| Crywolf rewards | OK | Altar contractors and the top 5 heroes are separate classic reward lists, so a contractor in the top 5 gets both. |
| Imperial Guardian, Kanturu, Castle Siege rewards | OK (spot check) | EXP paths covered by PR #150; tribute withdraw respects the zen limit. |
| Rewards after disconnect | OK | Score kept by character name; a rejoining player gets the rewards on its new player object. |
| Duplicate rewards | OK | IT claims are once per player; reward service evaluates each reward once per rank. |
| Happy Hour plug-in vs Bonus Events plug-in | **Fixed** | [#158](https://github.com/nguyentuanphu/HNMO/pull/158): no stacking, the larger EXP bonus applies. Happy Hour has no drop bonus, so drops are unchanged. Bonus events still add up among themselves (O8). |
| Last Man Standing cancelled for too few players | **Fixed** | [#156](https://github.com/nguyentuanphu/HNMO/pull/156): the entry fee was kept and the contestants stayed in the arena. |

## Owner decisions (decided 20 Nov 2026)

| # | Question | Decision | Change |
|---|---|---|---|
| O1 | Repair all without an open NPC (logged as a cheat, repairs at 2.5x) | Keep as it is | none |
| O2 | Zen of kills inside mini games | No zen in Devil Square and Chaos Castle (classic); Blood Castle keeps it | [#159](https://github.com/nguyentuanphu/HNMO/pull/159) |
| O3 | Vault fee (classic charges a level-based storage fee) | Keep: no fee | none |
| O4 | `/pkclear` with 0 kills but still in a PK state | At least one kill's price | [#161](https://github.com/nguyentuanphu/HNMO/pull/161) |
| O5 | Illusion Temple at level 400 without a master class | Keep as it is | none |
| O6 | Blood Castle loser zen when nobody won | Keep as it is | none |
| O7 | Mini-game money reward with a full zen inventory | Pay up to the maximum; show the paid amount; keep the message | [#160](https://github.com/nguyentuanphu/HNMO/pull/160) |
| O8 | Happy Hour + Bonus Events stacking | No stacking; the larger bonus applies; bonus events keep adding up among themselves | [#158](https://github.com/nguyentuanphu/HNMO/pull/158) |

## Verification

All fix branches merged locally onto `claude/hnmo-main-handover-54co2m` (#152, #153, #151 in place of #154, #155, #156, #158, #159, #160, #161): the full `HNMO.Tests` run passes (3469 passed, 2 skipped, 0 failed).
