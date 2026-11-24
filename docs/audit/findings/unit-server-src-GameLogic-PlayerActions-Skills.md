# Audit: `server/src/GameLogic/PlayerActions/Skills`

- **Unit**: `server/src/GameLogic/PlayerActions/Skills`
- **Files read in full**: 29 of 29 (3,350+ lines total)
  - `AreaSkillAttackAction.cs` (339), `AreaSkillHitAction.cs` (42), `AreaSkillTargetSelector.cs` (214), `AttackActionValidator.cs` (100), `BloodSlashSkillPlugIn.cs` (49), `ChainLightningSkillPlugIn.cs` (91), `DeferredHitScheduler.cs` (455), `DragonRoarSkillPlugIn.cs` (94), `DrainLifeSkillPlugIn.cs` (51), `EarthShakeSkillPlugIn.cs` (57), `FireScreamSkillPlugIn.cs` (98), `ForceSkillAction.cs` (94), `ForceWaveStrengSkillAction.cs` (34), `FrustumBasedTargetFilter.cs` (275), `NovaSkillStartPlugin.cs` (235), `NovaSkillStopPlugin.cs` (26), `PhoenixShotSkillPlugIn.cs` (23), `PollutionSkillPlugIn.cs` (59), `RageSkillAttackAction.cs` (152), `RequiemSkillPlugIn.cs` (55), `SkillCancellationTokenSource.cs` (29), `SkillEquipmentValidator.cs` (134), `SoulBarrierProficieSkillAction.cs` (20), `SoulBarrierStrengSkillAction.cs` (95), `SoulDrainSkillPlugIn.cs` (53), `SummonPartySkillPlugin.cs` (207), `TargetedSkillDefaultPlugin.cs` (324), `TargetedSkillPluginBase.cs` (23), `TwistingSlashMasterySkillPlugIn.cs` (59)
- **Suite references**:
  - Season 6 & Season 9 `the suite's regular skill calc script`, `MasterSkillCalc.lua`, `the suite's skill list file`

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Attack & equipment validation (`AttackActionValidator`, `SkillEquipmentValidator`) | Blocks stun/sleep/safezone/no-ammo, checks mount/weapon rules for Fenrir, Impale, Earthshake, Death Stab, Spiral Slash, Mana Rays, Rageful Blow, Fire Slash, Destruction, Flame Strike, Power Slash, Elf bows, RF gloves (`0:32..35, 38`) | Standard S6/S9 weapon/mount skill restrictions | Same (Note: S9 RF skill `PhoenixShot` `270` is not yet in `SkillEquipmentValidator` switch) |
| Area skill frustum, deferred hits & Electric Spike (`AreaSkillAttackAction`, `AreaSkillTargetSelector`, `DeferredHitScheduler`, `FrustumBasedTargetFilter`) | Rounded angle->rotation, frustum lanes with point-blank overlap, wavefront sweeps; Electric Spike (`65`) drains nearby party HP 20% / Mana 5% | Standard projectile spread, range & Electric Spike party cost | Same |
| Chain Lightning (`215`), Drain Life (`214`), Fire Scream (`78`), Nova (`58`/`40`) | Chain Lightning: 300ms hops, 70%/50% dmg (re-hits target if alone); Drain Life: PvP `Ene/23 + Dmg*0.1`, PvM `Ene/15 + Lvl/2.5`; Fire Scream: 30% chance radius 2 10% dmg; Nova: 12 steps `[20..1320]` at 500ms | Same S6/S9 skill formulas | Same |
| Dragon Roar (`264`) & Phoenix Shot (`270`) (`DragonRoarSkillPlugIn`, `PhoenixShotSkillPlugIn`) | Up to 7 extra targets (first 3 @ 100%, next 4 @ 50%); Dragon Roar range 3, Phoenix Shot range 2 | Standard S6/S9 RF AoE splash counts | Same |
| Summon Party (`63`), Soul Barrier (`403`/`404`), Push/Stun passives | Summon Party: 5s countdown, blocks PK2+, checks warp rules; Soul Barrier Streng/Proficie adds `0.1% * lvl` max mana toll per hit; Earthshake pushes 3 tiles, Pollution/Twisting Slash push 2 tiles, Requiem stuns 3s | Same S6/S9 skill mechanics | Same |
| Rage skills (`RageSkillAttackAction`) & HNMO skills (`701` Blood Slash, `723` Soul Drain, `711`/`721` summons) | Max 5 targets, 2 hits @ 200ms, range+2 tolerance; Blood Slash heals 6% dmg; Soul Drain steals 12% mana | Standard RF Rage Attack + HNMO custom skills | Same (custom skills HNMO-specific) |
