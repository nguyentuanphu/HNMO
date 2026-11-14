// Gens, as the desktop keeps them (WSclient.cpp's ReceiveGens* handlers, C1 F8 xx): the hero's
// family (m_byGensInfluence), title rank (GensRanking), contribution, ranking position and the
// contribution the next rank needs (GensInfo F8 07, SetGensStanding); the family, rank and contribution
// of the players in view (AssignCharactersToGens F8 05); the answers to joining, leaving and the
// reward at the stewards (F8 02, F8 04, F8 0A), which the NPC dialogue turns into its next state.

import { packetCode, read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';

export const GENS_NONE = 0, GENS_DUPRIAN = 1, GENS_VANERT = 2;
/** The stewards: 543 the Duprian one (Lorencia), 544 the Vanert one (Noria). */
export const DUPRIAN_STEWARD = 543, VANERT_STEWARD = 544;

// Game.en.resx
export const GensText = {
  InfoWindow: 'Gens Info Window',
  Gens: 'Gens',
  Duprian: 'Duprian',
  Vanert: 'Vanert',
  Level: 'Level:',
  Ranking: 'Gens Ranking',
  GainContribution: 'Gain Contribution',
  Description: 'Gens Description',
  NextRank: 'The amount of contribution needed for promotion to the next rank is %d.',
  RewardsGivenOut: 'Gens ranking rewards are given out with the patch in the first week of each month.',
  RewardsClaimed: 'Gens ranking rewards can be claimed from the gens steward NPC.## Gens rewards will automatically disappear if not claimed within a week.',
  NotJoined: 'You have not joined a gens.',
  Titles: 'Grand Duke#Duke#Marquis#Count#Viscount#Baron#Knight Commander#Superior Knight#Knight#Guard Prefect#Officer#Lieutenant#Sergeant#Private',
  Close: 'Close',
} as const;

const TITLES = GensText.Titles.split('#');

/** GetTitleName: the title of a rank (1 Grand Duke … 14 Private); out of range is the last one. */
export function gensTitle(rank: number): string {
  return TITLES[rank >= 1 && rank <= TITLES.length ? rank - 1 : TITLES.length - 1];
}

/** GetImageIndex: the rank's cell of the family's mark sheet (rank 14 is cell 0), -1 for none. */
export function gensMarkCell(rank: number): number {
  const index = rank < 1 || rank > 14 ? 14 : rank;
  return 14 - index;
}

export interface GensMember { gens: number; rank: number; contribution: number }

interface InfoPacket { GensType: number; RankingPosition: number; Rank: number; ContributionPoints: number; NextRankContributionPoints: number }
interface AssignPacket { Members: { GensType: number; PlayerId: number; RankingPosition: number; Rank: number; ContributionPoints: number }[] }

/** Who listens to the steward's answers: the NPC dialogue's next state (CNPCDialogue::ProcessGens*Receive). */
export interface GensAnswers {
  joined: (result: number) => void;
  left: (result: number) => void;
  reward: (result: number) => void;
}

export class Gens {
  /** The hero's family (m_byGensInfluence). */
  influence = GENS_NONE;
  /** The hero's title rank (GensRanking), 1..14. */
  rank = 0;
  contribution = 0;
  /** The ranking position, "-" when there's none (SetRanking). */
  ranking = '-';
  /** The contribution the next rank needs, -1 for none (SetNextContribution). */
  nextContribution = 0;
  /** The players in view with a family (c->m_byGensInfluence, GensRanking, GensContributionPoints). */
  readonly members = new Map<number, GensMember>();
  answers: GensAnswers | null = null;
  private readonly listeners = new Set<() => void>();

  constructor(connection: Connection) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(): void { for (const listener of this.listeners) listener(); }

  /** A player leaving the view takes its family with it. */
  forget(id: number): void { if (this.members.delete(id)) this.changed(); }

  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1 && bytes[0] !== 0xc2) return;
    const { code, subCode } = packetCode(bytes);
    if (code !== 0xf8) return;
    switch (subCode) {
      case 0x02: {
        // ReceiveGensJoining: the family comes with a successful answer.
        const result = bytes[4];
        if (result === 0) this.influence = bytes[5];
        if (result <= 7) this.answers?.joined(result);
        break;
      }
      case 0x04: {
        // ReceiveGensSecession
        const result = bytes[4];
        if (result === 0) this.influence = GENS_NONE;
        if (result <= 3) this.answers?.left(result);
        break;
      }
      case 0x07: {
        // ReceivePlayerGensInfluence
        const info = read<InfoPacket>('ServerToClient', 'GensInfo', bytes);
        this.influence = info.GensType;
        this.rank = info.Rank;
        this.contribution = Math.max(0, info.ContributionPoints | 0);
        this.ranking = (info.RankingPosition | 0) > 0 ? String(info.RankingPosition) : '-';
        this.nextContribution = (info.NextRankContributionPoints | 0) < 0 ? -1 : info.NextRankContributionPoints;
        break;
      }
      case 0x05: {
        // ReceiveOtherPlayerGensInfluenceViewport
        const packet = read<AssignPacket>('ServerToClient', 'AssignCharactersToGens', bytes);
        for (const member of packet.Members) {
          this.members.set(member.PlayerId & 0x7fff, { gens: member.GensType, rank: member.Rank, contribution: member.ContributionPoints });
        }
        break;
      }
      case 0x0a: {
        // ReceiveReward
        const result = bytes[4];
        if (result <= 6) this.answers?.reward(result);
        break;
      }
      default: return;
    }
    this.changed();
  }

  get familyName(): string {
    if (this.influence === GENS_DUPRIAN) return GensText.Duprian;
    if (this.influence === GENS_VANERT) return GensText.Vanert;
    return '';
  }
}

// CNPCDialogue::ProcessGens*Receive: the dialogue state each answer leads to.
export const GENS_JOIN_STATE = [5, 9, 11, 8, 10, 12, 18, 19];
export const GENS_LEAVE_STATE = [16, 15, 14, 17];
export const GENS_REWARD_STATE = [20, 21, 22, 23, 24, 17, 25];
