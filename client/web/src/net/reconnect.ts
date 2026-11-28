// Reconnect after a lost connection (HNMO, PARITY.md section 1): when the connection to the game server drops while in
// the world (not a logout, a server's logout answer or a page being left), the game's reconnect box shows over the
// world, and the game tries again until the player presses Cancel, as the game does, with growing waits (capped at
// 16 seconds): it looks whether the server is back (the connect server knows it and the game server greets), takes a
// one-time login ticket of the website session, and reloads the page into the same server and the same character with
// it (the way the Arena travels). A try that fails after the reload goes on with the next one. Cancel goes back to the
// server choice. The plan (server, character, which try) is kept in this tab's session storage; no password, no ticket.

export interface ResumePlan {
  account: string;
  relay: string;
  /** The connect server that lists the game server (its season's). */
  connect: { host: string; port: number };
  /** The start screen's column of the server and its id there. */
  column: string;
  serverId: number;
  character: string;
  /** The try the plan is at (0-based; its wait is retryDelay(attempt)). */
  attempt: number;
  /** When the connection was lost (ms since 1970). */
  lostAt: number;
}

/** The waits before the first tries, in seconds: at once, then growing; every later try waits the last one. */
export const RETRY_DELAYS_S = [0, 2, 4, 8, 16];

/** The wait before a try, in seconds (capped at 16). */
export function retryDelay(attempt: number): number {
  return RETRY_DELAYS_S[Math.min(Math.max(attempt, 0), RETRY_DELAYS_S.length - 1)];
}

const STORAGE_KEY = 'hnmo.reconnect';
/** A plan not saved again for this long is forgotten (a tab closed on the box, then opened much later). */
const PLAN_LIFETIME_MS = 10 * 60_000;

export function saveResumePlan(plan: ResumePlan, now = Date.now()): void {
  try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ ...plan, savedAt: now })); } catch { /* private window: no resume after the reload */ }
}

export function clearResumePlan(): void {
  try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* nothing kept */ }
}

/** The kept plan (of this account when given), or null. */
export function loadResumePlan(account?: string, now = Date.now()): ResumePlan | null {
  try {
    const raw = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null') as (Partial<ResumePlan> & { savedAt?: number }) | null;
    if (!raw || typeof raw.account !== 'string' || typeof raw.relay !== 'string' || typeof raw.column !== 'string'
      || typeof raw.serverId !== 'number' || typeof raw.character !== 'string' || typeof raw.attempt !== 'number'
      || typeof raw.lostAt !== 'number' || typeof raw.connect?.host !== 'string' || typeof raw.connect.port !== 'number') return null;
    if (now - (raw.savedAt ?? raw.lostAt) > PLAN_LIFETIME_MS) { clearResumePlan(); return null; }
    if (account !== undefined && raw.account.toLowerCase() !== account.toLowerCase()) return null;
    const { account: a, relay, connect, column, serverId, character, attempt, lostAt } = raw as ResumePlan;
    return { account: a, relay, connect, column, serverId, character, attempt, lostAt };
  } catch {
    return null;
  }
}

export interface ReconnectDriver {
  /** The step the box shows and the seconds to the next try. */
  show(step: 'probing' | 'connecting', countdown: number): void;
  /** Resolves when the game server is back (throws while it isn't). */
  probe(plan: ResumePlan): Promise<void>;
  /** A login ticket for the plan's account, or 'signed-out' (no website session now: the next try asks again). */
  ticket(plan: ResumePlan): Promise<{ ticket: string; loginName: string } | 'signed-out'>;
  /** Goes back in with the ticket (the page reloads). */
  resume(plan: ResumePlan, ticket: { ticket: string; loginName: string }): void;
  /** Cancel was pressed: the tries stop. */
  cancelled(): void;
  /** Waits (tests and the page: a timer). */
  sleep?: (ms: number) => Promise<void>;
}

const wait = (ms: number) => new Promise<void>((resolve) => { setTimeout(resolve, ms); });

/** Tries for a plan from its `attempt` until one goes back in or cancel() (the box's Cancel) stops them. */
export class Reconnector {
  private stopped = false;
  private running = false;

  constructor(private readonly driver: ReconnectDriver) {}

  get active(): boolean { return this.running; }

  cancel(): void {
    if (!this.running) return;
    this.stopped = true;
  }

  async run(plan: ResumePlan): Promise<void> {
    if (this.running) return;
    this.running = true;
    this.stopped = false;
    const sleep = this.driver.sleep ?? wait;
    try {
      for (let attempt = plan.attempt; ; attempt++) {
        // The wait before this try, second by second for the box's countdown (and a prompt Cancel).
        for (let left = retryDelay(attempt); left > 0; left--) {
          if (this.stopped) { this.driver.cancelled(); return; }
          this.driver.show('probing', left);
          await sleep(1000);
        }
        if (this.stopped) { this.driver.cancelled(); return; }
        this.driver.show('probing', 0);
        try { await this.driver.probe(plan); } catch { continue; }
        if (this.stopped) { this.driver.cancelled(); return; }
        this.driver.show('connecting', 0);
        let entry: { ticket: string; loginName: string } | 'signed-out';
        try { entry = await this.driver.ticket(plan); } catch { continue; }
        if (entry === 'signed-out') continue;
        if (this.stopped) { this.driver.cancelled(); return; }
        const next = { ...plan, attempt };
        saveResumePlan(next);
        this.driver.resume(next, entry);
        return;
      }
    } finally {
      this.running = false;
    }
  }
}
