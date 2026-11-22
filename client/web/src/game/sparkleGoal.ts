// Pure sparkle trail goal selection and constants shared between AutoPlaySparkles, OnboardingWindow, and main.ts.

export type SparkleGoalKind = 'autoplay' | 'quest' | 'boss';

export interface SparkleCell {
  x: number;
  y: number;
}

export interface SparkleGoal {
  kind: SparkleGoalKind;
  cell: SparkleCell;
}

export interface OnboardingLikeState {
  status: string;
  current?: {
    where?: { map: number; x: number; y: number };
  };
}

export const SPARKLE_POINT_SIZE = 56;

export const SPARKLE_COLORS: Record<SparkleGoalKind, number> = {
  autoplay: 0xf4d06f,
  quest: 0x38bdf8,
  boss: 0xf43f5e,
};

/** Returns active onboarding guide coordinates when on the given map, or null. */
export function onboardingTargetCell(
  state: OnboardingLikeState | null | undefined,
  mapNumber: number | null | undefined,
): SparkleCell | null {
  const where = state?.status === 'active' ? state.current?.where : undefined;
  if (!where || where.x < 0 || where.y < 0) return null;
  if (mapNumber !== undefined && mapNumber !== null && where.map !== mapNumber) return null;
  return { x: where.x, y: where.y };
}

/** Chooses the active sparkle trail goal: autoplay target > world boss > active quest objective. */
export function resolveSparkleGoal(options: {
  autoPlayTarget?: SparkleCell | null;
  bossCell?: SparkleCell | null;
  questCell?: SparkleCell | null;
}): SparkleGoal | null {
  if (options.autoPlayTarget) return { kind: 'autoplay', cell: options.autoPlayTarget };
  if (options.bossCell) return { kind: 'boss', cell: options.bossCell };
  if (options.questCell) return { kind: 'quest', cell: options.questCell };
  return null;
}
