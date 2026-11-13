// How the hero casts a skill and what a caster plays when the server shows a skill,
// as the desktop client does it. The cast side follows ExecuteSkill / CanExecuteSkill
// (GameLogic/Combat/SkillExecution.cpp), the per-class executors AttackElf,
// AttackKnight, AttackWizard and AttackCommon (GameLogic/Combat/ClassAttack.cpp) and
// SkillWarrior, CastWarriorSkill, UseSkillWarrior, UseSkillWizard, UseSkillElf,
// UseSkillSummon and SkillElf (GameLogic/Combat/SkillCast.cpp). The receive side
// follows ReceiveMagic (SkillAnimation, C3 19) and ReceiveMagicContinue
// (AreaSkillAnimation, C3 1E) in Network/Server/WSclient.cpp. Each case names the
// desktop function or branch it comes from.
//
// Only the base (non-master) skills are covered; the master variants (_STR,
// _MASTERY, numbers 300 and up) and the Rage Fighter's skills return null.

import {
  CharacterClass,
  PlayerActions,
  isFemaleClass,
  playerAttackAction,
  type HandItem,
  type Mount,
  type RandomSource,
} from './combatLook.js';
import { isNewSkill, NEW_SKILL_LOOKS } from './newSkills.js';

// ---------------------------------------------------------------------------
// Skill numbers (the desktop's AT_SKILL_* values, Core/Globals/_enum.h).
// ---------------------------------------------------------------------------
export const SkillId = {
  POISON: 1,
  METEO: 2,
  LIGHTNING: 3,
  FIREBALL: 4,
  FLAME: 5,
  TELEPORT: 6,
  ICE: 7,
  STORM: 8,
  EVIL_SPIRIT: 9,
  HELL_FIRE: 10,
  POWERWAVE: 11,
  FLASH: 12,
  BLAST: 13,
  INFERNO: 14,
  TELEPORT_ALLY: 15,
  SOUL_BARRIER: 16,
  ENERGYBALL: 17,
  BLOCKING: 18,
  FALLING_SLASH: 19,
  LUNGE: 20,
  UPPERCUT: 21,
  CYCLONE: 22,
  SLASH: 23,
  TRIPLE_SHOT: 24,
  BOW: 25,
  HEALING: 26,
  DEFENSE: 27,
  ATTACK: 28,
  SUMMON: 30,
  /** AT_SKILL_SUMMON + 6, the last elf summon the desktop build has (ADD_ELF_SUMMON is off). */
  SUMMON_LAST: 36,
  DECAY: 38,
  ICE_STORM: 39,
  NOVA: 40,
  TWISTING_SLASH: 41,
  RAGEFUL_BLOW: 42,
  DEATHSTAB: 43,
  RUSH: 44,
  JAVELIN: 45,
  DEEPIMPACT: 46,
  IMPALE: 47,
  SWELL_LIFE: 48,
  RIDER: 49,
  ICE_ARROW: 51,
  PENETRATION: 52,
  IMPROVE_AG: 53,
  BLAST_CROSSBOW4: 54,
  FIRE_SLASH: 55,
  POWER_SLASH: 56,
  SPIRAL_SLASH: 57,
  NOVA_BEGIN: 58,
  COMBO: 59,
  FORCE: 60,
  FIREBURST: 61,
  EARTHSHAKE: 62,
  PARTY_TELEPORT: 63,
  ADD_CRITICAL: 64,
  THUNDER_STRIKE: 65,
  FORCE_WAVE: 66,
  STUN: 67,
  REMOVAL_STUN: 68,
  MANA: 69,
  INVISIBLE: 70,
  REMOVAL_INVISIBLE: 71,
  REMOVAL_BUFF: 72,
  DEATH_CANNON: 73,
  SPACE_SPLIT: 74,
  BRAND_OF_SKILL: 75,
  PLASMA_STORM_FENRIR: 76,
  INFINITY_ARROW: 77,
  FIRE_SCREAM: 78,
  ALICE_DRAINLIFE: 214,
  ALICE_CHAINLIGHTNING: 215,
  ALICE_LIGHTNINGORB: 216,
  ALICE_THORNS: 217,
  ALICE_BERSERKER: 218,
  ALICE_SLEEP: 219,
  ALICE_BLIND: 220,
  ALICE_WEAKNESS: 221,
  ALICE_ENERVATION: 222,
  LIGHTNING_SHOCK: 230,
  STRIKE_OF_DESTRUCTION: 232,
  EXPANSION_OF_WIZARDRY: 233,
  RECOVER: 234,
  MULTI_SHOT: 235,
  FLAME_STRIKE: 236,
  GIGANTIC_STORM: 237,
  CHAOTIC_DISEIER: 238,
} as const;

const S = SkillId;
const A = PlayerActions;

const C = CharacterClass;

// ---------------------------------------------------------------------------
// Public types.
// ---------------------------------------------------------------------------

export type SkillPacket = 'targeted' | 'area' | 'none';

/** A cast that does not go through TargetedSkill / AreaSkill. */
export type SkillSpecial =
  /** AT_SKILL_TELEPORT: EnterGateRequest(0, x, y) to the clicked walkable tile (AttackWizard). */
  | 'teleport'
  /** AT_SKILL_TELEPORT_ALLY: TeleportTarget(partyMemberKey, x, y) to a free tile next to the hero. */
  | 'teleportAlly'
  /** AT_SKILL_NOVA_BEGIN: TargetedSkill(58, own key) when the skill key goes down; Nova (40) follows on release. */
  | 'novaBegin'
  /** AT_SKILL_NOVA: TargetedSkill(40, selected key, else own key) on release; not throttled. */
  | 'nova';

export interface SkillCastPlan {
  /** What the hero sends when casting. */
  packet: SkillPacket;
  /** TargetedSkill with the hero's own id (buffs/heals on self when no friendly target). */
  targetSelf: boolean;
  /** Only castable at a selected monster/player (the desktop's CheckAttack()). */
  needsTarget: boolean;
  /** Cast on allies (heal/buffs) rather than enemies. */
  friendly: boolean;
  /** PlayerAction the hero plays when casting (null: none; the server's animation may set one). */
  heroAction: number | null;
  /** The desktop's Attacking = 2 vs -1 after the cast (only with auto attack on, outside the Stadium and Chaos Castle). */
  keepAutoAttack: boolean;
  /** Whether the desktop follows up with AreaSkillHit (C3 DB). It never does: the desktop has no sender for it. */
  sendAreaHit: boolean;
  /** AreaSkill only: the X/Y sent are the hero's tile ('self') or the target's / clicked tile ('target'). */
  areaAt: 'self' | 'target' | null;
  /** AreaSkill only: the selected monster/player goes in ExtraTargetId (else 0xFFFF). */
  areaExtraTarget: boolean;
  /** AreaSkill only: Rotation = angle / 360 * this (256 for most skills, 255 where the desktop writes 255). */
  rotationScale: 256 | 255;
  /** The desktop first sends a one-step CharacterMove on the hero's own tile (turns the hero towards the target). */
  turnFirst: boolean;
  /** The equipment allows the cast (melee weapon, bow, spear, mount... as the desktop checks it). */
  equipmentOk: boolean;
  /** The skill is cast from the weapon's skill list (SkillWarrior / SkillElf loop over the weapon's Special). */
  fromWeapon: boolean;
  /** Not a TargetedSkill / AreaSkill cast (packet is then 'targeted' for Nova, 'none' for the teleports). */
  special: SkillSpecial | null;
  /** The desktop function the plan follows. */
  source: string;
}

