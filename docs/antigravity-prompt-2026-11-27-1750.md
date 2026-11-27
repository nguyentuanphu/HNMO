# Prompt for Antigravity, 27 Nov 2026 17:50 Sydney (adds the PK fix; the 17:25 queue stands)

```
From main cloud Claude, Fri 27 Nov 17:50 Sydney. Merged since 17:25: #436 (1803cf4d, socket 0xFF tooltip).
Your 17:25 queue stands (1. rebase #438 first, 2. anti-crack layers, 3. headers, 4. #439, 5. S9 chain).
Thanks for pk-rules-s6-s9.md (#430 / #431) — item 6 is done. New item 7:

## 7. PK rules per season (owner decided 27 Nov 17:45: "S6 plays S6 rules, S9 plays S9 rules")
Bug today: OutlawPoints sets exactly 43,200 on the 2nd kill = the PK1 threshold, and 1 point/second online is
removed, so PK1 shows for 1 second. Fix in server/src/GameLogic/OutlawPoints.cs + Player.cs (outlaw tick,
RemoveOutlawPointsAsync, the kill path) and PKChatCommandPlugIn, choosing the rules with
HnmoSeasonInstance.IsSeasonNine (Interfaces/HnmoSeasonInstance.cs). Draft PR into claude/hnmo-main-handover-54co2m.
- Season 6.3 (s6, and the Arena unless the S6 config says otherwise — tell me what you chose): each outlaw stage lasts
  3 hours (PKTIME = 1): 1st kill Warning 3 h; 2nd kill PK1 3 h then Warning 3 h; 3rd kill Murderer 3 h, then PK1 3 h,
  then Warning 3 h. 1 point per second online, monster kills remove floor(level / 2), /pkclear as configured
  (PkClearCost 1,000,000 x kill count). Further kills as Murderer: follow the S6 suite (read it; say what it does).
- Season 9 (s9): ReqPoint thresholds 21,600 / 43,200 / 86,400, MurdererPointIncrease 3,600, NO per-second decay;
  only monster kills (floor(level / 2)) and /pkclear (Cost 1,000,000, CostMultiplyByKillCount 1) lower it. The kill
  must land INSIDE the new stage (PK1 must not drop out after one monster or one second).
- Saved characters: convert existing totals so nobody's stage jumps on the first login after the update (state
  kept, remaining time sensible); unit-test the conversion for each stage in both seasons.
- Unit tests: per season, 1st/2nd/3rd/4th kill → state and hold time; S6 PK1 still PK1 after 1 s and after 2 h 59 m,
  Warning after 3 h; S9 PK1 unchanged after 1 h online idle, lowered by monster kills; /pkclear in both.
- The web client shows the state from the server; check the PK name colour still follows (no client change expected).
Report on the PR: tests run, what was verified and what wasn't, Sydney times.
```
