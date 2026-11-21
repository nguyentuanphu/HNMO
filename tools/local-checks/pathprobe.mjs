// Lorencia: can the web game's pathfinder reach new-skills' goals from the bridge cell where /move drops the hero?
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-t110/client/web/package.json');
const { chromium } = require('playwright');
const b = await chromium.launch({ headless: true });
const p = await b.newPage();
await p.goto('http://127.0.0.1:8081/index.html');
console.log(JSON.stringify(await p.evaluate(async () => {
  const season = await import('/js/dataRoot.js'); season.setSeason('s9');
  const world = await import('/js/world.js');
  const { findPath } = await import('/js/game/pathfinding.js');
  const data = await world.loadWorldData(0);
  const { isWalkableCell } = await import("/js/game/pathfinding.js"); const out = { url: data.dir, w150_100: isWalkableCell(data.flags, 150, 100), w155_85: isWalkableCell(data.flags, 155, 85), w135_98: isWalkableCell(data.flags, 135, 98), w140_110: isWalkableCell(data.flags, 140, 110) };
  for (const [from, to] of [[{ x: 135, y: 98 }, { x: 150, y: 100 }], [{ x: 135, y: 97 }, { x: 150, y: 100 }], [{ x: 133, y: 121 }, { x: 150, y: 100 }], [{ x: 150, y: 100 }, { x: 155, y: 85 }], [{ x: 135, y: 98 }, { x: 155, y: 85 }], [{ x: 133, y: 121 }, { x: 155, y: 85 }], [{ x: 140, y: 125 }, { x: 155, y: 85 }]]) {
    out[`${from.x},${from.y}->${to.x},${to.y}`] = findPath(data.flags, from, to).length;
  }
  return out;
})));
await b.close();