export interface SkillCastContext {
  /** weaponKind of the right-hand item (undefined: empty hand). */
  rightHandKind?: string;
  /** weaponKind of the left-hand item (undefined: empty hand). */
  leftHandKind?: string;
  /**
   * Riding a mount outside the safe zone. Used when `mount` is not given; a ride
   * mount (Uniria/Dinorant) is then assumed for the mount-specific actions.
   */
  mounted?: boolean;
  /** The worn mount (the desktop's Helper.Type); takes precedence over `mounted`. */
  mount?: Mount;
  /** In a safe zone (the desktop's SafeZone); mount actions only play outside it. */
  safeZone?: boolean;
  /** Wings are worn (bows and crossbows then use their flying attack). */
  wings?: boolean;
  /** Defaults to isFemaleClass(classIndex). */
  female?: boolean;
  /** The hand items, when known: used for the plain-attack animation some bow skills play. */
  rightHand?: HandItem | null;
  leftHand?: HandItem | null;
  /** On Tarkan, Icarus or in the Kanturu Maya scene (AT_SKILL_RIDER plays its flying action). */
  flyingMap?: boolean;
  /** Random source for the actions the desktop draws at random (Math.random by default). */
  rand?: RandomSource;
}

export interface SkillAnimationContext extends SkillCastContext {
  /** The caster is the own hero (the desktop leaves many of the hero's actions to the cast). */
  own?: boolean;
  /** Which packet showed the skill; defaults to the packet the skill is cast with. */
  via?: 'targeted' | 'area';
  /** The caster's swing counter (SwordCount); AT_SKILL_SLASH alternates on it. */
  swordCount?: number;
}

// ---------------------------------------------------------------------------
// Equipment and mount helpers.
// ---------------------------------------------------------------------------

interface Gear {
  mount: Mount;
  /** Mount actions play: a mount is worn and the caster is outside the safe zone. */
  riding: boolean;
  right: string | undefined;
  left: string | undefined;
  hasRight: boolean;
  hasLeft: boolean;
  bow: 'bow' | 'crossbow' | null;
}

function gearOf(ctx: SkillCastContext): Gear {
  const mount: Mount = ctx.mount ?? (ctx.mounted ? 'uniria' : 'none');
  const riding = ctx.mount !== undefined ? mount !== 'none' && !(ctx.safeZone ?? false) : (ctx.mounted ?? false);
  const right = ctx.rightHandKind ?? ctx.rightHand?.weaponKind;
  const left = ctx.leftHandKind ?? ctx.leftHand?.weaponKind;
  // CCharacterManager::GetEquipedBowType: a bow in the left hand, or a crossbow in the right.
  const bow = left === 'bow' ? 'bow' : right === 'crossbow' ? 'crossbow' : null;
  return {
    mount,
    riding,
    right,
    left,
    hasRight: (ctx.rightHand ?? null) !== null || right !== undefined,
    hasLeft: (ctx.leftHand ?? null) !== null || left !== undefined,
    bow,
  };
}

const isStaffOrBook = (kind: string | undefined): boolean => kind === 'staff' || kind === 'book';
const isRideMount = (m: Mount): boolean => m === 'uniria' || m === 'dinorant';
// Helper.Type in [MODEL_HORN_OF_UNIRIA, MODEL_DARK_HORSE_ITEM]: Uniria, Dinorant, Dark Horse.
const isUniriaToDarkHorse = (m: Mount): boolean => m === 'uniria' || m === 'dinorant' || m === 'darkHorse';

/** SetPlayerMagic (Engine/Object/ZzzCharacter.cpp). */
function magicAction(classIndex: number, ctx: SkillCastContext): number {
  const g = gearOf(ctx);
  if (isRideMount(g.mount) && g.riding) return A.RIDE_SKILL;
  if (g.mount === 'fenrir' && g.riding) return A.FENRIR_ATTACK_MAGIC;
  if (ctx.female ?? isFemaleClass(classIndex)) return A.SKILL_ELF1;
  const rand = ctx.rand ?? Math.random;
  return A.SKILL_HAND1 + (Math.floor(rand() * 2) % 2);
}

/** SetPlayerAttack (Engine/Object/ZzzCharacter.cpp), through combatLook's port of it. */
function attackAction(classIndex: number, ctx: SkillCastContext): number {
  const g = gearOf(ctx);
  const pseudo = (kind: string | undefined): HandItem | null => (kind === undefined ? null : { group: -1, number: -1, weaponKind: kind });
  return playerAttackAction(
    {
      classIndex,
      rightHand: ctx.rightHand !== undefined ? ctx.rightHand : pseudo(g.right),
      leftHand: ctx.leftHand !== undefined ? ctx.leftHand : pseudo(g.left),
      mount: g.mount,
      safeZone: !g.riding,
      ...(ctx.wings !== undefined ? { wings: ctx.wings } : {}),
    },
    0,
    ctx.rand ?? Math.random,
  );
}

/** SetPlayerBow (Network/Server/WSclient.cpp): only an elf outside the safe zone; else no change. */
function bowAction(classIndex: number, ctx: SkillCastContext): number | null {
  const g = gearOf(ctx);
  if (classIndex !== C.ELF || (ctx.safeZone ?? false)) return null;
  if (g.bow === 'bow') {
    if (g.mount === 'fenrir') return A.FENRIR_ATTACK_BOW;
    if (isRideMount(g.mount)) return A.ATTACK_RIDE_BOW;
    return ctx.wings ? A.ATTACK_FLY_BOW : A.ATTACK_BOW;
  }
  if (g.bow === 'crossbow') {
    if (g.mount === 'fenrir') return A.FENRIR_ATTACK_CROSSBOW;
    if (isRideMount(g.mount)) return A.ATTACK_RIDE_CROSSBOW;
    return ctx.wings ? A.ATTACK_FLY_CROSSBOW : A.ATTACK_CROSSBOW;
  }
  return null;
}

/** SetPlayerHighBowAttack (Engine/Object/ZzzCharacter.cpp): the hero's Deep Impact. */
function highBowAttackAction(ctx: SkillCastContext): number | null {
  const g = gearOf(ctx);
  if (isRideMount(g.mount) && g.riding) {
    if (g.bow === 'bow') return A.ATTACK_RIDE_BOW_UP;
    if (g.bow === 'crossbow') return A.ATTACK_RIDE_CROSSBOW_UP;
    return null;
  }
  if (g.bow === 'bow') return ctx.wings ? A.ATTACK_FLY_BOW_UP : A.ATTACK_BOW_UP;
  if (g.bow === 'crossbow') return ctx.wings ? A.ATTACK_FLY_CROSSBOW_UP : A.ATTACK_CROSSBOW_UP;
  return null;
}

/** SetPlayerHighBow (Network/Server/WSclient.cpp): Deep Impact as the SkillAnimation shows it. */
function highBowAction(ctx: SkillCastContext): number | null {
  const g = gearOf(ctx);
  if (g.bow === 'bow') {
    if (g.mount === 'fenrir' || isRideMount(g.mount)) return A.ATTACK_RIDE_BOW_UP;
    return ctx.wings ? A.ATTACK_FLY_BOW_UP : A.ATTACK_BOW_UP;
  }
  if (g.bow === 'crossbow') {
    if ((g.mount === 'fenrir' && !(ctx.safeZone ?? false)) || isRideMount(g.mount)) return A.ATTACK_RIDE_CROSSBOW_UP;
    return ctx.wings ? A.ATTACK_FLY_CROSSBOW_UP : A.ATTACK_CROSSBOW_UP;
  }
  return null;
}

