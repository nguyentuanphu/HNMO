# Friends across seasons (owner's choice, 2026-11-21, built 02:20-03:00 Sydney)

- Every HNMO program (Season 6.3, Season 9, the Arena) publishes who plays on each of its game servers every 15 s in
  the shared Season 6.3 database (`data."HnmoOnlinePlayer"`: login, character, program, server; rows older than two
  minutes count as gone). The login name is the key: accounts are shared but their ids differ between the programs.
- `CrossSeasonFriendsPlugIn` ("HNMO friends across seasons", EN/VI): every 20 s each player's friends who aren't
  online in this program are looked up by their account's login; a friend playing elsewhere is shown with the friend
  list "server" 0xE0 / 0xE1 / 0xE2, which the web game names "Season 6.3" / "Season 9" / "Arena" (back to Offline when
  they leave).
- Whispers: a name not online here is looked up elsewhere (by that character's name, or by the account of a character
  of this program); the whisper is left in `data."HnmoCrossWhisper"` for that program, which delivers it within 2 s
  as "(Season 6.3) text" from the sender's name; the sender is told where it went. Replies work the same way. No
  trading, parties or mail between programs.
- Checked live: test6Dk (Season 6.3) and test7Dk made friends in the test save; test7 in the Arena showed as "Arena"
  in test6's list, and whispers went both ways (`friends-across-seasons.spec.ts`, opt-in with ARENA_BATTLES=1). Tests
  `CrossSeasonFriendsTest`.
