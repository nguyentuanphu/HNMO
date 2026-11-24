# Unit: client/web/src/game (files 1)

Checked against: desktop client `MUHelper`, `CharacterManager`, `ZzzInfomation`, and `ZzzInventory` formulas + HNMO custom features.

Scope: `client/web/src/game (files 1)` (`12` files, `5492` lines: `accountSafety.ts`, `autoPlay*.ts`, `buffs.ts`, `characterCalc.ts`, `castleSiege.ts`, `chat*.ts`).

## Findings

| File & Line | Rule / Formula | Desktop / Suite Role & Value | Result |
|---|---|---|---|
| `autoPlay.ts:16-23,588-713` | Pickup retry `1s/3s`, target retry `2s`, `DEFAULT_DURABILITY_THRESHOLD = 50%`, `MAX_ACTIONABLE_DISTANCE = 10`, 3-slot skill rotation & combo cycle | Ports desktop `MUHelper::CMuHelper::Work` | Same |
| `autoPlayConfig.ts:70-77` | `initialAutoPlayConfig()`: hunting range `6`, obtaining `8`, max away `10s`, buff duration `true`, heal `60%`, potion `40%` | Desktop `CMuHelperConfigWindow::Reset` defaults | Same |
| `autoPlayExtras.ts:51-67` | Default Free (`sparkleGuide`, `presetsStats`) vs VIP-gated (`manaSdPotions`, `jewelDeposit`, `targetPriority`, `patrol`, `survival`, `bossFocus`) policy | HNMO custom Ultimate Auto Play tiering | HNMO custom |
| `characterCalc.ts:95-247` | Durability degradation steps (`>50%/70%/80%` -> `0.2/0.3/0.5`), per-class base min/max damage (`str/dex/ene/vit`), bolt/arrow bonus | Desktop `CalcDurabilityPercent` & `CalculateDamage` | Same |