/** SetAction_Fenrir_Skill (Engine/AI/ZzzAI.cpp): Plasma Storm. */
function fenrirSkillAction(classIndex: number, ctx: SkillCastContext): number {
  const g = gearOf(ctx);
  if (classIndex === C.RAGE_FIGHTER) {
    if (g.hasRight && g.hasLeft) return A.RAGE_FENRIR_TWO_SWORD;
    if (g.hasRight) return A.RAGE_FENRIR_ONE_RIGHT;
    if (g.hasLeft) return A.RAGE_FENRIR_ONE_LEFT;
    return A.RAGE_FENRIR;
  }
  if (g.hasRight && g.hasLeft) return A.FENRIR_SKILL_TWO_SWORD;
  if (g.hasRight) return A.FENRIR_SKILL_ONE_RIGHT;
  if (g.hasLeft) return A.FENRIR_SKILL_ONE_LEFT;
  return A.FENRIR_SKILL;
}

/** The Dark Lord's strike: Uniria..Dark Horse ride strike, Fenrir strike, else on foot (safe zone checked). */
function strikeActionOutsideSafeZone(ctx: SkillCastContext): number {
  const g = gearOf(ctx);
  if (isUniriaToDarkHorse(g.mount) && g.riding) return A.ATTACK_RIDE_STRIKE;
  if (g.mount === 'fenrir' && g.riding) return A.FENRIR_ATTACK_DARKLORD_STRIKE;
  return A.ATTACK_STRIKE;
}

/** The same strike where the desktop does not check the safe zone (Fenrir first). */
function strikeActionAnyZone(ctx: SkillCastContext): number {
  const g = gearOf(ctx);
  if (g.mount === 'fenrir') return A.FENRIR_ATTACK_DARKLORD_STRIKE;
  if (isUniriaToDarkHorse(g.mount)) return A.ATTACK_RIDE_STRIKE;
  return A.ATTACK_STRIKE;
}

/** The castle skills' action (AttackCommon / ReceiveMagic AT_SKILL_STUN..AT_SKILL_REMOVAL_BUFF). */
function commonSkillAction(ctx: SkillCastContext, onFoot: number): number {
  const g = gearOf(ctx);
  if (!g.riding) return onFoot;
  switch (g.mount) {
    case 'darkHorse': return A.ATTACK_RIDE_ATTACK_MAGIC;
    case 'uniria': return A.SKILL_RIDER;
    case 'dinorant': return A.SKILL_RIDER_FLY;
    case 'fenrir': return A.FENRIR_ATTACK_MAGIC;
    default: return onFoot;
  }
}

/**
 * The summoner's actions come in fours: on foot, Uniria, Dinorant, Fenrir
 * (PLAYER_SKILL_SLEEP, _UNI, _DINO, _FENRIR and so on). The desktop picks by
 * Helper.Type without checking the safe zone.
 */
function summonerAction(ctx: SkillCastContext, onFoot: number): number {
  switch (gearOf(ctx).mount) {
    case 'uniria': return onFoot + 1;
    case 'dinorant': return onFoot + 2;
    case 'fenrir': return onFoot + 3;
    default: return onFoot;
  }
}

/** The Dark Lord's buffs (ReceiveMagic AT_SKILL_ADD_CRITICAL, AT_SKILL_BRAND_OF_SKILL). */
function lordMagicAction(ctx: SkillCastContext): number {
  const g = gearOf(ctx);
  if (g.mount === 'darkHorse' && g.riding) return A.ATTACK_RIDE_ATTACK_MAGIC;
  if (g.mount === 'fenrir' && g.riding) return A.FENRIR_ATTACK_MAGIC;
  return A.SKILL_HAND1;
}

// ---------------------------------------------------------------------------
// Auto attack after the cast (ExecuteSkill).
// ---------------------------------------------------------------------------

/**
 * ExecuteSkill: with the auto-attack option on (outside the Stadium and Chaos
 * Castle) the desktop sets Attacking = 2 (keep attacking the target with the skill)
 * or -1 (stop) by class and skill.
 */
export function skillKeepsAutoAttack(skill: number, classIndex: number): boolean {
  if (classIndex === C.ELF) {
    // Elf: only the bow skills keep attacking.
    return ([S.TRIPLE_SHOT, S.MULTI_SHOT, S.BOW, S.PENETRATION, S.BLAST_CROSSBOW4, S.PLASMA_STORM_FENRIR] as readonly number[]).includes(skill);
  }
  if (classIndex === C.DARK_KNIGHT && skill === S.SWELL_LIFE) return false;
  if (classIndex === C.DARK_LORD && (skill === S.ADD_CRITICAL || skill === S.PARTY_TELEPORT)) return false;
  if (classIndex === C.DARK_WIZARD && (skill === S.NOVA_BEGIN || skill === S.NOVA)) return false;
  if (skill >= S.STUN && skill <= S.REMOVAL_BUFF) return false;
  if (skill === S.BRAND_OF_SKILL) return false;
  if (
    ([S.ALICE_THORNS, S.ALICE_BERSERKER, S.ALICE_SLEEP, S.ALICE_BLIND, S.ALICE_WEAKNESS, S.ALICE_ENERVATION] as readonly number[]).includes(skill)
  ) {
    return false;
  }
  // AT_SKILL_ATT_UP_OURFORCES..AT_SKILL_DEF_UP_OURFORCES (266..268).
  if (skill >= 266 && skill <= 268) return false;
  return true;
}

// IsPartyMemberSkill (PartyMemberSkill.cpp): the elf's, wizard's and summoner's skills that go to a
// selected party member (its id in TargetedSkill) instead of the hero. The master variants count too.
const PARTY_MEMBER_SKILLS: readonly number[] = [
  S.HEALING, 413, S.DEFENSE, 417, 423, S.ATTACK, 420, 422, S.TELEPORT_ALLY,
  S.SOUL_BARRIER, 403, 404, S.ALICE_THORNS, S.RECOVER,
];

export function isPartyMemberSkill(classIndex: number, skill: number): boolean {
  if (classIndex !== C.ELF && classIndex !== C.DARK_WIZARD && classIndex !== C.SUMMONER) return false;
  return PARTY_MEMBER_SKILLS.includes(skill);
}

// ---------------------------------------------------------------------------
// Which executor the desktop runs for a class (ExecuteSkill's dispatch).
// ---------------------------------------------------------------------------

const runsKnight = (cls: number): boolean => cls === C.DARK_KNIGHT || cls === C.MAGIC_GLADIATOR || cls === C.DARK_LORD;
const runsWizard = (cls: number): boolean => cls === C.DARK_WIZARD || cls === C.MAGIC_GLADIATOR || cls === C.SUMMONER;

