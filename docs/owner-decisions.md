# Owner's decisions, word for word

Each line quotes the owner exactly, with the date (Sydney time). Agents cite this file instead of retelling a
decision. Add new decisions here as they are made; never turn an agent's habit into an owner's rule.

| Date | Question | The owner's words |
|---|---|---|
| 21 Nov | Shops of Arena Guard (239), Jin (583/604), Moss (492)? | "Ở recommend, đẻ làm sau cubgf dữ liệu s9; hiện tại chưa được làm ah" (later, together with the Season 9 data; the data landed on 22 Nov, the shops were merged on 23 Nov) |
| 21 Nov | Item 13-50 | "Giữ Scroll of Blood" |
| 21 Nov | Bots in events and in the online count | "Không vào event; online có, không chiếm chỗ" |
| 23 Nov | How agents work | One draft PR per area, targeted tests, GitHub runs the full suites, main runs them once per merge batch ("Okay em ghi vào nhé", to main's proposal) |
| 23 Nov | Which group first when usage is back | "Sửa test đỏ trước" |
| 23 Nov | How far the audit reads | "Hết, theo thứ tự ưu tiên" |
| 23 Nov | Old PRs #26-#85, #257, #277 | "Kiểm tra rồi đóng" |
| 24 Nov | Audit: the HNMO folders left out (AttributeSystem, GuildServer, FriendServer, PlugIns, Interfaces, web bridge) and the Season 6 / 9 clients' encoded data tables (items, skills, quests, gates ...) | "Đưa cả hai vào" |
| 24 Nov | Audit of the HNMO desktop client (446,000 lines) | "Sau server, chỉ phần có luật" (after the server, its rule parts only) |
| 24 Nov | Give ChatGPT: one admin (control page into the admin panel), VIP and coin top-up in test mode, licence packages on the hub in test mode | "Ok em cho chatgpt lamf 3 viec do di" |
| 24 Nov | Auto play: one window instead of three | "Gộp 3 thành một autoplay"; how: "Gom vào cửa sổ 1, thêm tab" (the desktop helper window keeps its three tabs; HNMO extras and Ultimate become two more tabs; Z opens it, Shift+Z / Alt+Z open their tab; the small Ultimate HUD bar stays); who: "Main (em) làm", changed the same day to Antigravity: "Em đừng làm autoplay đi rồi cái đó để antigravity làm" |
| 24 Nov | Merge the hub's licence packages PR (test mode; Netlify may publish it) | "Ok em làm đi" |
| 23 Nov | Release 1.0.1 | "Hiện tại chưa có host nào hết. Với cả luôn cần updated full folder và cả phần cần update" |
| 26 Nov | Batch order, old PRs, hub #2, `main` into the working branch | Batch A then B; old PRs "Kiểm tra rồi đóng"; hub #2 review and test, do not merge yet; merge `main` into the working branch after batch A (popup, main's recommendations) |
| 26 Nov | Q1 master skill tree | Add the missing S6 passives (314..324 etc.); the 330-level S9 tree waits for the Season 9 data set |
| 26 Nov | Q2 3rd-class quests 4 and 5 | "Giữ luật HNMO" (drops 3/4/5 %, points 20/10) |
| 26 Nov | Q3 episode quests | Import the missing Ep1/Ep2 steps and sync Ep19 rewards |
| 26 Nov | Q4 Castle Siege | Keep the 7-day Saturday cycle and today's combat rules |
| 26 Nov | Q5 mini-games | "Giữ tất cả luật HNMO" |
| 26 Nov | Q6 missing events | "Tất cả cùng lúc" (BonusEvent, XMasAttack, LorenDeep, RingAttack, LastManStanding, MiningSystem, MuRummy, Delgado Lucky Coin, Cherry Blossom) |
| 26 Nov | Q6b invasion schedules, Moss merchant | Keep HNMO's flexible schedules and Moss |
| 26 Nov | Q7 VIP tiers, quest completion | Keep today's VIP; the player must be talking to the quest NPC |
| 26 Nov | Q8 Gens | Same Gens required for guild/alliance; hide opposite-Gens chat; PK pays 2x warp Zen |
| 26 Nov | Q9 seals +150 % drop | "Giữ như hiện tại (không tăng rơi)" |
| 26 Nov | Q10 Bless potion, reflect caps | Bless only for Siege gates and statues; keep the 60 % / 70 % caps |
| 26 Nov | Q11 Chaos Card mixes | "Làm ngay" |
| 26 Nov | Q12 personal store | Add the server-side Level >= 6 check |
| 26 Nov | Q13 Season 9 data set | "Làm tất cả cùng lúc" (items, events, combat/progression systems) |
| 26 Nov | Mac app: Apple signing and notarization | The owner says he already told main that the Mac app may run without notarization ("anh đã báo là chạy ko công chứng app mac dc"; not found in this file before, recorded now): ship it unsigned, Gatekeeper asks once; no Apple Developer account needed for now |
| 26 Nov | #381 Arena Home | Fix the PR text (Home is only the standing and respawn rectangle, walkable, not a safe zone); no new protection rule ("Sửa mô tả PR cho đúng") |
| 27 Nov | Two local branches (asset integrity in the database, Season 9 part c) | Popup answer: "Push cả hai" — draft PRs #423 and #424 |
| 27 Nov | How skills 618–621 are learned | Popup answer: "Qua master tree" |
| 27 Nov | Effects of skills 618–620 | Popup answer: "Theo client desktop S9" |
| 27 Nov | Muun and Pentagram/Errtel | Popup answer: "Làm cả hai" (full: slot, packets, damage, desktop and web UI) |
| 27 Nov | Weapon Mastery 571 (server 20 levels, web tree 10) | Popup answer: "Theo desktop client" |
| 27 Nov | The 10 Season 9 admin settings without effect | Popup answer: "Làm cho có tác dụng"; any that can't be wired yet stays hidden |
| 27 Nov | 30 Vietnamese-only buffs, 15 item descriptions without "vi" | Popup answer: "Em viết" (main writes them; the owner reviews in the PR) |
| 27 Nov | Merge the working branch into `main`, release 1.0.1 | Popup answer: "Chờ anh báo" |
| 27 Nov | Hub PR #2 (test licence orders) | Popup answer: "Merge" |
| 27 Nov | Real payments (Stripe, bank) | Popup answer: "Chuẩn bị mở" (prepare the list of what is needed; don't turn it on) |
| 27 Nov | Merging PRs with red e2e shards into the working branch | Popup answer: "Được, nếu đỏ không liên quan" (server tests green, the PR's own specs green, the red specs red on the base too or green on a rerun; `main` still needs green) |
| 27 Nov | Hardware GPU FPS for #381 | Popup answer: "Không cần" |
| 27 Nov | Real launches on Mac and Linux | Popup answer: "Chỉ Mac" (the owner tries the Mac builds; Linux relies on CI) |
| 27 Nov | Lost FIN on loopback (network filter) | Popup answer: "Rút ngắn thời gian chờ" (a shorter keep-alive / idle timeout, set in the admin panel) |
| 27 Nov | Muun level-up cost | Popup answer: "Theo client desktop S9" |
| 27 Nov | Muun bonuses marked "not in PvP" | Popup answer: "Tách đúng: chỉ đánh quái" (a PvE-only stat) |
| 27 Nov | Muun and Pentagram data tables taken from another suite's data in #403 (coding rule 9) | Popup answer: "Rà lại, làm theo desktop" (desktop client values where it has them; otherwise admin settings with HNMO's own defaults) |
| 27 Nov | Pentagram monster elements, drops, mix costs | Popup answer: "Theo client desktop S9 + Admin" |
| 26 Nov | M menu for outlaws (PK level 2 and up) | "Giữ khóa như desktop gốc" (the M menu stays locked; /move charges 2x into a gens battle zone, 50x elsewhere) |
| 26 Nov | Vault zen with several vault pages | "Mỗi trang một két Zen riêng" (one purse per page; a zen box belongs to its page) |
| 26 Nov | Gens defaults on servers already running | "Áp cho mọi server" |
| 26 Nov | #377 master experience at the maximum level waits for the Season Plan | "Bỏ điều kiện Season Plan" (solo and party alike) |

Earlier decisions (Maya's body, Fenrir, `/move` in events, the skill sweep points, the socket weapon mix in
Season 6 and 9, the MUVIETNAM2 purge after the merge into `main`) are summarized in
`docs/main-todo-2026-11-21.md`; their exact words are still to be copied here from the conversations.
