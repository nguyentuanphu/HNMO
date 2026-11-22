export interface GuideClass { id: number; nameEn: string; nameVi: string; requiredLevel: number; shieldConfigured: boolean; maximumMasterTier: number | null }
export interface GuideOffer { days: number; price: number; coinType: string }
export interface GuideVip { id: number; nameEn: string; nameVi: string; experiencePercent: number; zenPercent: number; dailyCoins: number; dailyCoinType: string; offers: GuideOffer[] }
export interface GuideProduct { nameEn: string; nameVi: string; itemLevel: number; quantity: number; durationSeconds: number; price: number; coinType: string; bundle: boolean }
export interface ServerGuideFacts {
  maximumInventoryMoney: number; maximumVaultMoney: number;
  classes: GuideClass[];
  helper: { minLevel: number; vipMinLevel: number; maxLevel: number; payIntervalSeconds: number; costPerStage: number[] } | null;
  inventory: { maximumExtensions: number } | null;
  vault: { defaultVaultCount: number; maxVaultsPerAccount: number; vipExtraVaults: number; purchaseExtraVaultCost: number; purchaseCoinType: string } | null;
  partyExperience: { size: number; percent: number; differentClassesPercent: number }[];
  vip: GuideVip[];
  catalog: { products: GuideProduct[] | null; truncated: boolean };
}
export interface ServerGuideView {
  updatedAt: string; clientRules: { en: string; vi: string }; notForSale: { en: string; vi: string };
  servers: { id: number; facts: ServerGuideFacts; extras?: ServerGuideExtras }[];
}

export interface GuideQuest { nameEn: string; nameVi: string; npcEn: string; npcVi: string; minimumLevel: number; maximumLevel: number; requiredStartMoney: number; classEn: string; classVi: string }
export interface GuideCraft { nameEn: string; nameVi: string; npcEn: string; npcVi: string; itemEn: string; itemVi: string; randomMinimumLevel: number; randomMaximumLevel: number; addLevel: number; upgrade: boolean; basePercent: number | null; maximumPercent: number; fixedZen: number; zenPerFinalPercentage: number; excellentChance: number | null; maximumExcellentOptions: number }
export interface ServerGuideExtras {
  systems: { antiCheatEnabled: boolean; activityLogEnabled: boolean; onlyValuableDrops: boolean };
  quests: GuideQuest[]; questsTruncated: boolean; crafts: GuideCraft[]; craftsTruncated: boolean;
}

const MAX_PUBLIC_ROWS = 32;
const MAX_PARTY_MEMBERS = 255;
const MAX_CATALOG_ROWS = 100;
const MAX_POLICY_CHARACTERS = 4000;
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('server-guide-shape');
  return value as Record<string, unknown>;
}
function number(value: unknown): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error('server-guide-number');
  return value;
}
function text(value: unknown): string { if (typeof value !== 'string') throw new Error('server-guide-text'); return value; }
function flag(value: unknown): boolean { if (typeof value !== 'boolean') throw new Error('server-guide-flag'); return value; }
function array<T>(value: unknown, parse: (item: unknown) => T, maximum = MAX_CATALOG_ROWS): T[] {
  if (!Array.isArray(value) || value.length > maximum) throw new Error('server-guide-array');
  return value.map(parse);
}
function optional<T>(value: unknown, parse: (value: Record<string, unknown>) => T): T | null { return value === null ? null : parse(object(value)); }
function names(value: Record<string, unknown>): { nameEn: string; nameVi: string } { return { nameEn: text(value.nameEn), nameVi: text(value.nameVi) }; }
function bilingual(value: unknown): { en: string; vi: string } { const row = object(value); return { en: text(row.en).slice(0, MAX_POLICY_CHARACTERS), vi: text(row.vi).slice(0, MAX_POLICY_CHARACTERS) }; }