// SkillWarrior's first list: cast at a selected target through CastWarriorSkill.
const WARRIOR_TARGET_SKILLS: readonly number[] = [
  S.RIDER, S.FIRE_SLASH, S.TWISTING_SLASH, S.DEATHSTAB, S.IMPALE, S.FORCE, S.FORCE_WAVE, S.FIREBURST, S.RUSH, S.SPIRAL_SLASH, S.SPACE_SPLIT,
];
// SkillWarrior's weapon-skill loop (the weapon's Special list).
const WEAPON_SKILLS: readonly number[] = [S.BLOCKING, S.FALLING_SLASH, S.LUNGE, S.UPPERCUT, S.CYCLONE, S.SLASH, S.RIDER];
// NeedsNoMeleeWeapon (ClassAttack.cpp): knight skills that work without a melee weapon.
const NO_MELEE_WEAPON_SKILLS: readonly number[] = [
  S.SWELL_LIFE, S.ADD_CRITICAL, S.PARTY_TELEPORT, S.THUNDER_STRIKE, S.EARTHSHAKE, S.BRAND_OF_SKILL, S.PLASMA_STORM_FENRIR,
  S.FIRE_SCREAM, S.GIGANTIC_STORM, S.CHAOTIC_DISEIER, S.TWISTING_SLASH,
];

// ---------------------------------------------------------------------------
// The cast.
// ---------------------------------------------------------------------------

type PlanParts = Partial<SkillCastPlan> & Pick<SkillCastPlan, 'packet' | 'heroAction' | 'source'>;

function make(skill: number, classIndex: number, p: PlanParts): SkillCastPlan {
  const area = p.packet === 'area';
  return {
    targetSelf: false,
    needsTarget: false,
    friendly: false,
    keepAutoAttack: skillKeepsAutoAttack(skill, classIndex),
    sendAreaHit: false,
    areaAt: area ? 'self' : null,
    areaExtraTarget: area,
    rotationScale: 256,
    turnFirst: false,
    equipmentOk: true,
    fromWeapon: false,
    special: null,
    ...p,
  };
}

/**
 * How the hero casts `skill` (see SkillCastPlan), or null for a skill this port
 * does not cover or that the class's executor does not handle. All casts are
 * refused in a safe zone (see skillCastAllowed).
 */
export function skillCastPlan(
  skill: number,
  classIndex: number,
  context: { rightHandKind?: string; leftHandKind?: string; mounted?: boolean } & SkillCastContext,
): SkillCastPlan | null {
  const ctx = context;
  const g = gearOf(ctx);
  const plan = (p: PlanParts): SkillCastPlan => make(skill, classIndex, p);

  // HNMO's own skills: how each is cast is in its look (game/newSkills.ts).
  if (isNewSkill(skill)) {
    const look = NEW_SKILL_LOOKS[skill];
    if (!look) return null;
    const heroAction = look.action === 'magic' ? magicAction(classIndex, ctx)
      : look.action === 'attack' ? (bowAction(classIndex, ctx) ?? attackAction(classIndex, ctx)) : look.action;
    return plan({
      packet: look.packet, heroAction, needsTarget: look.needsTarget, friendly: look.friendly, targetSelf: look.friendly,
      ...(look.packet === 'area' ? { areaAt: look.areaAt ?? 'target', areaExtraTarget: true } : {}),
      source: 'HNMO skill (game/newSkills.ts)',
    });
  }

  // AttackCommon (ClassAttack.cpp): the castle skills, for every class.
  if (skill >= S.STUN && skill <= S.REMOVAL_BUFF) {
    if (skill === S.STUN) {
      return plan({ packet: 'area', heroAction: commonSkillAction(ctx, A.SKILL_VITALITY), source: 'AttackCommon AT_SKILL_STUN' });
    }
    const onFoot = skill === S.REMOVAL_STUN || skill === S.REMOVAL_INVISIBLE ? A.ATTACK_REMOVAL : A.SKILL_VITALITY;
    return plan({
      packet: 'targeted',
      targetSelf: true,
      friendly: skill !== S.REMOVAL_BUFF,
      heroAction: commonSkillAction(ctx, onFoot),
      source: 'AttackCommon (SendRequestMagic to the selected character, else the hero)',
    });
  }

  if (classIndex === C.ELF) return elfPlan(skill, classIndex, ctx, g, plan);
  if (classIndex === C.RAGE_FIGHTER) return null; // AttackRagefighter: not ported.

  if (runsKnight(classIndex)) {
    const knight = knightPlan(skill, classIndex, ctx, g, plan);
    if (knight) return knight;
  }
  if (runsWizard(classIndex)) return wizardPlan(skill, classIndex, ctx, g, plan);
  return null;
}

function elfPlan(
  skill: number,
  classIndex: number,
  ctx: SkillCastContext,
  g: Gear,
  plan: (p: PlanParts) => SkillCastPlan,
): SkillCastPlan | null {
  if (skill >= S.SUMMON && skill <= S.SUMMON_LAST) {
    // AttackElf AT_SKILL_SUMMON: not on Icarus, in Chaos Castle or in the Kanturu Maya scene.
    return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: magicAction(classIndex, ctx), source: 'AttackElf AT_SKILL_SUMMON' });
  }
  switch (skill) {
    case S.HEALING:
    case S.DEFENSE:
    case S.ATTACK:
      // AttackElf first switch: at a selected player (walks into range), else on the hero;
      // a selected monster cancels the cast (Attacking = -1).
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: magicAction(classIndex, ctx), source: 'AttackElf AT_SKILL_HEALING/ATTACK/DEFENSE' });
    case S.RECOVER:
      // AttackElf AT_SKILL_RECOVER: the selected player, else the hero; no range check.
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: A.RECOVER_SKILL, source: 'AttackElf AT_SKILL_RECOVER' });
    case S.IMPROVE_AG:
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: magicAction(classIndex, ctx), source: 'AttackElf AT_SKILL_IMPROVE_AG' });
    case S.INFINITY_ARROW:
      // AttackElf AT_SKILL_INFINITY_ARROW: only without the buff; RUSH1, or SetPlayerMagic with a mount.
      return plan({
        packet: 'targeted', targetSelf: true, friendly: true,
        heroAction: g.mount !== 'none' ? magicAction(classIndex, ctx) : A.RUSH1,
        source: 'AttackElf AT_SKILL_INFINITY_ARROW',
      });
    case S.TRIPLE_SHOT:
      // SkillElf AT_SKILL_TRIPLE_SHOT: needs arrows and the bow's skill; AreaSkill at the hero's tile.
      return plan({ packet: 'area', heroAction: attackAction(classIndex, ctx), equipmentOk: g.bow !== null, fromWeapon: true, source: 'SkillElf AT_SKILL_TRIPLE_SHOT' });
    case S.PENETRATION:
      // AttackElf AT_SKILL_PENETRATION: a bow or crossbow and arrows.
      return plan({ packet: 'area', heroAction: attackAction(classIndex, ctx), equipmentOk: g.bow !== null, source: 'AttackElf AT_SKILL_PENETRATION' });
    case S.MULTI_SHOT:
      // AttackElf AT_SKILL_MULTI_SHOT: turns first, rotation * 255, SetPlayerBow.
      return plan({ packet: 'area', heroAction: bowAction(classIndex, ctx), rotationScale: 255, turnFirst: true, equipmentOk: g.bow !== null, source: 'AttackElf AT_SKILL_MULTI_SHOT' });
    case S.ICE_ARROW:
      // UseSkillElf AT_SKILL_ICE_ARROW (from AttackElf's wall check): at a monster/player, bow and arrows, dexterity 646+.
      return plan({ packet: 'targeted', needsTarget: true, heroAction: attackAction(classIndex, ctx), equipmentOk: g.bow !== null, source: 'UseSkillElf AT_SKILL_ICE_ARROW' });
    case S.DEEPIMPACT:
      return plan({ packet: 'targeted', needsTarget: true, heroAction: highBowAttackAction(ctx), equipmentOk: g.bow !== null, source: 'UseSkillElf AT_SKILL_DEEPIMPACT' });
    case S.BLAST_CROSSBOW4:
      // SkillElf AT_SKILL_BLAST_CROSSBOW4: rotation * 255.
      return plan({ packet: 'area', heroAction: attackAction(classIndex, ctx), rotationScale: 255, equipmentOk: g.bow !== null, fromWeapon: true, source: 'SkillElf AT_SKILL_BLAST_CROSSBOW4' });
    case S.PLASMA_STORM_FENRIR:
      return plasmaStorm(classIndex, ctx, g, plan, 'AttackElf AT_SKILL_PLASMA_STORM_FENRIR');
    default:
      return null;
  }
}

