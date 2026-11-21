const fs=require('fs'); const labels=process.argv.slice(2);
for (const l of labels) {
  const f='skillsweep/'+l+'/rows.json'; if(!fs.existsSync(f)){console.log(l,'no rows');continue;}
  const {rows,errors}=JSON.parse(fs.readFileSync(f));
  const log=fs.readFileSync('skillsweep-'+l+'.log','utf8'); const hero=((log.match(/hero (\{.*\})/)||[])[1]||'').replace(/"season":null,?/,''); const field=(log.match(/at field (\{.*\})/)||[])[1];
  const p95=Math.max(...rows.map(r=>r.p95||0)), max=Math.max(...rows.map(r=>r.max||0));
  console.log(l, hero, field, '| skills',rows.length,'cast',rows.filter(r=>r.cast).length,'dmg',rows.filter(r=>r.damage>0).length,'fx',rows.filter(r=>r.effectPeak>0).length,'| p95',p95,'max',max,'| errors',errors.length);
  for(const r of rows.filter(r=>!r.cast||r.effectPeak===0||(r.damage===0))) console.log('   ',r.n,r.name,'cast',r.cast,'dist',r.dist,'dmg',r.damage,'fx',r.effectPeak);
}
