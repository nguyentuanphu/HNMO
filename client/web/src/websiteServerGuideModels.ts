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
  servers: { id: number; facts: ServerGuideFacts }[];
}

const MAX_SERVERS = 32;
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
function array<T>(value: unknown, parse: (item: unknown) => T, maximum = 100): T[] {
  if (!Array.isArray(value) || value.length > maximum) throw new Error('server-guide-array');
  return value.map(parse);
}
function optional<T>(value: unknown, parse: (value: Record<string, unknown>) => T): T | null { return value === null ? null : parse(object(value)); }
function names(value: Record<string, unknown>): { nameEn: string; nameVi: string } { return { nameEn: text(value.nameEn), nameVi: text(value.nameVi) }; }
function bilingual(value: unknown): { en: string; vi: string } { const row = object(value); return { en: text(row.en).slice(0, 4000), vi: text(row.vi).slice(0, 4000) }; }

function classes(value: unknown): GuideClass {
  const row = object(value);
  return { id: number(row.id), ...names(row), requiredLevel: number(row.requiredLevel), shieldConfigured: flag(row.shieldConfigured), maximumMasterTier: row.maximumMasterTier === null ? null : number(row.maximumMasterTier) };
}
function vip(value: unknown): GuideVip {
  const row = object(value);
  return { id: number(row.id), ...names(row), experiencePercent: number(row.experiencePercent), zenPercent: number(row.zenPercent), dailyCoins: number(row.dailyCoins), dailyCoinType: text(row.dailyCoinType),
    offers: array(row.offers, value => { const offer = object(value); return { days: number(offer.days), price: number(offer.price), coinType: text(offer.coinType) }; }, MAX_SERVERS) };
}
function product(value: unknown): GuideProduct {
  const row = object(value);
  return { ...names(row), itemLevel: number(row.itemLevel), quantity: number(row.quantity), durationSeconds: number(row.durationSeconds), price: number(row.price), coinType: text(row.coinType), bundle: flag(row.bundle) };
}
function facts(value: unknown): ServerGuideFacts {
  const row = object(value); const catalog = object(row.catalog);
  return {
    maximumInventoryMoney: number(row.maximumInventoryMoney), maximumVaultMoney: number(row.maximumVaultMoney), classes: array(row.classes, classes, MAX_SERVERS),
    helper: optional(row.helper, item => ({ minLevel: number(item.minLevel), vipMinLevel: number(item.vipMinLevel), maxLevel: number(item.maxLevel), payIntervalSeconds: number(item.payIntervalSeconds), costPerStage: array(item.costPerStage, number) })),
    inventory: optional(row.inventory, item => ({ maximumExtensions: number(item.maximumExtensions) })),
    vault: optional(row.vault, item => ({ defaultVaultCount: number(item.defaultVaultCount), maxVaultsPerAccount: number(item.maxVaultsPerAccount), vipExtraVaults: number(item.vipExtraVaults), purchaseExtraVaultCost: number(item.purchaseExtraVaultCost), purchaseCoinType: text(item.purchaseCoinType) })),
    partyExperience: array(row.partyExperience, value => { const item = object(value); return { size: number(item.size), percent: number(item.percent), differentClassesPercent: number(item.differentClassesPercent) }; }),
    vip: array(row.vip, vip, MAX_SERVERS), catalog: { products: catalog.products === null ? null : array(catalog.products, product), truncated: flag(catalog.truncated) },
  };
}

/** Reject incompatible replies rather than rendering invented fallback rates or old offers. */
export function parseServerGuide(value: unknown): ServerGuideView {
  const row = object(value); const updatedAt = text(row.updatedAt);
  if (Number.isNaN(Date.parse(updatedAt))) throw new Error('server-guide-time');
  return { updatedAt, clientRules: bilingual(row.clientRules), notForSale: bilingual(row.notForSale), servers: array(row.servers, value => { const server = object(value); return { id: number(server.id), facts: facts(server.facts) }; }, MAX_SERVERS) };
}
