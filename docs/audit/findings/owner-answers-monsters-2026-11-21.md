# Owner's answers on monsters (21 Nov 2026)

Questions from ledger-monster-ai.md. Every answer: follow the Season 6/9 server suites.

1. Event monsters (Blood Castle incl. BC7/BC8, Chaos Castle, Kalima), Kanturu bosses (Maya, Nightmare), Raklion monsters: take the suites' stats. This replaces HNMO's deliberate BC7/BC8 tuning.
2. Kundun (275): 5,000,000 HP plus the suites' HP refill (KundunRefillHPSec 400, KundunRefillHP 15000, KundunRefillHPTime 600 from CommonServer.cfg). Make the refill configurable.
3. (same as 1)
4. Resistances: re-import all four from the suites for every monster, and fix the 4 maps that store lightning as WaterResistance.
5. Respawn times: take the suites' values.
6. Move speed, attack speed, view range, attack range, move range: take the suites' values.
7. Field spawn counts: take the suites' counts.
8. Chase: a monster chases the player who hit it, even from outside its view range, within its move range.
9. Leash: a monster pulled out of its area drops the target, walks back to its spawn area and heals to full.
10. EXP is split by the damage each player dealt. The drop belongs to the top damage dealer for the first few seconds.