function plasmaStorm(classIndex: number, ctx: SkillCastContext, g: Gear, plan: (p: PlanParts) => SkillCastPlan, source: string): SkillCastPlan {
  // AT_SKILL_PLASMA_STORM_FENRIR: only at a selected target (else nothing is sent); on Fenrir.
  return plan({ packet: 'area', needsTarget: true, heroAction: fenrirSkillAction(classIndex, ctx), equipmentOk: g.mount === 'fenrir', source });
}

function knightPlan(
  skill: number,
  classIndex: number,
  ctx: SkillCastContext,
  g: Gear,
  plan: (p: PlanParts) => SkillCastPlan,
): SkillCastPlan | null {
  // AttackKnight: a right-hand weapon that is not a staff or book, unless the skill needs none.
  const hasMeleeWeapon = g.right !== undefined && !isStaffOrBook(g.right) && !isStaffOrBook(g.left);
  const meleeOk = hasMeleeWeapon || NO_MELEE_WEAPON_SKILLS.includes(skill);
  // SkillWarrior's checks for its first list.
  const warriorTargetOk = (): boolean => {
    switch (skill) {
      case S.IMPALE: return g.mount !== 'none' && g.right === 'spear';
      case S.DEATHSTAB: return g.right !== undefined && !isStaffOrBook(g.right);
      case S.SPIRAL_SLASH: return g.right === 'sword' || g.right === 'glove';
      default: return true;
    }
  };

  // UseSkillWarrior's action (CastWarriorSkill, from SkillWarrior).
  const warriorAction = (): number | null => {
    switch (skill) {
      case S.IMPALE: return g.mount === 'fenrir' ? A.FENRIR_ATTACK_SPEAR : A.ATTACK_SKILL_SPEAR;
      case S.DEATHSTAB: return A.ATTACK_DEATHSTAB;
      case S.TWISTING_SLASH:
      case S.FIRE_SLASH: return A.ATTACK_SKILL_WHEEL;
      case S.RIDER: return ctx.flyingMap ? A.SKILL_RIDER_FLY : A.SKILL_RIDER;
      case S.FORCE:
      case S.FORCE_WAVE:
      case S.FIREBURST:
      case S.SPACE_SPLIT: return null; // The SkillAnimation from the server plays the strike.
      case S.RUSH: return A.ATTACK_RUSH;
      default:
        if (g.mount === 'fenrir' && g.riding) return A.FENRIR_ATTACK_MAGIC;
        // PLAYER_ATTACK_SKILL_SWORD1 + skill - AT_SKILL_FALLING_SLASH (Falling Slash..Slash: SWORD1..SWORD5).
        return A.ATTACK_SKILL_SWORD1 + skill - S.FALLING_SLASH;
    }
  };

  switch (skill) {
    case S.BLOCKING:
      // SkillWarrior weapon loop AT_SKILL_BLOCKING: DEFENSE1, TargetedSkill on the hero.
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: A.DEFENSE1, fromWeapon: true, source: 'SkillWarrior AT_SKILL_BLOCKING' });
    case S.FALLING_SLASH:
    case S.LUNGE:
    case S.UPPERCUT:
    case S.CYCLONE:
    case S.SLASH:
      // SkillWarrior weapon loop -> CastWarriorSkill -> UseSkillWarrior: TargetedSkill at the target.
      return plan({ packet: 'targeted', needsTarget: true, heroAction: warriorAction(), fromWeapon: true, source: 'SkillWarrior -> CastWarriorSkill -> UseSkillWarrior' });
    case S.RIDER:
      return plan({ packet: 'targeted', needsTarget: true, heroAction: warriorAction(), fromWeapon: true, equipmentOk: g.mount !== 'none', source: 'SkillWarrior AT_SKILL_RIDER -> UseSkillWarrior' });
    case S.DEATHSTAB:
    case S.IMPALE:
    case S.FORCE:
    case S.FORCE_WAVE:
    case S.FIREBURST:
    case S.RUSH:
    case S.SPACE_SPLIT:
      // SkillWarrior first list -> CastWarriorSkill -> UseSkillWarrior: SendRequestMagic(skill, target).
      return plan({ packet: 'targeted', needsTarget: true, heroAction: warriorAction(), equipmentOk: warriorTargetOk(), source: 'SkillWarrior -> CastWarriorSkill -> UseSkillWarrior' });
    case S.TWISTING_SLASH:
      // With a selected target: SkillWarrior -> UseSkillWarrior (AreaSkill after walking up);
      // without: AttackKnight AT_SKILL_TWISTING_SLASH at once. Both: AreaSkill at the hero's tile, WHEEL.
      return plan({ packet: 'area', heroAction: A.ATTACK_SKILL_WHEEL, turnFirst: true, equipmentOk: meleeOk, source: 'SkillWarrior / AttackKnight AT_SKILL_TWISTING_SLASH' });
    case S.FIRE_SLASH:
      // AttackKnight AT_SKILL_FIRE_SLASH: strength 596+; also via SkillWarrior at a target.
      return plan({ packet: 'area', heroAction: A.ATTACK_SKILL_WHEEL, equipmentOk: meleeOk, source: 'AttackKnight AT_SKILL_FIRE_SLASH' });
    case S.POWER_SLASH:
      // AttackKnight AT_SKILL_POWER_SLASH: not with a mount worn; rotation * 255.
      return plan({
        packet: 'area',
        heroAction: g.mount === 'fenrir' && g.riding ? A.FENRIR_ATTACK_MAGIC : A.ATTACK_TWO_HAND_SWORD_TWO,
        rotationScale: 255, turnFirst: true, equipmentOk: meleeOk && g.mount === 'none',
        source: 'AttackKnight AT_SKILL_POWER_SLASH',
      });
    case S.RAGEFUL_BLOW:
      return plan({ packet: 'area', heroAction: A.ATTACK_SKILL_FURY_STRIKE, equipmentOk: meleeOk, source: 'AttackKnight AT_SKILL_RAGEFUL_BLOW' });
    case S.STRIKE_OF_DESTRUCTION:
      // AttackKnight AT_SKILL_STRIKE_OF_DESTRUCTION: AreaSkill at the target tile.
      return plan({ packet: 'area', areaAt: 'target', heroAction: A.SKILL_BLOW_OF_DESTRUCTION, equipmentOk: meleeOk, source: 'AttackKnight AT_SKILL_STRIKE_OF_DESTRUCTION' });
    case S.SWELL_LIFE:
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: A.SKILL_VITALITY, source: 'AttackKnight AT_SKILL_SWELL_LIFE' });
    case S.FLAME_STRIKE:
      return plan({ packet: 'area', heroAction: A.SKILL_FLAMESTRIKE, rotationScale: 255, turnFirst: true, equipmentOk: meleeOk, source: 'AttackKnight AT_SKILL_FLAME_STRIKE' });
    case S.GIGANTIC_STORM:
      return plan({ packet: 'area', heroAction: A.SKILL_GIGANTICSTORM, rotationScale: 255, turnFirst: true, source: 'AttackKnight AT_SKILL_GIGANTIC_STORM' });
    case S.CHAOTIC_DISEIER:
      return plan({ packet: 'area', heroAction: strikeActionAnyZone(ctx), rotationScale: 255, turnFirst: true, source: 'AttackKnight AT_SKILL_CHAOTIC_DISEIER' });
    case S.PARTY_TELEPORT:
      // AttackKnight AT_SKILL_PARTY_TELEPORT: in a party, not in a duel or Doppelganger; the action comes from the SkillAnimation.
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: null, source: 'AttackKnight AT_SKILL_PARTY_TELEPORT' });
    case S.ADD_CRITICAL:
    case S.BRAND_OF_SKILL:
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: null, source: 'AttackKnight AT_SKILL_ADD_CRITICAL / AT_SKILL_BRAND_OF_SKILL' });
    case S.FIRE_SCREAM:
      return plan({ packet: 'area', heroAction: strikeActionOutsideSafeZone(ctx), source: 'AttackKnight AT_SKILL_FIRE_SCREAM' });
    case S.EARTHSHAKE:
      // AttackKnight AT_SKILL_EARTHSHAKE: only on the Dark Horse (alive) outside the safe zone.
      return plan({ packet: 'area', heroAction: A.ATTACK_DARKHORSE, equipmentOk: g.mount === 'darkHorse' && g.riding, source: 'AttackKnight AT_SKILL_EARTHSHAKE' });
    case S.THUNDER_STRIKE: {
      let action: number = A.SKILL_FLASH;
      if (g.mount === 'darkHorse' && g.riding) action = A.ATTACK_RIDE_ATTACK_FLASH;
      else if (g.mount === 'fenrir' && g.riding) action = A.FENRIR_ATTACK_DARKLORD_FLASH;
      return plan({ packet: 'area', heroAction: action, source: 'AttackKnight AT_SKILL_THUNDER_STRIKE' });
    }
    case S.PLASMA_STORM_FENRIR:
      return plasmaStorm(classIndex, ctx, g, plan, 'AttackKnight AT_SKILL_PLASMA_STORM_FENRIR');
    default:
      return null;
  }
}

