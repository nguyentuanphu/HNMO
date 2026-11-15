// HNMO extra: the performance recorder (Settings, "Performance report", or Ctrl+Shift+P). It records about
// 15 seconds of play on the player's own computer and turns it into a report the owner can send back:
//  - every presented frame's time (requestAnimationFrame's timestamps) and the game's own split of it
//    (main.ts's frame loop: characters, combat, effects, interface, camera and labels, the draw call);
//  - long tasks and long animation frames (with the scripts Chrome blames for them);
//  - a sampled JavaScript profile (the JS Self-Profiling API; the bridge sends `Document-Policy: js-profiling`
//    with the game page), summed into the functions that took the most time, with their file:line:column;
//  - the renderer's counters (draw calls, triangles, programs, geometries, textures), how many characters,
//    effects and DOM elements there are, the canvas size and pixel ratio, the quality settings;
//  - the JS heap now and then (a drop is a garbage collection).
// The report is plain JSON (downloaded by the report window, ui/perfReportWindow.ts).

import type * as THREE from 'three';

export const RECORD_MS = 15_000;
const HEAP_SAMPLE_MS = 100;
const PROFILE_INTERVAL_MS = 10;
const TOP = 25;

/** The frame loop's parts (main.ts), in the order it runs them. */
export const FRAME_SECTIONS = ['characters', 'combat', 'effects', 'interface', 'camera', 'render'] as const;
export type FrameSection = typeof FRAME_SECTIONS[number];

export interface PerfSources {
  renderer: THREE.WebGLRenderer;
  canvas: HTMLCanvasElement;
  /** What is in the world now. */
  scene(): Record<string, number | string | null>;
  /** The quality and interface settings now. */
  settings(): Record<string, unknown>;
}

interface ProfilerFrame { name: string; resourceId?: number; line?: number; column?: number }
interface ProfilerTrace {
  resources: string[];
  frames: ProfilerFrame[];
  stacks: { parentId?: number; frameId: number }[];
  samples: { timestamp: number; stackId?: number }[];
}
interface JsProfiler { stop(): Promise<ProfilerTrace> }
type ProfilerConstructor = new (options: { sampleInterval: number; maxBufferSize: number }) => JsProfiler;

interface LoafScript {
  invoker?: string; invokerType?: string; sourceURL?: string; sourceFunctionName?: string; sourceCharPosition?: number;
  duration: number; forcedStyleAndLayoutDuration?: number; startTime: number;
}
interface LoafEntry extends PerformanceEntry {
  blockingDuration?: number; renderStart?: number; styleAndLayoutStart?: number; scripts?: LoafScript[];
}

export interface FunctionTime { name: string; file: string; line: number; column: number; ms: number; percent: number }
export interface WorstFrame { atMs: number; ms: number; sections: Partial<Record<FrameSection, number>> }

export interface PerfReport {
  format: 'hnmo-performance-report';
  version: 1;
  createdUtc: string;
  createdSydney: string;
  durationMs: number;
  environment: Record<string, unknown>;
  settings: Record<string, unknown>;
  sceneAtStart: Record<string, unknown>;
  sceneAtEnd: Record<string, unknown>;
  frames: {
    count: number; averageFps: number; averageMs: number; medianMs: number; p90Ms: number; p95Ms: number; p99Ms: number; maxMs: number;
    /** The screen's refresh interval as the frames show it (their shortest common time). */
    refreshMs: number;
    /** Frames the screen showed again because the game was not ready (each long frame's extra refreshes). */
    missedRefreshes: number;
    over25ms: number; over50ms: number; over100ms: number;
    /** Frames that missed a refresh, and of them those where the game's own work took under half the frame
     *  (the time went elsewhere: the browser, the graphics driver, other programs, garbage collection). */
    slowFrames: number;
    slowOutsideGame: number;
    /** The game's own time per frame, on average, by part (ms). */
    sectionsAverageMs: Partial<Record<FrameSection, number>>;
    /** The frame loop's whole time on average (ms) and the share of the frame interval it takes. */
    gameAverageMs: number;
  };
  worstFrames: WorstFrame[];
  /** Every frame's time in ms (one decimal). */
  frameTimesMs: number[];
  longTasks: { atMs: number; ms: number; attribution: string }[];
  longAnimationFrames: { atMs: number; ms: number; blockingMs: number; renderMs: number; scripts: { invoker: string; source: string; ms: number; forcedLayoutMs: number }[] }[];
  /** The scripts long animation frames blame most, summed. */
  longFrameScripts: { source: string; ms: number; count: number }[];
  profile: { available: boolean; note: string; sampleIntervalMs: number; samples: number; idlePercent: number; topSelf: FunctionTime[]; topTotal: FunctionTime[] };
  heap: { startMb: number; endMb: number; peakMb: number; garbageCollections: number; freedMb: number; samples: [number, number][] } | null;
}

