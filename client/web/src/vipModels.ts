/** The VIP website and game window share a server-priced offer and status contract. */
export type VipCoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';

export interface VipBenefits {
  experiencePercent: number;
  zenPercent: number;
  dailyCoins: number;
  dailyCoinType: VipCoinType;
}

export interface VipOfferView extends VipBenefits {
  levelId: number;
  nameEn: string;
  nameVi: string;
  days: number;
  price: number;
  coinType: VipCoinType;
  /** Optimistic concurrency token: the server must reject changed prices or benefits before charging. */
  offerToken: string;
}

export interface VipOperation {
  operationId: string;
  levelId: number;
  days: number;
  offerToken: string;
}

export interface VipStateView {
  enabled: boolean;
  active: (VipBenefits & { levelId: number; nameEn: string; nameVi: string; untilUtc: string }) | null;
  offers: readonly VipOfferView[];
  pending: VipOperation | null;
  daily: { available: boolean; claimed: boolean };
  status?: string | null;
  rule?: string | null;
}