function wizardPlan(
  skill: number,
  classIndex: number,
  ctx: SkillCastContext,
  g: Gear,
  plan: (p: PlanParts) => SkillCastPlan,
): SkillCastPlan | null {
  switch (skill) {
    // AttackWizard first switch.
    case S.NOVA_BEGIN:
      return plan({ packet: 'targeted', targetSelf: true, heroAction: A.SKILL_HELL_BEGIN, special: 'novaBegin', source: 'AttackWizard AT_SKILL_NOVA_BEGIN' });
    case S.NOVA:
      return plan({ packet: 'targeted', targetSelf: true, heroAction: A.SKILL_HELL_START, special: 'nova', source: 'AttackWizard AT_SKILL_NOVA' });
    case S.SOUL_BARRIER:
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: magicAction(classIndex, ctx), source: 'AttackWizard AT_SKILL_SOUL_BARRIER' });
    case S.HELL_FIRE:
      // No range check: AreaSkill at the hero's tile, no extra target.
      return plan({ packet: 'area', areaExtraTarget: false, heroAction: A.SKILL_HELL, source: 'AttackWizard AT_SKILL_HELL_FIRE' });
    case S.INFERNO:
      return plan({ packet: 'area', areaExtraTarget: false, heroAction: A.SKILL_INFERNO, source: 'AttackWizard AT_SKILL_INFERNO' });
    case S.PLASMA_STORM_FENRIR:
      return plasmaStorm(classIndex, ctx, g, plan, 'AttackWizard AT_SKILL_PLASMA_STORM_FENRIR');
    case S.EXPANSION_OF_WIZARDRY:
      // Only without the buff; the action (SWELL_OF_MP) comes from the SkillAnimation.
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: null, source: 'AttackWizard AT_SKILL_EXPANSION_OF_WIZARDRY' });

    // UseSkillWizard (AttackWizard's wall check, only at a selected monster/player).
    case S.POISON:
    case S.METEO:
    case S.LIGHTNING:
    case S.ENERGYBALL:
    case S.POWERWAVE:
    case S.ICE:
    case S.FIREBALL:
    case S.JAVELIN:
      return plan({ packet: 'targeted', needsTarget: true, heroAction: magicAction(classIndex, ctx), source: 'UseSkillWizard (SendRequestMagic + SetPlayerMagic)' });
    case S.DEATH_CANNON:
      return plan({ packet: 'targeted', needsTarget: true, heroAction: A.ATTACK_DEATH_CANNON, equipmentOk: isStaffOrBook(g.right), source: 'UseSkillWizard AT_SKILL_DEATH_CANNON' });
    case S.BLAST:
      return plan({ packet: 'area', needsTarget: true, areaAt: 'target', heroAction: magicAction(classIndex, ctx), source: 'UseSkillWizard AT_SKILL_BLAST' });

    // AttackWizard's wall-check switch: the clicked point (or target) within range.
    case S.STORM:
    case S.EVIL_SPIRIT:
      return plan({ packet: 'area', areaExtraTarget: false, heroAction: magicAction(classIndex, ctx), source: 'AttackWizard AT_SKILL_STORM / AT_SKILL_EVIL_SPIRIT' });
    case S.FLASH:
      return plan({ packet: 'area', areaExtraTarget: false, heroAction: A.SKILL_FLASH, source: 'AttackWizard AT_SKILL_FLASH' });
    case S.FLAME:
      return plan({ packet: 'area', areaAt: 'target', areaExtraTarget: false, heroAction: magicAction(classIndex, ctx), source: 'AttackWizard AT_SKILL_FLAME' });
    case S.DECAY:
    case S.ICE_STORM:
      return plan({ packet: 'area', areaAt: 'target', heroAction: magicAction(classIndex, ctx), source: 'AttackWizard AT_SKILL_DECAY / AT_SKILL_ICE_STORM' });

    // AttackWizard, CLASS_WIZARD only.
    case S.TELEPORT:
      if (classIndex !== C.DARK_WIZARD) return null;
      return plan({ packet: 'none', heroAction: A.SKILL_TELEPORT, special: 'teleport', source: 'AttackWizard AT_SKILL_TELEPORT (SendEnterGateRequest(0, x, y))' });
    case S.TELEPORT_ALLY:
      if (classIndex !== C.DARK_WIZARD) return null;
      return plan({ packet: 'none', needsTarget: true, friendly: true, heroAction: null, special: 'teleportAlly', source: 'AttackWizard AT_SKILL_TELEPORT_ALLY (SendTeleportTarget)' });
    default:
      break;
  }

  if (classIndex !== C.SUMMONER) return null;
  // AttackWizard, CLASS_SUMMONER -> UseSkillSummon.
  switch (skill) {
    case S.ALICE_DRAINLIFE:
      return plan({ packet: 'area', needsTarget: true, areaAt: 'target', heroAction: summonerAction(ctx, A.SKILL_DRAIN_LIFE), source: 'UseSkillSummon AT_SKILL_ALICE_DRAINLIFE' });
    case S.ALICE_LIGHTNINGORB:
      return plan({ packet: 'area', needsTarget: true, areaAt: 'target', heroAction: summonerAction(ctx, A.SKILL_LIGHTNING_ORB), source: 'UseSkillSummon AT_SKILL_ALICE_LIGHTNINGORB' });
    case S.ALICE_CHAINLIGHTNING:
      return plan({ packet: 'area', needsTarget: true, areaAt: 'target', heroAction: summonerAction(ctx, A.SKILL_CHAIN_LIGHTNING), source: 'UseSkillSummon AT_SKILL_ALICE_CHAINLIGHTNING' });
    case S.ALICE_SLEEP:
    case S.ALICE_BLIND:
      // At a monster; at a player only in Chaos Castle, Illusion Temple, Castle Siege or a duel.
      // No action on the cast (the SkillAnimation plays it).
      return plan({ packet: 'targeted', needsTarget: true, heroAction: null, source: 'UseSkillSummon AT_SKILL_ALICE_SLEEP / AT_SKILL_ALICE_BLIND' });
    case S.ALICE_THORNS:
      // Without a selected player: on the hero with the sleep action; at a player: UseSkillSummon (no action).
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: summonerAction(ctx, A.SKILL_SLEEP), source: 'AttackWizard AT_SKILL_ALICE_THORNS' });
    case S.ALICE_BERSERKER:
      return plan({ packet: 'targeted', targetSelf: true, friendly: true, heroAction: summonerAction(ctx, A.SKILL_SLEEP), source: 'UseSkillSummon AT_SKILL_ALICE_BERSERKER' });
    case S.ALICE_WEAKNESS:
    case S.ALICE_ENERVATION:
      return plan({ packet: 'area', areaExtraTarget: false, heroAction: summonerAction(ctx, A.SKILL_SLEEP), source: 'UseSkillSummon AT_SKILL_ALICE_WEAKNESS / AT_SKILL_ALICE_ENERVATION' });
    case S.LIGHTNING_SHOCK:
      return plan({ packet: 'area', heroAction: A.SKILL_LIGHTNING_SHOCK, rotationScale: 255, turnFirst: true, source: 'AttackWizard AT_SKILL_LIGHTNING_SHOCK' });
    default:
      return null;
  }
}

