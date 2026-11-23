const fs = require('fs');
const data = JSON.parse(fs.readFileSync('docs/audit/coverage.json', 'utf8'));
const notChecked = data.units.filter(u => u.status === 'Not checked' && u.id.startsWith('server/src/GameLogic'));
notChecked.forEach((u, i) => {
  console.log((i+1) + '. ' + u.id + ' (' + u.files.length + ' files, ' + u.lines + ' lines)');
});