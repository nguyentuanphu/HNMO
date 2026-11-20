# Feature audit: shops, money and events (20 Nov 2026)

Scope: (A) shops and money, (B) events. Each check is compared with classic MU Season 6 behaviour.
Where the classic rule is unclear, the check is listed as an owner decision and nothing was changed.

Not repeated here (already in PRs #147 to #150): zen vs EXP rate, drop-bonus zen, MG/DL/RF creation level, event EXP vs rate and master EXP.

Result key: **OK** = matches classic or is a sound HNMO rule; **Fixed** = bug, fixed in the linked draft PR with a test that fails before the fix; **Owner** = decision for the owner, options listed below.

## A. Shops and money

| Check | Result | Notes |
|---|---|---|
| NPC buy price (`ItemPriceCalculator`, castle store tax) | OK | Classic drop-level formula, option and excellent modifiers, rounding. |
| NPC sell price (buy price / 3, durability penalty up to 60 %) | OK | Matches classic. The divisor 3 is fixed in code (no setting exists). |
| Repair cost, NPC and self-repair | OK | `3 * sqrt(p) * sqrt(sqrt(p)) * missing + 1`, base capped at 400 M, x1.4 destroyed (x2 pets), x2.5 without NPC. |
| Repair all without an open NPC | Owner | Logged as a cheat but still repairs at the self-repair price. See O1. |
| Durability loss | OK (spot check) | Not audited in depth this round. |
| `MaximumInventoryMoney` in `TryAddMoney` / sell / pickup / rewards | OK | |
| `MaximumInventoryMoney` when a trade finishes | **Fixed** | [#152](https://github.com/nguyentuanphu/HNMO/pull/152): the received trade zen was added without the limit and could wrap negative. |
| Changing the trade zen offer | **Fixed** | [#153](https://github.com/nguyentuanphu/HNMO/pull/153): the zen already offered did not count, so the offer could not be lowered or raised with it. |
| `MaximumVaultMoney` on deposit, inventory limit on withdraw | OK | |
| `ClampMoneyOnPickup` | OK | Read in `MoneyDistribution` (single place for all pickup paths). |
| Money pickup and party split (`MoneyDistribution`) | OK | Split by EXP share, largest-remainder rounding, forfeited shares redistributed. |
| Zen of kills inside mini games | Owner | Not dropped, but paid straight into the inventory. See O2. |
| Vault deposit / usage fee | Owner | No vault fee exists. See O3. |
| Personal shop zen and other currencies | OK | Buyer charged first, refunded if the seller is at the limit. |
| PK clear cost (`/pkclear`) | OK, one Owner point | Cost = kills x `ZenCostPerKill`, capped at int.MaxValue. Free when the kill count is 0 but the PK state is still set. See O4. |
| Chaos mix zen cost | OK | `Money + MoneyPerFinalSuccessPercentage * rate`, plus castle chaos tax. |
| Chaos mix success rate from item prices | **Fixed** | [#154](https://github.com/nguyentuanphu/HNMO/pull/154): the per-item price divisor wrapped at 256 % (second/third wings, socket mixes). |
| Cash shop purchase price | OK | Coins checked and restored on a failed save. |
| Zen lost on death | OK | Configurable per level; not in mini games; talisman protects. |

## B. Events

| Check | Result | Notes |
|---|---|---|
| Level ranges BC 1-8, DS 1-7, CC 1-7 (normal and MG/DL/RF) | OK | Match the classic Season 6 tables. |
| Level ranges IT 1-6 | Owner | IT5 ends at 399 and IT6 needs a master class: a level-400 non-master can enter no temple. See O5. |
| Tickets (level must match, one durability used) | OK | Regular ticket preferred over a free ticket. |
| Entrance fee (CC, IT), refund when too few players | OK | Bots enter without a fee but get the refund too (server bots only; minor). |
| PK players | OK | PK stage 1 and above refused unless the event allows it. |
| Party rules | OK | Events without parties kick the entering player from the party. |
| Timetables, start plug-ins | OK | Configurable; start uses the server time zone. |
| Countdown at the event NPCs | **Fixed** | [#155](https://github.com/nguyentuanphu/HNMO/pull/155): read the timetable as UTC and counted to midnight after the last start. |
| BC rewards (EXP per goal, per second, winner/loser zen, Jewel of Chaos) | OK, one Owner point | Losers get the loser zen even when nobody won. See O6. |
| DS / CC rewards by rank | OK | |
| Money reward with a full inventory | Owner | Not paid, but the score table still shows it as given. See O7. |
| IT rewards | OK | EXP at the end, item when the winner closes the result dialog (classic). |
| Crywolf rewards | OK | Altar contractors and the top 5 heroes are separate classic reward lists, so a contractor in the top 5 gets both. |
| Imperial Guardian, Kanturu, Castle Siege rewards | OK (spot check) | EXP paths covered by PR #150; tribute withdraw respects the zen limit. |
| Rewards after disconnect | OK | Score kept by character name; a rejoining player gets the rewards on its new player object. |
| Duplicate rewards | OK | IT claims are once per player; reward service evaluates each reward once per rank. |
| Happy Hour plug-in vs Bonus Events plug-in | Owner | Both can be active and stack. See O8. |
| Last Man Standing cancelled for too few players | **Fixed** | [#156](https://github.com/nguyentuanphu/HNMO/pull/156): the entry fee was kept and the contestants stayed in the arena. |

## Owner decisions

- **O1. Repair all without an NPC.** Today it is logged as a cheat but repairs at 2.5x. Options: (a) keep; (b) refuse without an open NPC (classic: "repair all" is an NPC function).
- **O2. Zen of kills inside mini games.** Today it is paid straight into the inventory (no drop). Options: (a) keep; (b) give no zen in Devil Square / Chaos Castle (classic gives no zen there), keep Blood Castle; (c) give no zen in any mini game.
- **O3. Vault fee.** Classic charges a small storage fee based on level when the vault is used. Options: (a) no fee (today); (b) add a configurable fee, off by default.
- **O4. `/pkclear` with zero kills but PK state.** Today it is free. Options: (a) keep; (b) charge a minimum (one kill's cost).
- **O5. Illusion Temple at level 400 without a master class.** Options: (a) keep (no temple); (b) raise IT5's maximum to 400.
- **O6. Blood Castle loser zen when nobody won.** Options: (a) keep; (b) only when a winner exists.
- **O7. Money reward with a full inventory.** Options: (a) keep (lost, message shown, but the score table shows it); (b) show 0 in the table; (c) pay up to the limit; (d) queue it as an owed reward.
- **O8. Happy Hour + Bonus Events stacking.** `HappyHourPlugIn` multiplies the EXP rate attribute; `BonusEventsPlugIn` multiplies EXP in the calculation, and its own entries add up. With both on, e.g. 2x Happy Hour and +100 % bonus event give 4x. Options: (a) keep stacking; (b) only the larger one applies; (c) retire `HappyHourPlugIn` in favour of `BonusEventsPlugIn`.

## Verification

All five fix branches (#152 to #156) merged locally onto `claude/hnmo-main-handover-54co2m`: the full `HNMO.Tests` run passes (3455 passed, 2 skipped, 0 failed).
