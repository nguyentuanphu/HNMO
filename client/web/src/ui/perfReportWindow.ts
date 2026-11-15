// HNMO extra: the performance report window (the settings window's "Performance report" button, or
// Ctrl+Shift+P). It records about 15 seconds of play with game/perfRecorder.ts, shows what it found
// (frame times, the slowest frames and the game's parts in them, long tasks, the functions that took the
// most time, the scene and the screen) and downloads the whole report as a JSON file to send to the owner.
// It has no desktop window: HNMO's own interface document (public/data/Interface/RmlUi/hnmo_perf_report.rml)
// in the game's message box frame (window_shell).

import { onLanguageChange } from '../i18n.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale } from './nativeText.js';
import { tr } from './missionsWindow.js';
import { FRAME_SECTIONS, PerfRecorder, RECORD_MS, type FrameSection, type PerfReport } from '../game/perfRecorder.js';

interface Line { text: string; bad: boolean; muted: boolean }
interface Section { head: string; lines: Line[] }

interface PerfModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  intro: string; recording: boolean; progress_width: string; has_report: boolean; sections: Section[];
  record_label: string; download_label: string; close_label: string;
  pr_record: () => void; pr_download: () => void; pr_close: () => void;
}

const SECTION_NAMES: Record<FrameSection, string> = {
  characters: 'Characters', combat: 'Combat', effects: 'Effects', interface: 'Interface', camera: 'Camera and names', render: 'Drawing',
};

function line(text: string, bad = false, muted = false): Line {
  return { text, bad, muted };
}

/** The report's readable summary (also used by the tests). */
export function summarize(report: PerfReport): Section[] {
  const f = report.frames;
  const sections: Section[] = [];
  sections.push({
    head: tr('Frames'),
    lines: [
      line(tr('%s frames per second on average; a frame takes %s ms (half of them under %s ms).', String(f.averageFps), String(f.averageMs), String(f.medianMs))),
      line(tr('Slow frames: %s over 25 ms, %s over 50 ms, %s over 100 ms; the slowest %s ms.', String(f.over25ms), String(f.over50ms), String(f.over100ms), String(f.maxMs)), f.over50ms > 0),
      line(tr('The screen refreshes every %s ms; the game missed %s refreshes.', String(f.refreshMs), String(f.missedRefreshes)), f.missedRefreshes > f.count / 20),
      line(tr('The game itself takes %s ms of a frame: %s.', String(f.gameAverageMs),
        FRAME_SECTIONS.map((s) => `${tr(SECTION_NAMES[s])} ${f.sectionsAverageMs[s] ?? 0}`).join(', ')), false, true),
    ],
  });
  if (report.worstFrames.length > 0) {
    sections.push({
      head: tr('Slowest frames'),
      lines: report.worstFrames.slice(0, 6).map((w) => {
        const parts = Object.entries(w.sections).sort((a, b) => b[1] - a[1]).slice(0, 3).map(([name, ms]) => `${tr(SECTION_NAMES[name as FrameSection])} ${ms}`);
        return line(`${(w.atMs / 1000).toFixed(1)} s: ${w.ms} ms${parts.length ? ` (${parts.join(', ')})` : ''}`, w.ms > 50);
      }),
    });
  }
  const p = report.profile;
  sections.push({
    head: tr('Functions with the most time'),
    lines: p.available
      ? [line(tr('The page was idle %s of the time.', `${p.idlePercent}%`), false, true),
        ...p.topSelf.slice(0, 10).map((t) => line(`${t.percent}% ${t.ms} ms  ${t.name}  ${t.file}:${t.line}:${t.column}`))]
      : [line(p.note || tr('No profile.'), false, true)],
  });
  const tasks = report.longTasks.length, loafs = report.longAnimationFrames.length;
  const taskMs = report.longTasks.reduce((sum, t) => sum + t.ms, 0);
  sections.push({
    head: tr('Long tasks'),
    lines: [
      line(tr('%s long tasks (%s ms in all), %s long animation frames.', String(tasks), taskMs.toFixed(0), String(loafs)), tasks > 0),
      ...report.longFrameScripts.slice(0, 5).map((s) => line(`${s.ms} ms ×${s.count}  ${s.source}`, false, true)),
    ],
  });
  const scene = report.sceneAtEnd as { characters?: number; effects?: number; labels?: number; domElements?: number; map?: string | number | null;
    renderer?: { calls: number; triangles: number; programs: number; geometries: number; textures: number }; canvas?: { width: number; height: number; cssWidth: number; cssHeight: number; pixelRatio: number } };
  const env = report.environment as { devicePixelRatio?: number; gpu?: { renderer: string }; cpuThreads?: number };
  const r = scene.renderer, c = scene.canvas;
  const heap = report.heap;
  sections.push({
    head: tr('Scene and screen'),
    lines: [
      line(tr('Map %s: %s characters, %s effects, %s name labels, %s page elements.', String(scene.map ?? '-'), String(scene.characters ?? 0), String(scene.effects ?? 0), String(scene.labels ?? 0), String(scene.domElements ?? 0))),
      ...(r ? [line(tr('Drawing: %s draw calls, %s triangles, %s programs, %s geometries, %s textures.', String(r.calls), String(r.triangles), String(r.programs), String(r.geometries), String(r.textures)))] : []),
      ...(c ? [line(tr('Canvas %s × %s pixels for a %s × %s window (pixel ratio %s, the screen\'s %s).', String(c.width), String(c.height), String(c.cssWidth), String(c.cssHeight), c.pixelRatio.toFixed(2), String(env.devicePixelRatio ?? 1)))] : []),
      line(`${env.gpu?.renderer ?? ''} · ${env.cpuThreads ?? '?'} CPU threads`, false, true),
      ...(heap ? [line(tr('Memory %s MB → %s MB, %s garbage collections (%s MB freed).', String(heap.startMb), String(heap.endMb), String(heap.garbageCollections), String(heap.freedMb)), false, true)] : []),
    ],
  });
  return sections;
}

