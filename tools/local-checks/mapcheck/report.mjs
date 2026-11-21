// Turns the map check reports into the per-map tables of docs/map-check-2026-11-20.md (printed to stdout).
// Usage: node report.mjs <s6 json> <s9 json>
import fs from 'node:fs';
const notes = {
  // Known by design or the same as the original client (checked against the Season 6.3 client files).
  translucent: new Set([11, 22, 69]), // Ghost, Ice Monster, Alquamos: drawn see-through / glowing on the desktop too
};
for (const file of process.argv.slice(2)) {
  const r = JSON.parse(fs.readFileSync(file, 'utf8'));
  console.log(`\n### ${r.season === 's9' ? 'Season 9' : 'Season 6.3'} (${r.maps.length} maps tried, ${r.maps.filter((m) => m.arrived).length} entered)\n`);
  console.log('| map | name | monster/NPC types seen | faults | missing files (also missing in the original client unless said) | picture |');
  console.log('|---|---|---|---|---|---|');
  for (const m of r.maps) {
    const ms = Object.values(m.monsters);
    const faults = [];
    for (const x of ms) {
      if (x.unknown) faults.push(`${x.number} ${x.name}: ghost (${x.unknown})`);
      else if (x.untextured) faults.push(`${x.number} ${x.name}: ${x.untextured} untextured mesh(es)`);
      else if ((x.seeThrough || x.additiveOnly) && !notes.translucent.has(x.number)) faults.push(`${x.number} ${x.name}: see-through`);
      else if (x.shown === 0 && x.kind !== 'trap' && x.meshes > 0 && ![104, 106, 205, 206, 207, 208, 209, 364, 523].includes(x.number)) faults.push(`${x.number} ${x.name}: nothing drawn`);
    }
    const objTex = [...new Set(m.console.filter((c) => /^Texture missing: .* in Object\d+(, Item)?$/.test(c)).map((c) => c.split(' ')[2]))];
    const inItem = objTex.filter((t) => t !== 'bons.jpg');
    if (inItem.length) faults.push(`grey object meshes: ${inItem.join(', ')} (textures only Item/ has: **fixed**, object texture PR)`);
    if (objTex.includes('bons.jpg')) faults.push('bons.jpg missing for an object (missing in the original client\'s folder too: open, minor)');
    const files = m.http.map((h) => h.replace(/^404 \/data\//, '')).filter((h) => !/^Skill\//.test(h));
    const types = ms.length ? ms.map((x) => `${x.number}${x.playerShaped ? '*' : ''}`).sort((a, b) => parseInt(a) - parseInt(b)).join(', ') : (m.arrived ? 'none in view' : '-');
    const name = m.name ?? '';
    console.log(`| ${m.map} | ${m.arrived ? name : `${name} (not entered: ${m.map === 9 || m.map === 32 ? 'event map' : 'no warp / event map'})`} | ${types} | ${faults.join('; ') || 'none'} | ${files.slice(0, 6).join(', ')}${files.length > 6 ? ` (+${files.length - 6})` : ''}${m.objects?.skippedTypes?.length ? `; objects skipped: ${m.objects.skippedTypes.join(' ')}` : ''} | ${m.shot ? `[jpg](screenshots/map-check-2026-11-20/${r.season}/${m.shot})` : ''} |`);
  }
}
