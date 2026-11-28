// Reconnect after a lost connection (HNMO, PARITY.md section 1): when the connection to the game server drops while in
// the world (not a logout, a server's logout answer or a page being left), the game's reconnect box shows over the
// world, and the game tries again a few times with growing waits: it looks whether the server is back (the connect
// server knows it and the game server greets), takes a one-time login ticket of the website session, and reloads the
// page into the same server and the same character with it (the way the Arena travels). A try that fails after the
// reload goes on with the next one. When every try failed, the box gives the choice: try again, or back to the server
// choice. The plan (server, character, which try) is kept in this tab's session storage; no password and no ticket.

export interface ResumePlan {
  account: string;
  relay: string;
  /** The connect server that lists the game server (its season's). */
  connect: { host: string; port: number };
  /** The start screen's column of the server and its id there. */
  column: string;
  serverId: number;
  character: string;
  /** The try the plan is at (0-based, RETRY_DELAYS_S). */
  attempt: number;
  /** When the connection was lost (ms since 1970). */
  lostAt: number;
}

/** The waits before each try, in seconds: at once, then growing (five tries in about half a minute). */
export const RETRY_DELAYS_S = [0, 2, 4, 8, 16];

const STORAGE_KEY = 'hnmo.reconnect';
/** A plan older than this is forgotten (a tab left on the box, then reloaded much later). */
const PLAN_LIFETIME_MS = 10 * 60_000;

export function saveResumePlan(plan: ResumePlan): void {
  try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(plan)); } catch { /* private window: no resume after the reload */ }
}

export function clearResumePlan(): void {
  try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* nothing kept */ }
}

/** The kept plan (of this account when given), or null. */
export function loadResumePlan(account?: string, now = Date.now()): ResumePlan | null {
  try {
    const raw = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null') as Partial<ResumePlan> | null;
    if (!raw || typeof raw.account !== 'string' || typeof raw.relay !== 'string' || typeof raw.column !== 'string'
      || typeof raw.serverId !== 'number' || typeof raw.character !== 'string' || typeof raw.attempt !== 'number'
      || typeof raw.lostAt !== 'number' || typeof raw.connect?.host !== 'string' || typeof raw.connect.port !== 'number') return null;
    if (now - raw.lostAt > PLAN_LIFETIME_MS) { clearResumePlan(); return null; }
    if (account !== undefined && raw.account.toLowerCase() !== account.toLowerCase()) return null;
    return raw as ResumePlan;
  } catch {
    return null;
  }
}

/** Why the tries stopped without coming back. */
export type GiveUpReason = 'failed' | 'signed-out' | 'cancelled';

export interface ReconnectDriver {
  /** The step the box shows and the seconds to the next try. */
  show(step: 'probing' | 'connecting', countdown: number): void;
  /** Resolves when the game server is back (throws while it isn't). */
  probe(plan: ResumePlan): Promise<void>;
  /** A login ticket for the plan's account, or 'signed-out' (no website session: no try can work). */
  ticket(plan: ResumePlan): Promise<{ ticket: string; loginName: string } | 'signed-out'>;
  /** Goes back in with the ticket (the page reloads). */
  resume(plan: ResumePlan, ticket: { ticket: string; loginName: string }): void;
  /** Every try failed, the session ended or Cancel was pressed. */
  giveUp(reason: GiveUpReason): void;
  /** Waits (tests and the page: a timer). */
  sleep?: (ms: number) => Promise<void>;
}

const wait = (ms: number) => new Promise<void>((resolve) => { setTimeout(resolve, ms); });

/** One run of tries for a plan, from its `attempt`; cancel() stops it (the box's Cancel). */
export class Reconnector {
  private cancelled = false;
  private running = false;

  constructor(private readonly driver: ReconnectDriver) {}

  get active(): boolean { return this.running; }

  cancel(): void {
    if (!this.running) return;
    this.cancelled = true;
  }

  async run(plan: ResumePlan): Promise<void> {
    if (this.running) return;
    this.running = true;
    this.cancelled = false;
    const sleep = this.driver.sleep ?? wait;
    try {
      for (let attempt = plan.attempt; attempt < RETRY_DELAYS_S.length; attempt++) {
        // The wait before this try, second by second for the box's countdown.
        for (let left = RETRY_DELAYS_S[attempt]; left > 0; left--) {
          if (this.cancelled) { this.driver.giveUp('cancelled'); return; }
          this.driver.show('probing', left);
          await sleep(1000);
        }
        if (this.cancelled) { this.driver.giveUp('cancelled'); return; }
        this.driver.show('probing', 0);
        try { await this.driver.probe(plan); } catch { continue; }
        if (this.cancelled) { this.driver.giveUp('cancelled'); return; }
        this.driver.show('connecting', 0);
        let entry: { ticket: string; loginName: string } | 'signed-out';
        try { entry = await this.driver.ticket(plan); } catch { continue; }
        if (entry === 'signed-out') { this.driver.giveUp('signed-out'); return; }
        if (this.cancelled) { this.driver.giveUp('cancelled'); return; }
        const next = { ...plan, attempt };
        saveResumePlan(next);
        this.driver.resume(next, entry);
        return;
      }
      this.driver.giveUp('failed');
    } finally {
      this.running = false;
    }
  }
}
