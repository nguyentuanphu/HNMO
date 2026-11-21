// Prints the faults of a map check report: per map the numbers with the unknown look, untextured or see-through
// meshes, nothing drawn, a player body where the entry names a model, plus failed files and skipped objects.
import fs from 'node:fs';
const r = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
for (const m of r.maps) {
  const ms = Object.values(m.monsters);
  const faults = ms.filter((x) => x.unknown || x.untextured || x.seeThrough || x.additiveOnly || (x.shown === 0 && x.kind !== 'trap'));
  console.log(`MAP ${m.map} ${m.name ?? '-'} arrived=${m.arrived} types=${ms.length} spots=${m.spots.map((s) => s.seen).join(',')} skipped=${m.objects?.skippedTypes?.join(' ') ?? '-'} http=${m.http.length} pageErrors=${m.pageErrors.length}`);
  for (const x of faults) console.log(`   FAULT ${x.number} ${x.name} [${x.kind}] model=${x.model} body=${x.body} skin=${x.skin} unknown=${x.unknown} shown=${x.shown}/${x.meshes} untex=${x.untextured} see=${x.seeThrough} add=${x.additiveOnly} dead=${x.dead}`);
  for (const h of m.http) console.log(`   HTTP ${h}`);
  for (const c of m.console.slice(0, 8)) console.log(`   CONSOLE ${c}`);
}