// ---------------------------------------------------------------------------
// Checks before the cast.
// ---------------------------------------------------------------------------

/**
 * CanExecuteSkill: no skill while dead or in a safe zone (the desktop refuses every
 * skill in a safe zone, even the buffs it lists for Blood/Chaos Castle).
 */
export function skillCastAllowed(state: { dead: boolean; safeZone: boolean }): boolean {
  return !state.dead && !state.safeZone;
}

/**
 * CheckMana: mana must be at least the skill's mana cost and AG at least its AG cost
 * (the desktop then tries a mana potion when mana is short). The elf's skills cost
 * InfinityArrowAdditionalMana (10) more mana while Infinity Arrow is active (AttackElf, SkillElf).
 */
export function hasSkillResources(
  have: { mana: number; ag: number },
  cost: { mana: number; ag: number },
  infinityArrowActive = false,
): boolean {
  const mana = cost.mana + (infinityArrowActive ? 10 : 0);
  return have.mana >= mana && have.ag >= cost.ag;
}

/** SendRequestMagic sends a TargetedSkill at most every 300 ms, except Nova (40) and skills 261, 263. */
export const TARGETED_SKILL_MIN_INTERVAL_MS = 300;
export function targetedSkillThrottled(skill: number): boolean {
  return skill !== S.NOVA && skill !== 261 && skill !== 263;
}

/** The AreaSkill rotation byte: (BYTE)(angle / 360 * scale), angle in degrees (o->Angle[2]). */
export function areaSkillRotation(angleDegrees: number, scale: 256 | 255 = 256): number {
  const a = ((angleDegrees % 360) + 360) % 360;
  return Math.trunc((a / 360) * scale) & 0xff;
}

/** Skills SkillWarrior casts through CastWarriorSkill, whose reach is the skill distance * 1.2. */
const CAST_WARRIOR_RANGE_SKILLS: readonly number[] = [...WEAPON_SKILLS, ...WARRIOR_TARGET_SKILLS];

/**
 * Range check as the desktop does it (GetSkillDistance: SkillAttribute Distance in
 * cells, + 2 on the Dark Horse; CastWarriorSkill: * 1.2, and 1.8 for Falling
 * Slash..Slash in Blood Castle). CheckTile compares it with the distance from the
 * hero to the centre of the target's tile, in cells.
 */
export function skillRange(
  skill: number,
  distanceFromData: number,
  options: { darkHorse?: boolean; bloodCastle?: boolean } = {},
): number {
  const distance = distanceFromData + (options.darkHorse ? 2 : 0);
  if (CAST_WARRIOR_RANGE_SKILLS.includes(skill)) {
    if (options.bloodCastle && skill >= S.FALLING_SLASH && skill <= S.SLASH) return 1.8;
    return distance * 1.2;
  }
  return distance;
}

// ---------------------------------------------------------------------------
// The receive side.
// ---------------------------------------------------------------------------

/** The packet the server answers a cast with (the cast packet; TargetedSkill for the rest). */
function shownVia(skill: number): 'targeted' | 'area' {
  // The cast plan does not depend on the class for the packet kind, except for skills
  // a class cannot cast; try the classes that can.
  for (const cls of [C.DARK_WIZARD, C.DARK_KNIGHT, C.ELF, C.MAGIC_GLADIATOR, C.DARK_LORD, C.SUMMONER]) {
    const plan = skillCastPlan(skill, cls, {});
    if (plan) return plan.packet === 'area' ? 'area' : 'targeted';
  }
  return 'targeted';
}

/**
 * The action a caster (other player or own hero, as told by SkillAnimation /
 * AreaSkillAnimation) plays for a skill; null when the desktop leaves the action
 * as it is.
 */
export function skillAnimationAction(
  skill: number,
  classIndex: number,
  context: { rightHandKind?: string; leftHandKind?: string; mounted?: boolean; female?: boolean } & SkillAnimationContext,
): number | null {
  const ctx = context;
  const own = ctx.own ?? false;
  const via = ctx.via ?? shownVia(skill);
  return via === 'area' ? areaAnimationAction(skill, classIndex, ctx, own) : targetedAnimationAction(skill, classIndex, ctx, own);
}

