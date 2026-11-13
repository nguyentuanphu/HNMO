// The auto play (helper) settings, as the desktop keeps them (MUHelper::ConfigData,
// MuHelperData.h). The server stores them per character (MuHelperConfigurationData /
// MuHelperSaveDataRequest, 257 bytes; see decodeAutoPlayConfig / encodeAutoPlayConfig).

export const SkillActivation = {
  ALWAYS: 0x00, ON_TIMER: 0x01, ON_CONDITION: 0x02,
  ON_MOBS_NEARBY: 0x04, ON_MOBS_ATTACKING: 0x08,
  ON_MORE_THAN_TWO_MOBS: 0x10, ON_MORE_THAN_THREE_MOBS: 0x20, ON_MORE_THAN_FOUR_MOBS: 0x40, ON_MORE_THAN_FIVE_MOBS: 0x80,
} as const;

export const PetAttackMode = { CEASE: 0, AUTO: 1, TOGETHER: 2 } as const;

export interface AutoPlayConfig {
  huntingRange: number;
  longRangeCounterAttack: boolean;
  returnToOriginalPosition: boolean;
  maxSecondsAway: number;
  /** The basic skill and the two activation skills (skill numbers, 0 for none). */
  skills: [number, number, number];
  /** SkillActivation flags of each skill. */
  skillConditions: [number, number, number];
  /** Seconds between casts for ON_TIMER. */
  skillIntervals: [number, number, number];
  useCombo: boolean;
  buffs: [number, number, number];
  buffDuration: boolean;
  buffDurationParty: boolean;
  buffCastInterval: number;
  autoHeal: boolean;
  healThreshold: number;
  supportParty: boolean;
  autoHealParty: boolean;
  healPartyThreshold: number;
  useHealPotion: boolean;
  potionThreshold: number;
  useDrainLife: boolean;
  useDarkRaven: boolean;
  darkRavenMode: number;
  repairItem: boolean;
  obtainingRange: number;
  pickAllItems: boolean;
  pickSelectItems: boolean;
  pickJewel: boolean;
  pickZen: boolean;
  pickAncient: boolean;
  pickExcellent: boolean;
  pickExtraItems: boolean;
  extraItems: string[];
  useSelfDefense: boolean;
  autoAcceptFriend: boolean;
  autoAcceptGuild: boolean;
  fallbackBasicAttack: boolean;
}

export function defaultAutoPlayConfig(): AutoPlayConfig {
  return {
    huntingRange: 0, longRangeCounterAttack: false, returnToOriginalPosition: false, maxSecondsAway: 0,
    skills: [0, 0, 0], skillConditions: [0, 0, 0], skillIntervals: [0, 0, 0], useCombo: false,
    buffs: [0, 0, 0], buffDuration: false, buffDurationParty: false, buffCastInterval: 0,
    autoHeal: false, healThreshold: 0, supportParty: false, autoHealParty: false, healPartyThreshold: 0,
    useHealPotion: false, potionThreshold: 0, useDrainLife: false, useDarkRaven: false, darkRavenMode: 0,
    repairItem: false, obtainingRange: 0, pickAllItems: false, pickSelectItems: false, pickJewel: false,
    pickZen: false, pickAncient: false, pickExcellent: false, pickExtraItems: false, extraItems: [],
    useSelfDefense: false, autoAcceptFriend: false, autoAcceptGuild: false, fallbackBasicAttack: true,
  };
}
