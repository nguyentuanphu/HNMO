// The game's start screen after logging in: the servers of Season 6.3 worlds and of Season 9 worlds side by side
// (seasons plan, step 4), and the HNMO Arena's when it runs (a third column). Each server belongs to its world's season
// (the admin panel's Worlds page); a character stays in the season it was made in, so the character list that follows
// is that season's.

import type { SeasonKey } from '../dataRoot.js';

/** A column of the start screen: a season's, or the Arena's (battles across servers and seasons). */
export type ColumnKey = SeasonKey | 'arena';

/** The columns, Season 6.3 first, each in the order the servers came in. */
export interface SeasonColumns<T> {
  s6: T[];
  s9: T[];
  arena: T[];
}

/** Splits servers into the columns; a server without a known season is Season 6.3's. */
export function groupBySeason<T>(servers: readonly T[], seasonOf: (server: T) => ColumnKey | undefined): SeasonColumns<T> {
  const columns: SeasonColumns<T> = { s6: [], s9: [], arena: [] };
  for (const server of servers) {
    const key = seasonOf(server);
    columns[key === 's9' || key === 'arena' ? key : 's6'].push(server);
  }
  return columns;
}

/** The texts of the start screen, in English and Vietnamese. */
export function seasonColumnTexts(vi: boolean): { heading: Record<ColumnKey, string>; hint: Record<ColumnKey, string>; empty: string; title: string } {
  return vi
    ? {
      title: 'Chọn máy chủ',
      heading: { s6: 'Season 6.3', s9: 'Season 9', arena: 'Đấu trường' },
      hint: { s6: 'Thế giới cổ điển', s9: 'Thế giới mới, nhiều bản đồ hơn', arena: 'Đấu giữa các server và season' },
      empty: 'Chưa có thế giới nào mở.',
    }
    : {
      title: 'Choose a server',
      heading: { s6: 'Season 6.3', s9: 'Season 9', arena: 'Arena' },
      hint: { s6: 'The classic worlds', s9: 'The newer worlds, more maps', arena: 'Battles across servers and seasons' },
      empty: 'No world open yet.',
    };
}

/**
 * Shows the servers in columns (two, three with the Arena) and resolves with the one clicked. Each server is a `.choice` button, Season 6.3's
 * first, so "the first server" stays what it was before the columns.
 */
export function chooseServerBySeason<T>(
  container: HTMLElement,
  servers: readonly T[],
  seasonOf: (server: T) => ColumnKey | undefined,
  describe: (server: T) => string,
  vi: boolean,
): Promise<T> {
  const texts = seasonColumnTexts(vi);
  const columns = groupBySeason(servers, seasonOf);
  container.replaceChildren();
  container.classList.add('seasons');
  container.hidden = false;
  const title = document.createElement('p');
  title.className = 'seasons-title';
  title.textContent = texts.title;
  container.append(title);
  return new Promise<T>((resolve) => {
    for (const key of ['s6', 's9', 'arena'] as const) {
      // The Arena's column shows only while the Arena runs.
      if (key === 'arena' && !columns.arena.length) continue;
      const column = document.createElement('section');
      column.className = `season-column season-${key}`;
      column.dataset.season = key;
      column.setAttribute('aria-label', texts.heading[key]);
      const heading = document.createElement('h2');
      heading.textContent = texts.heading[key];
      const hint = document.createElement('small');
      hint.textContent = texts.hint[key];
      heading.append(hint);
      column.append(heading);
      for (const server of columns[key]) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'choice';
        button.innerHTML = describe(server);
        button.addEventListener('click', () => resolve(server), { once: true });
        column.append(button);
      }
      if (!columns[key].length) {
        const empty = document.createElement('p');
        empty.className = 'season-empty';
        empty.textContent = texts.empty;
        column.append(empty);
      }
      container.append(column);
    }
  });
}
