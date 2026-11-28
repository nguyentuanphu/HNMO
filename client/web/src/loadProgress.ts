// How far a load is: the files the game asked for and the ones that came (bmd.ts fetchBytes counts each data file once,
// when it starts and when it is done or failed), and a meter that turns them into one fraction for a loading bar.
// A load has stages (world data, objects, the hero): each stage has its share of the bar, and inside a stage the bar
// follows the stage's files, or what the stage says of itself (createWorld's object types), whichever is further.

/** The loads counted so far; each load's number is the count when it started (1, 2, 3, …). */
let started = 0;
let finished = 0;
type LoadEvent = (kind: 'start' | 'done', id: number) => void;
const listeners = new Set<LoadEvent>();

/** The files asked for and the files done since the page opened. */
export function loadCounts(): { started: number; finished: number } {
  return { started, finished };
}

/** Counts a load (a file, or a step like a catalog) from now until it settles; gives it back as it is. */
export function trackLoad<T>(load: Promise<T>): Promise<T> {
  const id = ++started;
  for (const listener of listeners) listener('start', id);
  const done = () => { finished++; for (const listener of listeners) listener('done', id); };
  load.then(done, done);
  return load;
}

/** Hears every load that starts or settles from now on; returns the way to stop. */
export function onLoads(listener: LoadEvent): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

/** What a meter shows: its fraction (0 to 1, never going back) and the files of its load. */
export interface LoadReading { fraction: number; done: number; total: number }

/** A stage's bar never quite fills before the stage ends (its files may still bring more files). */
const STAGE_CAP = 0.95;

export class LoadMeter {
  /** The loads started before the meter (or its stage) are not its own. */
  private readonly firstId = started + 1;
  private stageFirstId = started + 1;
  private asked = 0;
  private came = 0;
  private stageAsked = 0;
  private stageCame = 0;
  private passed = 0;
  private weight = 0;
  private hint = 0;
  private shown = 0;
  private readonly stop: () => void;

  constructor(private readonly onChange: (reading: LoadReading) => void) {
    this.stop = onLoads((kind, id) => {
      if (id < this.firstId) return;
      const inStage = id >= this.stageFirstId;
      if (kind === 'start') { this.asked++; if (inStage) this.stageAsked++; } else { this.came++; if (inStage) this.stageCame++; }
      this.emit();
    });
  }

  /** The next stage, with its share of the bar (the shares of a load add up to 1). */
  stage(weight: number): void {
    this.passed += this.weight;
    this.weight = weight;
    this.stageFirstId = started + 1;
    this.stageAsked = 0;
    this.stageCame = 0;
    this.hint = 0;
    this.emit();
  }

  /** How far the stage says it is (0 to 1), for a stage that knows (createWorld's object types). */
  progress(fraction: number): void {
    this.hint = Math.max(this.hint, Math.min(1, fraction));
    this.emit();
  }

  /** The load is done: a full bar, and no more calls. */
  finish(): void {
    this.stop();
    this.shown = 1;
    this.onChange({ fraction: 1, done: this.came, total: this.asked });
  }

  /** Stops the calls without filling the bar (a load that failed or went away). */
  dispose(): void {
    this.stop();
  }

  /** The fraction shown now. */
  get fraction(): number { return this.shown; }

  private emit(): void {
    const files = this.stageAsked > 0 ? this.stageCame / this.stageAsked : 0;
    const inStage = Math.min(STAGE_CAP, Math.max(files, this.hint));
    this.shown = Math.max(this.shown, Math.min(1, this.passed + this.weight * inStage));
    this.onChange({ fraction: this.shown, done: this.came, total: this.asked });
  }
}
