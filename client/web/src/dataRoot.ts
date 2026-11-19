// The game data of the world the player picked.
//
// Every world plays a season (the admin panel's Worlds page). The base game data, Season 6.3, is under
// /data/...; a Season 9 world's models, maps, effects and sounds are under /data/s9/..., where the web
// server answers from the Season 9 data pack first and from the base data for the rest. A player of a
// Season 6 world never gets a Season 9 file.

/** The seasons a world can play: the keys the website's data uses. */
export type SeasonKey = 's6' | 's9';

let season: SeasonKey = 's6';
const listeners: (() => void)[] = [];

/** The season of the world being played ('s6' until a Season 9 world is picked). */
export function currentSeason(): SeasonKey {
  return season;
}

/** Reads a season key from the website's data; anything unknown is Season 6. */
export function parseSeason(value: unknown): SeasonKey {
  return value === 's9' ? 's9' : 's6';
}

/** What players see next to a world. */
export function seasonBadge(key: SeasonKey): string {
  return key === 's9' ? 'S9' : 'S6.3';
}

/** Switches the data root to a world's season; whatever was loaded for another season is dropped. */
export function setSeason(key: SeasonKey): void {
  if (key === season) return;
  season = key;
  for (const listener of listeners) listener();
}

/** Calls back when the season changes (to drop cached files of the other season). */
export function onSeasonChange(listener: () => void): void {
  listeners.push(listener);
}

/** The address of a data file for the current season: '/data/x' stays for Season 6, '/data/s9/x' for Season 9. */
export function seasonDataUrl(url: string): string {
  if (season === 's6' || !url.startsWith('/data/') || url.startsWith('/data/s9/')) return url;
  return `/data/${season}/${url.slice('/data/'.length)}`;
}