function round(value: number, digits = 2): number {
  const f = 10 ** digits;
  return Math.round(value * f) / f;
}

function percentile(sorted: number[], p: number): number {
  if (sorted.length === 0) return 0;
  return sorted[Math.min(sorted.length - 1, Math.floor(p * sorted.length))];
}

function shortFile(url: string): string {
  if (!url) return '(native)';
  try {
    const u = new URL(url, location.href);
    return u.origin === location.origin ? u.pathname : u.href;
  } catch {
    return url;
  }
}

function sydneyTime(date: Date): string {
  try {
    return new Intl.DateTimeFormat('en-AU', { timeZone: 'Australia/Sydney', dateStyle: 'medium', timeStyle: 'medium' }).format(date);
  } catch {
    return date.toISOString();
  }
}

function heapMb(): number | null {
  const memory = (performance as Performance & { memory?: { usedJSHeapSize: number } }).memory;
  return memory ? memory.usedJSHeapSize / 1_048_576 : null;
}

function gpuName(renderer: THREE.WebGLRenderer): { vendor: string; renderer: string } {
  try {
    const gl = renderer.getContext();
    const info = gl.getExtension('WEBGL_debug_renderer_info');
    if (info) return { vendor: String(gl.getParameter(info.UNMASKED_VENDOR_WEBGL)), renderer: String(gl.getParameter(info.UNMASKED_RENDERER_WEBGL)) };
    return { vendor: String(gl.getParameter(gl.VENDOR)), renderer: String(gl.getParameter(gl.RENDERER)) };
  } catch {
    return { vendor: '', renderer: '' };
  }
}

/** Sums a JS Self-Profiling trace into the functions with the most time of their own and in total. */
export function summarizeProfile(trace: ProfilerTrace, intervalMs: number): { samples: number; idlePercent: number; topSelf: FunctionTime[]; topTotal: FunctionTime[] } {
  const self = new Map<number, number>();
  const total = new Map<number, number>();
  let all = 0, idle = 0;
  const samples = trace.samples;
  for (let i = 0; i < samples.length; i++) {
    const next = samples[i + 1];
    const ms = next ? Math.min(next.timestamp - samples[i].timestamp, intervalMs * 4) : intervalMs;
    all += ms;
    const stackId = samples[i].stackId;
    if (stackId === undefined) { idle += ms; continue; }
    const top = trace.stacks[stackId]?.frameId;
    if (top !== undefined) self.set(top, (self.get(top) ?? 0) + ms);
    const seen = new Set<number>();
    for (let id: number | undefined = stackId; id !== undefined; id = trace.stacks[id]?.parentId) {
      const frameId = trace.stacks[id]?.frameId;
      if (frameId === undefined || seen.has(frameId)) continue;
      seen.add(frameId);
      total.set(frameId, (total.get(frameId) ?? 0) + ms);
    }
  }
  const busy = Math.max(1, all - idle);
  const list = (map: Map<number, number>): FunctionTime[] => [...map].sort((a, b) => b[1] - a[1]).slice(0, TOP).map(([frameId, ms]) => {
    const frame = trace.frames[frameId];
    return {
      name: frame?.name || '(anonymous)',
      file: shortFile(frame?.resourceId !== undefined ? trace.resources[frame.resourceId] ?? '' : ''),
      line: frame?.line ?? 0, column: frame?.column ?? 0, ms: round(ms, 1), percent: round(100 * ms / busy, 1),
    };
  });
  return { samples: samples.length, idlePercent: all > 0 ? round(100 * idle / all, 1) : 0, topSelf: list(self), topTotal: list(total) };
}