// ReceiveMagicContinue (AreaSkillAnimation, C3 1E): players other than the hero only.
function areaAnimationAction(skill: number, classIndex: number, ctx: SkillAnimationContext, own: boolean): number | null {
  if (own) return null;
  const g = gearOf(ctx);
  switch (skill) {
    case S.TRIPLE_SHOT:
    case S.BLAST_CROSSBOW4:
    case S.PENETRATION:
      return attackAction(classIndex, ctx);
    case S.DECAY:
    case S.ICE_STORM:
      return magicAction(classIndex, ctx);
    case S.FLASH:
      return A.SKILL_FLASH;
    case S.HELL_FIRE:
      return A.SKILL_HELL;
    case S.INFERNO:
      return A.SKILL_INFERNO;
    case S.TWISTING_SLASH:
    case S.FIRE_SLASH:
      return A.ATTACK_SKILL_WHEEL;
    case S.FIRE_SCREAM:
    case S.CHAOTIC_DISEIER:
      return strikeActionAnyZone(ctx);
    case S.THUNDER_STRIKE:
      return g.mount === 'fenrir' ? A.FENRIR_ATTACK_DARKLORD_FLASH : A.SKILL_FLASH;
    case S.EARTHSHAKE:
      return A.ATTACK_DARKHORSE;
    case S.RAGEFUL_BLOW:
      return A.ATTACK_SKILL_FURY_STRIKE;
    case S.STRIKE_OF_DESTRUCTION:
      return A.SKILL_BLOW_OF_DESTRUCTION;
    case S.IMPALE:
      return g.mount === 'fenrir' ? A.FENRIR_ATTACK_SPEAR : A.ATTACK_SKILL_SPEAR;
    case S.POWER_SLASH:
      return A.ATTACK_TWO_HAND_SWORD_TWO;
    case S.DEATHSTAB:
      return A.ATTACK_DEATHSTAB;
    case S.STUN:
    case S.INVISIBLE:
      return A.SKILL_VITALITY;
    case S.PLASMA_STORM_FENRIR:
      return fenrirSkillAction(classIndex, ctx);
    case S.MULTI_SHOT:
      return bowAction(classIndex, ctx);
    case S.RECOVER:
      return null; // Effects only.
    case S.ALICE_LIGHTNINGORB:
      return summonerAction(ctx, A.SKILL_LIGHTNING_ORB);
    case S.ALICE_DRAINLIFE:
      return summonerAction(ctx, A.SKILL_DRAIN_LIFE);
    case S.ALICE_WEAKNESS:
    case S.ALICE_ENERVATION:
      return summonerAction(ctx, A.SKILL_SLEEP);
    case 223: // AT_SKILL_SUMMON_EXPLOSION, _REQUIEM, _POLLUTION: the summon system's own animation.
    case 224:
    case 225:
      return null;
    case S.FLAME_STRIKE:
      return A.SKILL_FLAMESTRIKE;
    case S.GIGANTIC_STORM:
      return A.SKILL_GIGANTICSTORM;
    case S.LIGHTNING_SHOCK:
      return A.SKILL_LIGHTNING_SHOCK;
    case 264: // AT_SKILL_DRAGON_ROAR: the Rage Fighter's own animation (not ported).
      return null;
    default:
      // Every other area skill: SetPlayerMagic.
      return magicAction(classIndex, ctx);
  }
}

// ReceiveMagic (SkillAnimation, C3 19).
function targetedAnimationAction(skill: number, classIndex: number, ctx: SkillAnimationContext, own: boolean): number | null {
  const g = gearOf(ctx);
  const others = (action: number | null): number | null => (own ? null : action);
  switch (skill) {
    case S.RECOVER:
      return others(A.RECOVER_SKILL);
    case S.HEALING:
    case S.ATTACK:
    case S.DEFENSE:
    case S.SOUL_BARRIER:
    case S.LIGHTNING:
    case S.FIREBALL:
    case S.METEO:
    case S.ICE:
    case S.ENERGYBALL:
    case S.POWERWAVE:
    case S.POISON:
    case S.FLAME:
    case S.JAVELIN:
    case S.IMPROVE_AG:
      return others(magicAction(classIndex, ctx));
    case S.FORCE:
    case S.FORCE_WAVE:
    case S.FIREBURST:
    case S.SPACE_SPLIT:
      return strikeActionOutsideSafeZone(ctx);
    case S.FALLING_SLASH:
    case S.LUNGE:
    case S.UPPERCUT:
    case S.CYCLONE:
      return A.ATTACK_SKILL_SWORD1 + skill - S.FALLING_SLASH;
    case S.SLASH:
      // Alternates on the caster's SwordCount (which the desktop then counts up).
      return (ctx.swordCount ?? 0) % 2 === 0 ? A.ATTACK_SKILL_SWORD5 : A.ATTACK_TWO_HAND_SWORD3;
    case S.POWER_SLASH:
      return A.ATTACK_TWO_HAND_SWORD_TWO;
    case S.IMPALE:
      return g.mount === 'fenrir' ? A.FENRIR_ATTACK_SPEAR : A.ATTACK_SKILL_SPEAR;
    case S.DEATHSTAB:
      return A.ATTACK_DEATHSTAB;
    case S.TRIPLE_SHOT:
    case S.MULTI_SHOT:
    case S.BLAST_CROSSBOW4:
    case S.PENETRATION:
    case S.ICE_ARROW:
      return bowAction(classIndex, ctx);
    case S.RUSH:
      return A.ATTACK_RUSH;
    case S.DEATH_CANNON:
      return A.ATTACK_DEATH_CANNON;
    case S.DEEPIMPACT:
      return highBowAction(ctx);
    case S.BRAND_OF_SKILL:
    case S.ADD_CRITICAL:
      return lordMagicAction(ctx);
    case S.SPIRAL_SLASH:
      return A.ATTACK_ONE_FLASH;
    case S.STUN:
    case S.MANA:
    case S.INVISIBLE:
    case S.REMOVAL_BUFF:
      return commonSkillAction(ctx, A.SKILL_VITALITY);
    case S.REMOVAL_STUN:
    case S.REMOVAL_INVISIBLE:
      return commonSkillAction(ctx, A.ATTACK_REMOVAL);
    case S.PARTY_TELEPORT:
      if (isUniriaToDarkHorse(g.mount) && g.riding) return A.ATTACK_RIDE_TELEPORT;
      if (g.mount === 'fenrir' && g.riding) return A.FENRIR_ATTACK_DARKLORD_TELEPORT;
      return A.ATTACK_TELEPORT;
    case S.SWELL_LIFE:
      return A.SKILL_VITALITY;
    case S.RIDER:
      return ctx.flyingMap ? A.SKILL_RIDER_FLY : A.SKILL_RIDER;
    case S.NOVA_BEGIN:
      return A.SKILL_HELL_BEGIN;
    case S.NOVA:
      return A.SKILL_HELL_START;
    case S.FIRE_SCREAM:
    case S.CHAOTIC_DISEIER:
      return strikeActionAnyZone(ctx);
    case S.ALICE_SLEEP:
      return summonerAction(ctx, A.SKILL_SLEEP);
    case S.ALICE_BLIND:
    case S.ALICE_THORNS:
    case S.ALICE_BERSERKER:
      return A.SKILL_SLEEP;
    case S.EXPANSION_OF_WIZARDRY:
      return A.SKILL_SWELL_OF_MP;
    case 269: // AT_SKILL_OCCUPY
      return A.ATTACK_RUSH;
    default:
      // Teleport, Teleport Ally, the elf summons' and Infinity Arrow's effects, the
      // Rage Fighter's skills...: no action change here.
      return null;
  }
}

/**
 * Which way the caster turns for a shown skill: AreaSkillAnimation turns every
 * caster (the hero too) to Rotation / 255 * 360 degrees; SkillAnimation turns a
 * caster other than the hero towards a visible target, except for Teleport, Teleport
 * Ally and the combo.
 */
export function skillAnimationFacing(via: 'targeted' | 'area', skill: number, own: boolean): 'rotation' | 'target' | 'none' {
  if (via === 'area') return 'rotation';
  if (own || skill === S.TELEPORT || skill === S.TELEPORT_ALLY || skill === S.COMBO) return 'none';
  return 'target';
}

/** The caster's facing in degrees from the AreaSkillAnimation Rotation byte (the desktop divides by 255). */
export function areaAnimationAngle(rotation: number): number {
  return (rotation / 255) * 360;
}