function classes(value: unknown): GuideClass {
  const row = object(value);
  return { id: number(row.id), ...names(row), requiredLevel: number(row.requiredLevel), shieldConfigured: flag(row.shieldConfigured), maximumMasterTier: row.maximumMasterTier === null ? null : number(row.maximumMasterTier) };
}
function vip(value: unknown): GuideVip {
  const row = object(value);
  return { id: number(row.id), ...names(row), experiencePercent: number(row.experiencePercent), zenPercent: number(row.zenPercent), dailyCoins: number(row.dailyCoins), dailyCoinType: text(row.dailyCoinType),
    offers: array(row.offers, value => { const offer = object(value); return { days: number(offer.days), price: number(offer.price), coinType: text(offer.coinType) }; }, MAX_PUBLIC_ROWS) };
}
function product(value: unknown): GuideProduct {
  const row = object(value);
  return { ...names(row), itemLevel: number(row.itemLevel), quantity: number(row.quantity), durationSeconds: number(row.durationSeconds), price: number(row.price), coinType: text(row.coinType), bundle: flag(row.bundle) };
}
function facts(value: unknown): ServerGuideFacts {
  const row = object(value); const catalog = object(row.catalog);
  return {
    maximumInventoryMoney: number(row.maximumInventoryMoney), maximumVaultMoney: number(row.maximumVaultMoney), classes: array(row.classes, classes, MAX_PUBLIC_ROWS),
    helper: optional(row.helper, item => ({ minLevel: number(item.minLevel), vipMinLevel: number(item.vipMinLevel), maxLevel: number(item.maxLevel), payIntervalSeconds: number(item.payIntervalSeconds), costPerStage: array(item.costPerStage, number) })),
    inventory: optional(row.inventory, item => ({ maximumExtensions: number(item.maximumExtensions) })),
    vault: optional(row.vault, item => ({ defaultVaultCount: number(item.defaultVaultCount), maxVaultsPerAccount: number(item.maxVaultsPerAccount), vipExtraVaults: number(item.vipExtraVaults), purchaseExtraVaultCost: number(item.purchaseExtraVaultCost), purchaseCoinType: text(item.purchaseCoinType) })),
    partyExperience: array(row.partyExperience, value => { const item = object(value); return { size: number(item.size), percent: number(item.percent), differentClassesPercent: number(item.differentClassesPercent) }; }, MAX_PARTY_MEMBERS),
    vip: array(row.vip, vip, MAX_PUBLIC_ROWS), catalog: { products: catalog.products === null ? null : array(catalog.products, product), truncated: flag(catalog.truncated) },
  };
}

function nullableText(value: unknown): string { return value === null ? '' : text(value); }
function quest(value: unknown): GuideQuest {
  const row = object(value);
  return { ...names(row), npcEn: text(row.npcEn), npcVi: text(row.npcVi), minimumLevel: number(row.minimumLevel), maximumLevel: number(row.maximumLevel), requiredStartMoney: number(row.requiredStartMoney), classEn: nullableText(row.classEn), classVi: nullableText(row.classVi) };
}
function craft(value: unknown): GuideCraft {
  const row = object(value);
  return { ...names(row), npcEn: text(row.npcEn), npcVi: text(row.npcVi), itemEn: nullableText(row.itemEn), itemVi: nullableText(row.itemVi), randomMinimumLevel: number(row.randomMinimumLevel), randomMaximumLevel: number(row.randomMaximumLevel), addLevel: number(row.addLevel), upgrade: flag(row.upgrade), basePercent: row.basePercent === null ? null : number(row.basePercent), maximumPercent: number(row.maximumPercent), fixedZen: number(row.fixedZen), zenPerFinalPercentage: number(row.zenPerFinalPercentage), excellentChance: row.excellentChance === null ? null : number(row.excellentChance), maximumExcellentOptions: number(row.maximumExcellentOptions) };
}
function extras(value: unknown): ServerGuideExtras {
  const row = object(value); const systems = object(row.systems);
  return { systems: { antiCheatEnabled: flag(systems.antiCheatEnabled), activityLogEnabled: flag(systems.activityLogEnabled), onlyValuableDrops: flag(systems.onlyValuableDrops) }, quests: array(row.quests, quest, MAX_PUBLIC_ROWS), questsTruncated: flag(row.questsTruncated), crafts: array(row.crafts, craft, MAX_PUBLIC_ROWS), craftsTruncated: flag(row.craftsTruncated) };
}

/** Reject incompatible replies rather than rendering invented fallback rates or old offers. */
export function parseServerGuide(value: unknown): ServerGuideView {
  const row = object(value); const updatedAt = text(row.updatedAt);
  if (Number.isNaN(Date.parse(updatedAt))) throw new Error('server-guide-time');
  return { updatedAt, clientRules: bilingual(row.clientRules), notForSale: bilingual(row.notForSale), servers: array(row.servers, value => { const server = object(value); return { id: number(server.id), facts: facts(server.facts), ...(server.extras === undefined ? {} : { extras: extras(server.extras) }) }; }, MAX_PUBLIC_ROWS) };
}