/** The recorder: main.ts's frame loop tells it each frame's parts while it records. */
export class PerfRecorder {
  private active = false;
  private start = 0;
  private lastFrame = 0;
  private frameTimes: number[] = [];
  private frameAt: number[] = [];
  private frameSections: Float32Array[] = [];
  private pending: Float32Array = new Float32Array(FRAME_SECTIONS.length);
  private sectionStart = 0;
  private longTasks: PerfReport['longTasks'] = [];
  private loafs: PerfReport['longAnimationFrames'] = [];
  private heapSamples: [number, number][] = [];
  private lastHeap = 0;

  constructor(private readonly sources: PerfSources) {}

  /** Whether it is recording (main.ts measures its frame's parts only then). */
  get recording(): boolean { return this.active; }

  /** Starts a frame's measurement (main.ts, at the top of its frame). */
  beginFrame(): void {
    this.sectionStart = performance.now();
    this.pending.fill(0);
  }

  /** Ends a part of the frame: the time since the previous part goes to `section`. */
  lap(section: FrameSection): void {
    const now = performance.now();
    this.pending[FRAME_SECTIONS.indexOf(section)] += now - this.sectionStart;
    this.sectionStart = now;
  }

  /** The parts of the last measured frame, in FRAME_SECTIONS' order (ms). */
  get lastSections(): Float32Array { return this.pending; }

  /** A presented frame: its requestAnimationFrame timestamp (the frame loop draws in it). */
  endFrame(now: number): void {
    if (!this.active) return;
    if (this.lastFrame > 0) {
      this.frameTimes.push(now - this.lastFrame);
      this.frameAt.push(now - this.start);
      this.frameSections.push(this.pending.slice());
    }
    this.lastFrame = now;
    if (now - this.lastHeap >= HEAP_SAMPLE_MS) {
      this.lastHeap = now;
      const mb = heapMb();
      if (mb !== null) this.heapSamples.push([round(now - this.start, 0), round(mb, 2)]);
    }
  }

