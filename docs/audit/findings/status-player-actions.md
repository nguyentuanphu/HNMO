# Agent 2 (player actions), final

## Done
- #221 guild level 150 setting (Plug-ins → "Guild limits" → MinimumCreateLevel; texts in en/de/vi/zh-CN) + busy answer stops
- #229 mini game entry refused when dead/duel/trade/NPC dialog
- #231 talisman stat items state check
- #236 personal store opens from level 6, refused when dead/duel/trade/NPC dialog

## Not finished
- Store buy only within the seller's view range: branch store-buy-view-range (commit dab2f24b, pushed, no PR). Red was shown; the green and full runs did not run (build lock). Line endings LF in OfflineTradeTests.cs and the new test. `MonsterLeashesHomeAndHealsWhenPulledOutOfAreaAsync` failed once in a full run; it could be flaky or caused by this change.

## Not started (owner decided, scripts only locally)
- Repair-all without an NPC: remove only the wrong "cheater" log (keep no-NPC, 2.5x).
- Quests complete only while talking to the quest's NPC.
- Party kick with an index out of range: refuse quietly.