export class PerfReportWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private report: PerfReport | null = null;
  private readonly model: PerfModel;

  constructor(private readonly recorder: PerfRecorder, private readonly options: { click: () => void; beforeRecord?: () => void }) {
    this.model = {
      has_title: true, title: '', positioned: false, root_x: 0, root_y: 0,
      intro: '', recording: false, progress_width: '0%', has_report: false, sections: [],
      record_label: '', download_label: '', close_label: '',
      pr_record: () => { this.options.click(); void this.record(); },
      pr_download: () => { this.options.click(); this.download(); },
      pr_close: () => { this.options.click(); this.close(); },
    };
    window.addEventListener('keydown', (event) => {
      if (event.ctrlKey && event.shiftKey && !event.altKey && event.code === 'KeyP') {
        event.preventDefault();
        if (this.recorder.recording) return;
        void this.open().then(() => this.record());
      } else if (event.key === 'Escape' && this.wanted && !this.recorder.recording) this.close();
    });
    window.addEventListener('resize', () => this.place());
    onLanguageChange(() => this.fill());
  }

  get isOpen(): boolean { return this.wanted; }
  /** The last report (tests). */
  get lastReport(): PerfReport | null { return this.report; }

  async open(): Promise<void> {
    this.wanted = true;
    this.fill();
    if (this.doc) { this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_perf_report', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-perf-report');
      doc.host.setAttribute('role', 'dialog');
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      this.place();
      this.fill();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
    await this.opening;
  }

  close(): void {
    this.wanted = false;
    this.doc?.close();
    this.doc = null;
  }

  /** Records about 15 seconds and shows the report. */
  async record(): Promise<PerfReport | null> {
    if (this.recorder.recording) return null;
    this.options.beforeRecord?.();
    this.model.recording = true;
    this.model.progress_width = '0%';
    this.fill();
    try {
      this.report = await this.recorder.record(RECORD_MS, (share) => {
        this.model.progress_width = `${Math.round(share * 100)}%`;
        this.doc?.update();
      });
    } finally {
      this.model.recording = false;
    }
    this.fill();
    return this.report;
  }

  /** Saves the report as a JSON file (hnmo-performance-<date>.json). */
  download(): void {
    if (!this.report) return;
    const blob = new Blob([JSON.stringify(this.report, null, 1)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `hnmo-performance-${this.report.createdUtc.replace(/[:.]/g, '-').slice(0, 19)}.json`;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10_000);
  }

  private fill(): void {
    const m = this.model;
    m.title = tr('Performance report');
    m.intro = m.recording
      ? tr('Recording for %s seconds: keep playing as usual.', String(RECORD_MS / 1000))
      : this.report
        ? tr('Download the report and send the file to the game\'s owner.')
        : tr('Records %s seconds of play: how long each frame takes and what takes the time.', String(RECORD_MS / 1000));
    m.record_label = this.report ? tr('Record again') : tr('Record');
    m.download_label = tr('Download');
    m.close_label = tr('Close');
    m.has_report = this.report !== null && !m.recording;
    m.sections = this.report ? summarize(this.report) : [];
    this.doc?.update();
  }

  private place(): void {
    const doc = this.doc;
    if (!doc) return;
    const scale = typographyScale();
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0px', top: '0px', width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none', zIndex: '90',
    });
    doc.setScale(scale);
  }
}
