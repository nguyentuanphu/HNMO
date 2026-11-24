# Unit Audit: `server/src/GameLogic/ServerImporter`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (32 files read in full)

## Summary of Rules & Behavior

- **Version Detection (`LegacyFileParsers.DetectVersion`)**: Identifies Season 9+ via `BattleCore`, `PentagramOption*.xml`, `ArcaBattle*.xml`, `TormentedSquare*.xml`, or `MaxMasterLevel > 200` (noting Muun XML alone also exists in Season 6 Ep3).
- **Config & Data Parsers (`LegacyFileParsers`)**:
  - Rates (`ExpSystem*.xml`, `CommonServer.cfg`, `*Common.ini`, `ZenDrop*.xml`, `MapAttribute*.xml`).
  - Drops (`DropManager*.xml`, `SetItemDropManager*.xml`, `ItemBags/*.xml` separating `Monster_(N)` from `Item_(g,n,l)` boxes and `Event_BloodCastle/ChaosCastle(N)_Reward`, `AppointItemDrop*.xml`).
  - Shops (`ShopList*.xml`, skipping `GMShop="1"`).
  - Events & Spawns (`BonusEvent*.xml`, `.dat` & XML event schedules, `InvasionManager*.xml`/`EventsManager.dat`, `MonsterSetBase*.txt` excluding `_CS` when normal exists, `MonsterSpawn*.xml`, `MonsterList.xml`).
  - Binary storage decoding (`ItemSlotSizeOf`: 32 bytes when `>= 7000 && % 32 == 0`, else 16 bytes).
- **Import & Undo Engine (`LegacyServerImportEngine`, `MssqlLegacyDatabaseReader`)**:
  - `BuildDryRunPreviewAsync`, `ApplyImportAsync` (writes atomic `LegacyImportBackupSnapshot` before mutation), and `UndoBackupAsync`.
  - `MssqlLegacyDatabaseReader`: reads `MEMB_INFO` (excluding password columns), `Character`, `warehouse`, `Guild`, `GuildMember` on Windows; passwords are re-hashed with BCrypt (`ForceRandomReset` or `UseTemporaryPassphrase`).

No defects found.