  /** Records for `durationMs` and returns the report. `progress` hears the share done (0..1). */
  async record(durationMs = RECORD_MS, progress?: (share: number) => void): Promise<PerfReport> {
    if (this.active) throw new Error('already recording');
    const { renderer } = this.sources;
    const created = new Date();
    this.frameTimes = []; this.frameAt = []; this.frameSections = []; this.longTasks = []; this.loafs = []; this.heapSamples = [];
    this.lastFrame = 0; this.lastHeap = 0;
    const sceneAtStart = this.sceneNow();
    const observers: PerformanceObserver[] = [];
    const supported = PerformanceObserver.supportedEntryTypes ?? [];
    if (supported.includes('longtask')) {
      const observer = new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          const attribution = (entry as PerformanceEntry & { attribution?: { name?: string; containerType?: string; containerSrc?: string }[] }).attribution ?? [];
          this.longTasks.push({
            atMs: round(entry.startTime - this.start, 0), ms: round(entry.duration, 1),
            attribution: attribution.map((a) => [a.name, a.containerType, a.containerSrc].filter(Boolean).join(' ')).join('; '),
          });
        }
      });
      observer.observe({ type: 'longtask', buffered: false });
      observers.push(observer);
    }
    if (supported.includes('long-animation-frame')) {
      const observer = new PerformanceObserver((list) => {
        for (const raw of list.getEntries()) {
          const entry = raw as LoafEntry;
          this.loafs.push({
            atMs: round(entry.startTime - this.start, 0), ms: round(entry.duration, 1), blockingMs: round(entry.blockingDuration ?? 0, 1),
            renderMs: round(entry.renderStart ? entry.startTime + entry.duration - entry.renderStart : 0, 1),
            scripts: (entry.scripts ?? []).map((s) => ({
              invoker: s.invoker ?? s.invokerType ?? '',
              source: `${s.sourceFunctionName || '(anonymous)'} ${shortFile(s.sourceURL ?? '')}:${s.sourceCharPosition ?? -1}`,
              ms: round(s.duration, 1), forcedLayoutMs: round(s.forcedStyleAndLayoutDuration ?? 0, 1),
            })),
          });
        }
      });
      observer.observe({ type: 'long-animation-frame', buffered: false });
      observers.push(observer);
    }
    let profiler: JsProfiler | null = null;
    let profileNote = '';
    const Profiler = (window as unknown as { Profiler?: ProfilerConstructor }).Profiler;
    if (!Profiler) profileNote = 'This browser has no JS Self-Profiling API (Chrome and Edge have it).';
    else {
      try {
        profiler = new Profiler({ sampleInterval: PROFILE_INTERVAL_MS, maxBufferSize: Math.ceil(durationMs / PROFILE_INTERVAL_MS) * 2 });
      } catch (error) {
        profileNote = `The profiler could not start: ${error instanceof Error ? error.message : String(error)}`;
      }
    }
    const infoStart = { ...renderer.info.render, programs: renderer.info.programs?.length ?? 0, ...renderer.info.memory };
    this.start = performance.now();
    this.active = true;
    await new Promise<void>((resolve) => {
      const tick = () => {
        const share = (performance.now() - this.start) / durationMs;
        progress?.(Math.min(1, share));
        if (share >= 1) resolve(); else setTimeout(tick, 250);
      };
      tick();
    });
    this.active = false;
    const elapsed = performance.now() - this.start;
    for (const observer of observers) observer.disconnect();
    let profile: PerfReport['profile'] = { available: false, note: profileNote, sampleIntervalMs: PROFILE_INTERVAL_MS, samples: 0, idlePercent: 0, topSelf: [], topTotal: [] };
    if (profiler) {
      try {
        const trace = await profiler.stop();
        profile = { available: true, note: '', sampleIntervalMs: PROFILE_INTERVAL_MS, ...summarizeProfile(trace, PROFILE_INTERVAL_MS) };
      } catch (error) {
        profile.note = `The profiler failed: ${error instanceof Error ? error.message : String(error)}`;
      }
    }
    const sceneAtEnd = this.sceneNow();
    sceneAtEnd.rendererAtStart = infoStart;
    return this.build(created, elapsed, sceneAtStart, sceneAtEnd, profile);
  }

  private sceneNow(): Record<string, unknown> {
    const { renderer, canvas } = this.sources;
    const info = renderer.info;
    return {
      ...this.sources.scene(),
      domElements: document.getElementsByTagName('*').length,
      renderer: {
        calls: info.render.calls, triangles: info.render.triangles, points: info.render.points, lines: info.render.lines,
        programs: info.programs?.length ?? 0, geometries: info.memory.geometries, textures: info.memory.textures,
      },
      canvas: { cssWidth: canvas.clientWidth, cssHeight: canvas.clientHeight, width: canvas.width, height: canvas.height, pixelRatio: renderer.getPixelRatio() },
    };
  }

  private build(created: Date, elapsed: number, sceneAtStart: Record<string, unknown>, sceneAtEnd: Record<string, unknown>, profile: PerfReport['profile']): PerfReport {
    const times = this.frameTimes;
    const sorted = [...times].sort((a, b) => a - b);
    const count = times.length;
    const sum = times.reduce((a, b) => a + b, 0);
    // The refresh interval: the median of the shorter half of the frames (vsync's step).
    const refreshMs = Math.max(1, percentile(sorted, 0.25));
    let missed = 0, slowFrames = 0, slowOutsideGame = 0;
    times.forEach((ms, i) => {
      missed += Math.max(0, Math.round(ms / refreshMs) - 1);
      if (ms < refreshMs * 1.5) return;
      slowFrames++;
      if (this.frameSections[i].reduce((a, b) => a + b, 0) < ms / 2) slowOutsideGame++;
    });
    const sectionsSum = new Float64Array(FRAME_SECTIONS.length);
    for (const s of this.frameSections) for (let i = 0; i < s.length; i++) sectionsSum[i] += s[i];
    const sectionsAverageMs: Partial<Record<FrameSection, number>> = {};
    FRAME_SECTIONS.forEach((name, i) => { sectionsAverageMs[name] = count ? round(sectionsSum[i] / count, 3) : 0; });
    const gameAverageMs = count ? round(sectionsSum.reduce((a, b) => a + b, 0) / count, 3) : 0;
    const worst = times.map((ms, i) => i).sort((a, b) => times[b] - times[a]).slice(0, 15).map((i): WorstFrame => {
      const sections: Partial<Record<FrameSection, number>> = {};
      FRAME_SECTIONS.forEach((name, k) => { const v = this.frameSections[i][k]; if (v >= 0.05) sections[name] = round(v, 2); });
      return { atMs: round(this.frameAt[i], 0), ms: round(times[i], 1), sections };
    });
    const scripts = new Map<string, { ms: number; count: number }>();
    for (const loaf of this.loafs) for (const s of loaf.scripts) {
      const one = scripts.get(s.source) ?? { ms: 0, count: 0 };
      one.ms += s.ms; one.count++;
      scripts.set(s.source, one);
    }
    let heap: PerfReport['heap'] = null;
    if (this.heapSamples.length > 1) {
      let collections = 0, freed = 0;
      for (let i = 1; i < this.heapSamples.length; i++) {
        const drop = this.heapSamples[i - 1][1] - this.heapSamples[i][1];
        if (drop > 0.5) { collections++; freed += drop; }
      }
      heap = {
        startMb: this.heapSamples[0][1], endMb: this.heapSamples[this.heapSamples.length - 1][1],
        peakMb: Math.max(...this.heapSamples.map((s) => s[1])), garbageCollections: collections, freedMb: round(freed, 1), samples: this.heapSamples,
      };
    }
    const { renderer, canvas } = this.sources;
    const nav = navigator as Navigator & { deviceMemory?: number; userAgentData?: { brands?: { brand: string; version: string }[]; platform?: string } };
    return {
      format: 'hnmo-performance-report', version: 1,
      createdUtc: created.toISOString(), createdSydney: sydneyTime(created), durationMs: round(elapsed, 0),
      environment: {
        userAgent: navigator.userAgent, brands: nav.userAgentData?.brands, platform: nav.userAgentData?.platform ?? navigator.platform,
        cpuThreads: navigator.hardwareConcurrency, deviceMemoryGb: nav.deviceMemory, gpu: gpuName(renderer),
        devicePixelRatio: window.devicePixelRatio, screen: { width: screen.width, height: screen.height },
        window: { width: window.innerWidth, height: window.innerHeight }, fullScreen: document.fullscreenElement !== null,
        canvas: { cssWidth: canvas.clientWidth, cssHeight: canvas.clientHeight, width: canvas.width, height: canvas.height },
        bundle: [...document.scripts].map((s) => s.src).filter((s) => s.includes('/js/')).map(shortFile).join(' '),
        visibility: document.visibilityState,
      },
      settings: this.sources.settings(),
      sceneAtStart, sceneAtEnd,
      frames: {
        count, averageFps: elapsed > 0 ? round(count * 1000 / elapsed, 1) : 0, averageMs: count ? round(sum / count) : 0,
        medianMs: round(percentile(sorted, 0.5)), p90Ms: round(percentile(sorted, 0.9)), p95Ms: round(percentile(sorted, 0.95)),
        p99Ms: round(percentile(sorted, 0.99)), maxMs: round(sorted[count - 1] ?? 0), refreshMs: round(refreshMs), missedRefreshes: missed,
        over25ms: times.filter((t) => t > 25).length, over50ms: times.filter((t) => t > 50).length, over100ms: times.filter((t) => t > 100).length,
        slowFrames, slowOutsideGame, sectionsAverageMs, gameAverageMs,
      },
      worstFrames: worst,
      frameTimesMs: times.map((t) => round(t, 1)),
      longTasks: this.longTasks,
      longAnimationFrames: this.loafs,
      longFrameScripts: [...scripts].sort((a, b) => b[1].ms - a[1].ms).slice(0, TOP).map(([source, s]) => ({ source, ms: round(s.ms, 1), count: s.count })),
      profile,
      heap,
    };
  }
}
