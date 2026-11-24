# Audit: `server/src/GameLogic/PlayerActions/Craftings`

- **Unit**: `server/src/GameLogic/PlayerActions/Craftings`
- **Files read in full**: 20 of 20 (1,739 lines total)
  - `BaseEventTicketCrafting.cs` (129), `BloodCastleTicketCrafting.cs` (46), `ChaosWeaponAndFirstWingsCrafting.cs` (67), `DarkHorseCrafting.cs` (40), `DevilSquareTicketCrafting.cs` (40), `DinorantCrafting.cs` (71), `FenrirUpgradeCrafting.cs` (122), `FenrirUpgradeCraftingGold.cs` (193), `GuardianOptionCrafting.cs` (74), `IllusionTempleTicketCrafting.cs` (44), `LuckyItemTicketExchangeCrafting.cs` (97), `MountSeedSphereCrafting.cs` (179), `RefineStoneCrafting.cs` (174), `RemoveSeedSphereCrafting.cs` (54), `RestoreItemCrafting.cs` (45), `SecondWingsCrafting.cs` (55), `SeedSphereCrafting.cs` (78), `SocketWeaponCrafting.cs` (122), `ThirdWingsCrafting.cs` (83), `TransientItemCraftingRequiredItem.cs` (24)
- **Suite references**:
  - Season 6 & Season 9 `IGCData/IGC_ChaosBox.ini`, `docs/audit/findings/socket-weapon-mix.md` (S9 `Mix.bmd` ID 57, `ItemList.xml`, `AppointItemDrop.xml`)
  - `docs/audit/data/diff-mixes.txt`, `docs/audit/data/s9/diff-mixes.txt`

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Socket Weapon Mix (`SocketWeaponCrafting`, Mix 57) | Requires 1 socket item with a mounted Seed Sphere + 1 unmounted Seed Sphere + Chaos + Creation + Note (`14-191..203`, incl. `14-203` Frick Large Glove); forces `SocketCount = max(5, MaximumSockets)` | S9 `Mix.bmd` (recipes `113..124`, ID `57`): requires 3× Socket items (`+7+`, `+4+` opt) + 1× Note (`14-191..202`) + 2× Raklion mat (`14-180..184`) + 1× Dark Raklion mat (`14-185..188`); `14-203`/`0-38` disabled; 40% cap, 1M zen | **Differs** (different ingredients, includes `14-203`, forces 5 sockets) |
| Event Ticket Craftings (`BloodCastleTicketCrafting`, `DevilSquareTicketCrafting`, `IllusionTempleTicketCrafting`) | BC: 80% (+Crywolf <=100%), zen 50k..1.05M; DS: 80%(1..4)/70%(5..7) (+Crywolf <=100%), zen 100k..2M; IT: 70% (+Crywolf <=80%), zen 3M..13M | Same S6/S9 ticket prices and base rates | Same |
| Dinorant, Dark Horse, Fenrir (`DinorantCrafting`, `DarkHorseCrafting`, `FenrirUpgradeCrafting`, `FenrirUpgradeCraftingGold`) | Dinorant checks Uniria `Durability == 255` (30% option + 20% second distinct option); Fenrir Black/Blue/Gold (`+11+` Exc) 10M zen, max 79% rate | Same S6/S9 pet mix rules | Same |
| 1st / 2nd / 3rd Wings & 380 Guardian (`ChaosWeaponAndFirstWingsCrafting`, `SecondWingsCrafting`, `ThirdWingsCrafting`, `GuardianOptionCrafting`) | Standard random option/luck/skill/wing-exc rolls & `GuardianOption` attachment (`0x88` ref) | Standard S6/S9 wing & 380 mix rules | Same |
| Seed Sphere & Socket Mount/Remove (`SeedSphereCrafting`, `MountSeedSphereCrafting`, `RemoveSeedSphereCrafting`) | Seed `12:60..65` + Sphere `12:70..74` -> `12:100+`; 30% bonus socket option on 3rd mount for Fire-Lightning-Ice / Lightning-Ice-Fire (weapons) or Water-Earth-Wind / Earth-Wind-Water (armors); removing slot `< 3` deletes bonus option | Same S6/S9 socket mix & bonus socket element orders | Same |
| Harmony Refine & Restore (`RefineStoneCrafting`, `RestoreItemCrafting`) | Lower 20% (`14:43`), Higher 50% (`14:44`), excludes low-tier < +4 / Ancient / Harmonied; Restore removes Harmony for `100k..500k` zen | Same S6/S9 JOL/JOH refine & Osbourne restore rules | Same |
| Lucky Item Exchange (`LuckyItemTicketExchangeCrafting`) | Class-matched exchange preferring fewest qualified classes then lowest number; checks inventory space first | Same S6/S9 Lucky Item ticket exchange | Same |
