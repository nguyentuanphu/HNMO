// Extended parsers for remaining Season 6 and Season 9 server suite rule files:
// quests, all events and invasions, item bags, master skill tree, Castle Siege, Gens, and common/limit settings.

import fs from 'node:fs';
import path from 'node:path';
import {
  findSuiteFile,
  findSuiteFilesInDir,
  parseIni,
  parseSectionedTxt,
  parseXmlSections,
  parseXmlTags,
} from './suite-files.mjs';

const num = (v, d = 0) => (v === undefined || v === '' ? d : Number(v));
const cleanName = (f) => {
  const base = path.basename(f);
  const idx = base.indexOf('_');
  return idx > 0 ? base.slice(idx + 1) : base;
};

function parseKeyValCfg(filePath) {
  if (!filePath || !fs.existsSync(filePath)) return {};
  const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/);
  const out = {};
  for (const raw of lines) {
    const line = raw.replace(/\/\/.*$/, '').replace(/;.*$/, '').trim();
    if (!line || line.startsWith('[')) continue;
    const m = /^([A-Za-z0-9_.-]+)\s*=\s*(.+)$/.exec(line);
    if (m) out[m[1]] = m[2].trim();
  }
  return out;
}

export function parseSuiteQuests(rootDir) {
  const classQuestPath = findSuiteFile(rootDir, 'ClassQuest.xml');
  const questIniPath = findSuiteFile(rootDir, 'QuestSystem.ini');

  const quests = [];
  if (classQuestPath && fs.existsSync(classQuestPath)) {
    const raw = fs.readFileSync(classQuestPath, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
    const qiRe = /<QuestInfo\s+([^>]*)>([\s\S]*?)<\/QuestInfo>/gi;
    let qm;
    while ((qm = qiRe.exec(raw)) !== null) {
      const head = parseXmlTagsInText(`<QuestInfo ${qm[1]}>`, 'QuestInfo')[0] ?? {};
      const body = qm[2];
      const jobs = parseXmlTagsInText(body, 'Job').map((j) => ({
        type: num(j.Type),
        reqItemCat: num(j.ReqItemCat),
        reqItemIndex: num(j.ReqItemIndex),
        itemLevel: num(j.ItemLevel),
        itemCount: num(j.ItemCount),
        minLevel: num(j.MonsterMinLevel, -1),
        maxLevel: num(j.MonsterMaxLevel, -1),
        dropRate: num(j.DropRate),
        rewardType: num(j.RewardType),
        rewardCount: num(j.RewardCount),
      }));
      const conditions = parseXmlTagsInText(body, 'Condition').map((c) => ({
        type: num(c.Type, -1),
        index: num(c.Index, -1),
        minLevel: num(c.MinLevel),
        maxLevel: num(c.MaxLevel),
        reqMoney: num(c.ReqMoney),
      }));
      const minLevel = Math.max(0, ...conditions.filter((c) => c.minLevel > 0 && c.minLevel < 9999).map((c) => c.minLevel));
      const reqMoney = Math.max(0, ...conditions.map((c) => c.reqMoney));
      const rewardPts = jobs.reduce((s, j) => (j.rewardType === 200 || j.rewardType === 201 ? Math.max(s, j.rewardCount) : j.rewardType === 204 ? Math.max(s, j.rewardCount) : s), 0);
      quests.push({
        index: num(head.Index),
        type: num(head.Type),
        subType: num(head.SubType),
        name: head.Name ?? '',
        minLevel,
        reqMoney,
        rewardPts,
        jobs,
        conditions,
      });
    }
  }

  const jobs = quests.flatMap((q) => q.jobs);
  const ini = parseIni(questIniPath);
  const iniRows = [];
  if (questIniPath && fs.existsSync(questIniPath)) {
    for (const raw of fs.readFileSync(questIniPath, 'utf8').split(/\r?\n/)) {
      const line = raw.replace(/\/\/.*$/, '').trim();
      if (!line || line === '0' || line.toLowerCase() === 'end') continue;
      const m = /^(\d+)\s+"([^"]+)"\s+"([^"]+)"\s+(.*)$/.exec(line);
      if (m) iniRows.push({ id: num(m[1]), title: m[2], desc: m[3], rest: m[4].trim().split(/\s+/).map(Number) });
    }
  }

  // Parse Episode quest Lua scripts (Scripts/Quests/Quest_Ep*.lua & Quest_Reward_Ep*.lua)
  const epQuests = new Map();
  const luaFiles = findSuiteFilesInDir(rootDir, 'Quests', '.lua');
  const getEp = (ep, qs) => {
    const k = `${ep}:${qs}`;
    if (!epQuests.has(k)) {
      epQuests.set(k, { ep: num(ep), qs: num(qs), kills: [], items: [], rewardExp: 0, rewardZen: 0, rewardContrib: 0, rewardItems: 0 });
    }
    return epQuests.get(k);
  };
  for (const f of luaFiles) {
    const raw = fs.readFileSync(f, 'utf8');
    for (const rawLine of raw.split(/\r?\n/)) {
      const line = rawLine.replace(/--.*$/, '');
      let m;
      if ((m = /SetQuestMonsterKill\([^,]+,\s*Ep(\d+),\s*(\d+),\s*(\d+),\s*(\d+)/.exec(line))) {
        getEp(m[1], m[2]).kills.push({ monster: num(m[3]), count: num(m[4]) });
      }
      if ((m = /SetQuestRewardExp\(\s*Ep(\d+),\s*(\d+),\s*(\d+)/.exec(line))) {
        getEp(m[1], m[2]).rewardExp += num(m[3]);
      }
      if ((m = /SetQuestRewardZen\(\s*Ep(\d+),\s*(\d+),\s*(\d+)/.exec(line))) {
        getEp(m[1], m[2]).rewardZen += num(m[3]);
      }
      if ((m = /SetQuestRewardContribute\(\s*Ep(\d+),\s*(\d+),\s*(\d+)/.exec(line))) {
        getEp(m[1], m[2]).rewardContrib += num(m[3]);
      }
      if ((m = /SetQuestRewardItem\(\s*Ep(\d+),\s*(\d+),/.exec(line))) {
        getEp(m[1], m[2]).rewardItems += 1;
      }
      if ((m = /SetQuestGetItem\([^,]+,\s*Ep(\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*\d+,\s*\d+,\s*(\d+)/.exec(line))) {
        getEp(m[1], m[2]).items.push({ cat: num(m[3]), idx: num(m[4]), count: num(m[5]) });
      }
    }
  }

  return { quests, jobs, ini, iniRows, epQuests };
}

export function parseSuiteEventsAndInvasions(rootDir) {
  const summary = {};
  const extractStartTimes = (xmlPath) => {
    const starts = parseXmlTags(xmlPath, 'Start');
    return starts.length
      ? `${starts.length}x(${starts.slice(0, 3).map((s) => `${String(s.Hour ?? '?').padStart(2, '0')}:${String(s.Minute ?? '0').padStart(2, '0')}`).join(',')}${starts.length > 3 ? ',..' : ''})`
      : 'none';
  };
  const cfgFiles = [
    ['BloodCastle', 'BloodCastle.ini', 'BloodCastle.xml', 'BloodCastle.dat'],
    ['ChaosCastle', 'ChaosCastle.ini', 'ChaosCastle.xml', 'ChaosCastle.dat'],
    ['ChaosCastleSurvival', 'ChaosCastle_Survival.xml'],
    ['DevilSquare', 'DevilSquare.ini', 'DevilSquare_Classic.xml', 'DevilSquare.dat'],
    ['IllusionTemple', 'IllusionTemple_Classic.xml', 'IllusionTemple_Renewal.xml'],
    ['Kanturu', 'KanturuEvent.xml', 'Kanturu.dat'],
    ['Raklion', 'RaklionHatchery.xml', 'Raklion.dat'],
    ['CryWolf', 'CryWolf.xml'],
    ['LorenDeep', 'LorenDeep.xml'],
    ['BonusEvent', 'BonusEvent.xml'],
    ['GamblingLottery', 'GamblingLottery.xml'],
    ['GoldenInvasion', 'GoldenInvasion.xml', 'GoldenInvasion.ini'],
    ['InvasionManager', 'InvasionManager.xml'],
    ['ArcaBattle', 'ArcaBattle.xml'],
    ['AcheronGuardian', 'AcheronGuardian.xml'],
    ['DoppelGanger', 'DoppelGanger_Config.ini', 'DoppelGanger.ini'],
    ['ImperialGuardian', 'ImperialGuardian.ini'],
    ['LastManStanding', 'LastManStanding_Config.ini', 'LastManStanding.xml'],
    ['TormentedSquare', 'TormentedSquare.xml'],
    ['WhiteWizardAttack', 'WhiteWizardAttack.xml'],
    ['DragonEvent', 'DragonEvent.xml'],
    ['XMasAttack', 'XMasEvent.ini', 'XMasAttack.xml', 'XMasAttack.dat'],
    ['RingAttack', 'RingAttackEvent.dat'],
  ];

  for (const [name, ...candidates] of cfgFiles) {
    let found = null;
    for (const c of candidates) {
      found = findSuiteFile(rootDir, c);
      if (found) break;
    }
    if (!found) continue;
    const ext = path.extname(found).toLowerCase();
    const entry = {
      file: cleanName(found),
      kind: ext.slice(1),
      enable: 'n/a',
      minLevel: 'n/a',
      players: 'n/a',
      times: 'n/a',
      schedule: 'n/a',
      rewardsOrDrops: 'n/a',
    };

    if (name === 'BloodCastle') {
      const xml = findSuiteFile(rootDir, 'BloodCastle.xml');
      const ini = parseIni(findSuiteFile(rootDir, 'BloodCastle.ini')).main ?? {};
      const dat = parseSectionedTxt(findSuiteFile(rootDir, 'BloodCastle.dat'));
      if (xml) {
        const root = parseXmlTags(xml, 'BloodCastle')[0] ?? {};
        const t = parseXmlTags(xml, 'Time')[0] ?? {};
        const castles = parseXmlTags(xml, 'Castle');
        entry.enable = root.Enable ?? '1';
        entry.times = `enter=${num(t.ToOpen) * 60}s,play=${num(t.PlayDuration) * 60}s,exit=${num(t.ToClose) * 60}s`;
        entry.schedule = extractStartTimes(xml);
        entry.rewardsOrDrops = `Scroll=${root.ScrollofArchangelDropRate}/10000,Bone=${root.BloodBoneDropRate}/10000,BC1HP=${castles[0]?.StatueHP}/${castles[0]?.GateHP}`;
      } else {
        const s0 = dat.get(0)?.[0] ?? [];
        const s2 = dat.get(2)?.[0] ?? [];
        const s4 = dat.get(4) ?? [];
        entry.enable = ini.BloodCastleEvent ?? '1';
        entry.times = s0.length >= 3 ? `enter=${num(s0[0]) * 60}s,play=${num(s0[1]) * 60}s,exit=${num(s0[2]) * 60}s` : 'n/a';
        entry.schedule = s4.length ? `${s4.length}x(${s4[0][0]}:${s4[0][1]}..)` : 'n/a';
        entry.rewardsOrDrops = `Scroll=${ini.ScrollofArchangelDropRate}/10000,Bone=${ini.BloodBoneDropRate}/10000,BC1HP=${s2[1]}/${s2[2]}`;
      }
    } else if (name === 'ChaosCastle') {
      const xml = findSuiteFile(rootDir, 'ChaosCastle.xml');
      const ini = parseIni(findSuiteFile(rootDir, 'ChaosCastle.ini')).main ?? {};
      const dat = parseSectionedTxt(findSuiteFile(rootDir, 'ChaosCastle.dat'));
      if (xml) {
        const root = parseXmlTags(xml, 'ChaosCastle')[0] ?? {};
        const t = parseXmlTags(xml, 'Time')[0] ?? {};
        entry.enable = root.Enable ?? '1';
        entry.players = `min=${root.MinPlayer},maxObj=100`;
        entry.times = `enter=${num(t.StartIn) * 60}s,play=${num(t.Duration) * 60}s,exit=${num(t.TownSpawn) * 60}s`;
        entry.schedule = extractStartTimes(xml);
      } else {
        const s0 = dat.get(0)?.[0] ?? [];
        const s2 = dat.get(2) ?? [];
        entry.enable = '1';
        entry.players = `min=${ini.ChaosCastleStartUser ?? 2},maxObj=100`;
        entry.times = s0.length >= 3 ? `enter=${num(s0[0]) * 60}s,play=${num(s0[1]) * 60}s,exit=${num(s0[2]) * 60}s` : 'n/a';
        entry.schedule = s2.length ? `${s2.length}x(${s2[0][0]}:${s2[0][1]}..)` : 'n/a';
      }
    } else if (name === 'DevilSquare') {
      const xml = findSuiteFile(rootDir, 'DevilSquare_Classic.xml');
      const ini = parseIni(findSuiteFile(rootDir, 'DevilSquare.ini')).main ?? {};
      const dat = parseSectionedTxt(findSuiteFile(rootDir, 'DevilSquare.dat'));
      if (xml) {
        const root = parseXmlTags(xml, 'DevilSquare_Classic')[0] ?? {};
        const t = parseXmlTags(xml, 'Time')[0] ?? {};
        entry.enable = root.Enable ?? '1';
        entry.players = `max=${root.MaxPlayerPerRoom}`;
        entry.times = `enter=${num(t.ToOpen) * 60}s,play=${num(t.PlayDuration) * 60}s,close=${num(t.Close) * 60}s`;
        entry.schedule = extractStartTimes(xml);
        entry.rewardsOrDrops = `Eye=${root.DevilsEyeDropRate}/10000,Key=${root.DevilsKeyDropRate}/10000`;
      } else {
        const s0 = dat.get(0)?.[0] ?? [];
        const s1 = dat.get(1) ?? [];
        entry.enable = ini.DevilSquareEvent ?? '1';
        entry.players = `max=${ini.DevilSquareMaxPlayerPerRoom ?? 15}`;
        entry.times = s0.length >= 3 ? `enter=${num(s0[1]) * 60}s,play=${num(s0[2]) * 60}s,close=${num(s0[0]) * 60}s` : 'n/a';
        entry.schedule = s1.length ? `${s1.length} monster waves` : 'n/a';
        entry.rewardsOrDrops = `Eye=${ini.EyesOfDevilSquareDropRate}/10000,Key=${ini.KeyOfDevilSquareDropRate}/10000`;
      }
    } else if (name === 'IllusionTemple') {
      const cXml = findSuiteFile(rootDir, 'IllusionTemple_Classic.xml');
      const rXml = findSuiteFile(rootDir, 'IllusionTemple_Renewal.xml');
      if (cXml) {
        const root = parseXmlTags(cXml, 'IllusionTempleClassic')[0] ?? {};
        const t = parseXmlTags(cXml, 'Time')[0] ?? {};
        const kp = parseXmlTags(cXml, 'KillPoint')[0] ?? {};
        entry.enable = root.Enable ?? '1';
        entry.players = `min=${root.MinPlayer},max=10`;
        entry.times = `enter=${num(t.StartIn) * 60}s,wait=${num(t.WaitingArea) * 60}s,play=${num(t.Duration) * 60}s,exit=${num(t.TownSpawn) * 60}s`;
        entry.schedule = extractStartTimes(cXml);
        entry.rewardsOrDrops = `WinPts=${root.MaxMatchPoint},MinSkill=${root.MinUseSkillCount},KillPts=${kp.OpponentKill}/${kp.MonsterKill}`;
      } else if (rXml) {
        const root = parseXmlTags(rXml, 'IllusionTempleRenewal')[0] ?? {};
        const t = parseXmlTags(rXml, 'Time')[0] ?? {};
        entry.enable = root.Enable ?? '1';
        entry.players = `minUserInParty=${root.MinUserInParty}`;
        entry.times = `enter=${num(t.StartIn) * 60}s,wait=${num(t.WaitingArea) * 60}s,play=${num(t.Duration) * 60}s,exit=${num(t.TownSpawn) * 60}s`;
        entry.schedule = extractStartTimes(rXml);
      }
    } else if (name === 'DoppelGanger') {
      const ini = parseIni(found).Config ?? {};
      const luaPath = findSuiteFile(rootDir, 'DoppelGanger.lua');
      const lua = luaPath && fs.existsSync(luaPath) ? fs.readFileSync(luaPath, 'utf8') : '';
      const rdy = /LuaSetReadyTime\((\d+)\)/.exec(lua)?.[1];
      const ply = /LuaSetPlayTime\((\d+)\)/.exec(lua)?.[1];
      const end = /LuaSetEndTime\((\d+)\)/.exec(lua)?.[1];
      entry.enable = ini.DoppelGangerEvent ?? ini.Enable ?? '1';
      entry.times = `ready=${rdy ? `${Number(rdy) * 60}s` : '?'},play=${ply ? `${Number(ply) * 60}s` : '?'},end=${end ? `${Number(end) * 60}s` : '?'}`;
      entry.rewardsOrDrops = `MLExpRate=${ini.DPMasterLevelExpRate ?? '1.0'}`;
    } else if (name === 'ImperialGuardian') {
      const ini = parseIni(found).ImperialGuardian ?? {};
      entry.enable = ini.IGEventOn ?? ini.Enable ?? '1';
      entry.times = `standby=${ini.WaitPlayerTime ?? ini.StandByTime}s,round=${ini.TimeAttackTime ?? ini.RoundDurationTime}s,loot=${ini.LootTime}s`;
      entry.rewardsOrDrops = `MixZen=${ini.MixNeedZen ?? ini.CompleteSecromiconMixReqMoney}`;
    } else if (name === 'Kanturu') {
      const xml = findSuiteFile(rootDir, 'KanturuEvent.xml');
      if (xml) {
        const root = parseXmlTags(xml, 'KanturuEvent')[0] ?? {};
        const item = parseXmlTags(xml, 'Item')[0] ?? {};
        entry.enable = root.Enable ?? '1';
        entry.times = 'Standby=1200s,Maya=15+15+20m,Nightmare=20m,Tower=23h';
        entry.rewardsOrDrops = `MoonStoneDrop=${item.MoonStoneDropRate}/10000,JoHDrop=${item.JewelOfHarmonyDropRate}/10000`;
      } else {
        entry.enable = '1';
        entry.times = 'Standby=1200s,Maya=15+15+20m,Nightmare=20m,Tower=23h';
        entry.rewardsOrDrops = 'MoonStone(13:38),JoH';
      }
    } else if (name === 'CryWolf') {
      const root = parseXmlTags(found, 'CryWolf')[0] ?? {};
      const cycs = parseXmlTags(found, 'Cycle');
      const totalBattle = cycs.find((c) => c.State === '4')?.Seconds ?? '900';
      entry.enable = root.Enable ?? '1';
      entry.times = `notify1=600s,notify2=300s,ready=150s,battle=${totalBattle}s`;
      entry.schedule = extractStartTimes(found);
      entry.rewardsOrDrops = `RankS>=10001(1.8M EXP),Peace:+5%Chaos/90%HP`;
    } else if (name === 'Raklion') {
      if (ext === '.xml') {
        const root = parseXmlTags(found, 'RaklionEvent')[0] ?? {};
        const t = parseXmlTags(found, 'Time')[0] ?? {};
        const eggs = parseXmlTags(found, 'EggCount')[0] ?? {};
        entry.enable = root.Enable ?? '1';
        entry.times = `appear=${t.BossAppearance}s,close=${t.BossZoneClose}s,open=${t.EventStart}s`;
        entry.rewardsOrDrops = `Eggs=${eggs.Half}/${eggs.Max}`;
      } else {
        const secs = parseSectionedTxt(found);
        const s0 = secs.get(0)?.[0] ?? [];
        const s1 = secs.get(1)?.[0] ?? [];
        entry.enable = '1';
        entry.times = `appear=${s0[0]}s,close=${s0[1]}s,open=${s0[2]}s`;
        entry.rewardsOrDrops = `Eggs=${s1[0]}/${s1[1]}`;
      }
    } else if (ext === '.xml') {
      const firstTag = /<([A-Za-z0-9_]+)\s+([^>]*)>/.exec(
        fs.readFileSync(found, 'utf8').replace(/<!--[\s\S]*?-->/g, '').replace(/<\?xml[^>]*\?>/, ''),
      );
      const attrs = firstTag ? parseXmlTagsInText(`<X ${firstTag[2]}>`, 'X')[0] ?? {} : {};
      const t = parseXmlTags(found, 'Time')[0] ?? {};
      entry.enable = attrs.Enable ?? '1';
      entry.minLevel = attrs.MinLevel ?? 'n/a';
      entry.players = attrs.MinPlayer ?? attrs.MinUserToStart ?? attrs.MaxPlayer ?? 'n/a';
      entry.times = Object.keys(t).length ? Object.entries(t).map(([k, v]) => `${k}=${v}`).join(',') : 'n/a';
      entry.schedule = extractStartTimes(found);
      const rew = parseXmlTags(found, 'Reward')[0] ?? parseXmlTags(found, 'Event')[0] ?? {};
      entry.rewardsOrDrops = Object.keys(rew).length
        ? Object.entries(rew).slice(0, 4).map(([k, v]) => `${k}=${v}`).join(',')
        : 'see XML';
    } else if (ext === '.ini') {
      const ini = parseIni(found);
      const flat = Object.values(ini).reduce((acc, s) => Object.assign(acc, s), {});
      entry.enable = flat.Enabled ?? flat.EnableInvasion ?? flat.Enable ?? '1';
      entry.minLevel = flat.MinLevel ?? 'n/a';
      entry.players = flat.PVPMinPlayers ? `min=${flat.PVPMinPlayers},max=${flat.PVPMaxPlayers}` : 'n/a';
      entry.times = flat.Duration ? `reg=${flat.RegTime}m,play=${flat.Duration}m` : flat.XMasEventSantaBuffDuration ? `buff=${flat.XMasEventSantaBuffDuration}s` : 'n/a';
      entry.rewardsOrDrops = flat.XMasEventItemDropRate ? `drop=${flat.XMasEventItemDropRate}%` : flat.GoldenGoblin ? `regen=${flat.GoldenGoblin}m` : 'n/a';
    } else if (ext === '.dat') {
      const secs = parseSectionedTxt(found);
      const s0 = secs.get(0)?.[0] ?? [];
      entry.enable = '1';
      entry.times = s0.length ? `raw0=[${s0.join(',')}]` : 'n/a';
    }
    summary[name] = entry;
  }
  return summary;
}

export function parseSuiteItemBags(rootDir) {
  const xmlBags = findSuiteFilesInDir(rootDir, 'ItemBags', '.xml');
  const txtBags = findSuiteFilesInDir(rootDir, 'EventItemBags', '.txt');
  const bags = new Map();
  for (const f of xmlBags) {
    const base = path.basename(f, '.xml');
    const items = parseXmlTags(f, 'Item');
    const drops = parseXmlTags(f, 'DropAllow');
    const bagsCfg = parseXmlTags(f, 'BagConfig');
    bags.set(base, {
      format: 'xml',
      items: items.length,
      dropGroups: drops.length,
      moneyDrop: bagsCfg[0] ? num(bagsCfg[0].MoneyDrop) : 0,
      itemRate: bagsCfg[0] ? num(bagsCfg[0].ItemRate) : 0,
    });
  }
  for (const f of txtBags) {
    const base = path.basename(f, '.txt');
    const secs = parseSectionedTxt(f);
    let itemCount = 0;
    for (const [sec, rows] of secs) {
      if (sec >= 1) itemCount += rows.length;
    }
    bags.set(base, { format: 'txt', items: itemCount, dropGroups: secs.size, moneyDrop: 0, itemRate: 0 });
  }
  return bags;
}

function parseMasterSkillLua(rootDir) {
  const luaPath = findSuiteFile(rootDir, 'MasterSkillPoint.lua');
  const stidToFunc = new Map();
  const formulas = new Map();
  if (!luaPath || !fs.existsSync(luaPath)) return { stidToFunc, formulas };
  const raw = fs.readFileSync(luaPath, 'utf8');
  for (const m of raw.matchAll(/AddToMLSTable\(\s*(\d+)\s*,\s*'([^']+)'/g)) {
    stidToFunc.set(Number(m[1]), m[2]);
  }
  const fnRe = /function\s+(FN_Lua\w+)\s*\(\s*nPoint\s*\)([\s\S]*?)^\s*end\b/gm;
  let fm;
  while ((fm = fnRe.exec(raw)) !== null) {
    const fnName = fm[1];
    const body = fm[2];
    const ret = /local\s+nReturn\s*=\s*([^\r\n]+)/.exec(body);
    if (!ret) continue;
    const expr = ret[1].replace(/--.*$/, '').trim().replace(/\^/g, '**');
    formulas.set(fnName, expr);
  }
  return { stidToFunc, formulas };
}

export function evalMasterFormula(expr, level) {
  if (!expr) return null;
  try {
    const clean = expr.replace(/\bnPoint\b/g, String(level)).replace(/\blevel\b/g, String(level)).replace(/\^/g, '**');
    if (!/^[\d+\-*/().\s*eE]+$/.test(clean)) return null;
    const val = Function(`"use strict"; return (${clean});`)();
    return Number.isFinite(val) ? Number(val.toFixed(2)) : null;
  } catch {
    return null;
  }
}

export function parseSuiteMasterSkillTree(rootDir) {
  const skillListXml = parseXmlTags(findSuiteFile(rootDir, 'SkillList.xml'), 'Skill');
  const skillMeta = new Map();
  for (const s of skillListXml) {
    skillMeta.set(num(s.Index), {
      rank: num(s.Rank),
      stid: num(s.STID),
      reqMlPoint: num(s.ReqMLPoint),
      name: s.Name ?? '',
    });
  }
  const { stidToFunc, formulas } = parseMasterSkillLua(rootDir);
  const attachLua = (magicNumber) => {
    const meta = skillMeta.get(magicNumber) ?? { rank: 0, stid: 0, reqMlPoint: 0, name: '' };
    const fnName = stidToFunc.get(meta.stid) ?? '';
    const expr = formulas.get(fnName) ?? '';
    return {
      rank: meta.rank,
      stid: meta.stid,
      reqMlPoint: meta.reqMlPoint,
      skillName: meta.name,
      luaFn: fnName,
      expr,
      v1: evalMasterFormula(expr, 1),
      v10: evalMasterFormula(expr, 10),
      v20: evalMasterFormula(expr, 20),
    };
  };

  const xmlPath = findSuiteFile(rootDir, 'MasterSkillTree.xml');
  if (xmlPath) {
    const rows = [];
    const raw = fs.readFileSync(xmlPath, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
    const clsRe = /<Class\s+ID="(\d+)"[^>]*>([\s\S]*?)<\/Class>/gi;
    let cm;
    while ((cm = clsRe.exec(raw)) !== null) {
      const classId = num(cm[1]);
      const treeRe = /<Tree\s+Type="(\d+)"[^>]*>([\s\S]*?)<\/Tree>/gi;
      let tm;
      while ((tm = treeRe.exec(cm[2])) !== null) {
        const treeType = num(tm[1]);
        for (const attrs of parseXmlTagsInText(tm[2], 'Skill')) {
          const magicNumber = num(attrs.MagicNumber ?? attrs.Index);
          rows.push({
            key: `${classId}:${magicNumber}`,
            classId,
            tree: treeType,
            index: num(attrs.Index),
            magicNumber,
            maxPoint: num(attrs.MaxPoint),
            reqMinPoint: num(attrs.ReqMinPoint),
            parent1: num(attrs.ParentSkill1),
            parent2: num(attrs.ParentSkill2),
            name: attrs.Name ?? '',
            ...attachLua(magicNumber),
          });
        }
      }
    }
    return {
      format: 'xml',
      skillCount: rows.length,
      skills: new Map(rows.map((r) => [r.key, r])),
    };
  }

  const datPath = findSuiteFile(rootDir, 'MasterSkillTree.dat');
  if (datPath) {
    const lines = fs.readFileSync(datPath, 'utf8').split(/\r?\n/);
    const skills = new Map();
    for (const raw of lines) {
      const line = raw.replace(/\/\/.*$/, '').trim();
      if (!line || line.toLowerCase() === 'end' || line === '0') continue;
      const cols = line.split(/\s+/);
      if (cols.length < 8 || !/^\d+$/.test(cols[0])) continue;
      const index = num(cols[0]);
      const classId = num(cols[1]);
      const tree = num(cols[2]);
      const reqMinPoint = num(cols[3]);
      const maxPoint = num(cols[4]);
      const parent1 = num(cols[5]);
      const parent2 = num(cols[6]);
      const magicNumber = num(cols[7]);
      const key = `${classId}:${magicNumber}`;
      skills.set(key, {
        key,
        classId,
        tree,
        index,
        magicNumber,
        maxPoint,
        reqMinPoint,
        parent1,
        parent2,
        name: skillMeta.get(magicNumber)?.name ?? '',
        ...attachLua(magicNumber),
      });
    }
    return { format: 'dat', skillCount: skills.size, skills };
  }
  return { format: 'none', skillCount: 0, skills: new Map() };
}

function extractXmlBlock(filePath, tag) {
  if (!filePath || !fs.existsSync(filePath)) return '';
  const raw = fs.readFileSync(filePath, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
  const m = new RegExp(`<${tag}\\b[^>]*>([\\s\\S]*?)<\\/${tag}>`, 'i').exec(raw);
  return m ? m[1] : '';
}

function parseXmlTagsInText(xmlText, tagName) {
  const out = [];
  if (!xmlText) return out;
  const re = new RegExp(`<${tagName}\\b([^>]*)>`, 'gi');
  const attrRe = /([A-Za-z0-9_:-]+)="([^"]*)"/g;
  let m;
  while ((m = re.exec(xmlText)) !== null) {
    const attrs = {};
    let am;
    while ((am = attrRe.exec(m[1])) !== null) attrs[am[1]] = am[2];
    out.push(attrs);
  }
  return out;
}

export function parseSuiteCastleSiege(rootDir) {
  const csPath = findSuiteFile(rootDir, 'CastleSiege.xml');
  if (!csPath) return null;
  const commonCfg = parseKeyValCfg(findSuiteFile(rootDir, 'CommonServer.cfg'));
  const commonIni = parseIni(findSuiteFile(rootDir, 'Common.ini'));
  const event = parseXmlTags(csPath, 'CastleSiegeEvent')[0] ?? {};
  const dmg = parseXmlTags(csPath, 'DamageReduction')[0] ?? {};
  const cycleSettings = parseXmlTags(csPath, 'CycleSettings')[0] ?? {};
  const cycles = parseXmlTags(csPath, 'Cycle').map((c) => ({
    stage: num(c.Stage),
    day: num(c.Day),
    hour: num(c.Hour),
    minute: num(c.Minute),
  }));
  const npcs = parseXmlTags(csPath, 'NPC').map((n) => ({
    index: num(n.Index),
    number: num(n.Number),
    dbSave: num(n.DBSave),
    side: num(n.Side),
    maxHp: num(n.MaxHP),
    x: num(n.StartX),
    y: num(n.StartY),
    dir: num(n.Dir),
    name: n.Name ?? '',
  }));

  const defUpBlock = extractXmlBlock(csPath, 'DefenseUpgrade');
  const hpUpBlock = extractXmlBlock(csPath, 'HealthUpgrade');
  const recUpBlock = extractXmlBlock(csPath, 'StatueHealthRecoveryUpgrade');
  const defSetBlock = extractXmlBlock(csPath, 'DefenseSettings');

  const gateDefCosts = parseXmlTagsInText(extractXmlBlockFromText(defUpBlock, 'Gates'), 'Defense').map((d) => ({
    level: num(d.Level),
    jog: num(d.ReqJewelOfGuardian),
    zen: num(d.ReqZen),
  }));
  const statueDefCosts = parseXmlTagsInText(extractXmlBlockFromText(defUpBlock, 'Statues'), 'Defense').map((d) => ({
    level: num(d.Level),
    jog: num(d.ReqJewelOfGuardian),
    zen: num(d.ReqZen),
  }));
  const gateHpUpgrades = parseXmlTagsInText(extractXmlBlockFromText(hpUpBlock, 'Gates'), 'Health').map((h, i) => ({
    level: i + 1,
    value: num(h.Value),
    jog: num(h.ReqJewelOfGuardian),
    zen: num(h.ReqZen),
  }));
  const statueHpUpgrades = parseXmlTagsInText(extractXmlBlockFromText(hpUpBlock, 'Statues'), 'Health').map((h, i) => ({
    level: i + 1,
    value: num(h.Value),
    jog: num(h.ReqJewelOfGuardian),
    zen: num(h.ReqZen),
  }));
  const statueRegenUpgrades = parseXmlTagsInText(recUpBlock, 'Recovery').map((r) => ({
    level: num(r.Level),
    jog: num(r.ReqJewelOfGuardian),
    zen: num(r.ReqZen),
  }));
  const gateDefenseValues = parseXmlTagsInText(extractXmlBlockFromText(defSetBlock, 'Gates'), 'Defense').map((d) => ({
    level: num(d.Level),
    value: num(d.Value),
  }));
  const statueDefenseValues = parseXmlTagsInText(extractXmlBlockFromText(defSetBlock, 'Statues'), 'Defense').map((d) => ({
    level: num(d.Level),
    value: num(d.Value),
  }));

  const huntZoneSetDrop = num(commonCfg.IsDropSetItemInCastleHuntZone);
  const huntZoneSetDropRate = num(commonCfg.SetItemInCastleHuntZoneDropRate);
  const huntZoneSetDropMinLevel = num(commonCfg.SetItemInCastleHuntZoneDropLevel);
  const markOfLordDropRate = num(commonCfg.MarkOfTheLord);
  const ownerGuildDestroyLimit = num(
    commonIni.CastleSiege?.CastleOwnerGuildDestroyLimit ?? commonCfg.CastleOwnerGuildDestroyLimit ?? '1',
  );

  return {
    enable: num(event.Enable),
    friendlyFire: num(event.FriendlyFire),
    resetAccumulatedTimeAfterKill: num(event.ResetAccumulatedTimeAfterKill),
    lordMixDailyLimit: num(event.LordMixDailyLimit),
    towerWithoutAlliance: num(event.TowerEntryWithoutAlliance),
    damageReductionOpposite: num(dmg.OppositeTeam),
    damageReductionSelf: num(dmg.SelfTeam),
    cycleDays: num(cycleSettings.Days),
    cycles,
    npcs,
    gateDefCosts,
    statueDefCosts,
    gateHpUpgrades,
    statueHpUpgrades,
    statueRegenUpgrades,
    gateDefenseValues,
    statueDefenseValues,
    huntZoneSetDrop,
    huntZoneSetDropRate,
    huntZoneSetDropMinLevel,
    markOfLordDropRate,
    ownerGuildDestroyLimit,
  };
}

function extractXmlBlockFromText(xmlText, tag) {
  if (!xmlText) return '';
  const m = new RegExp(`<${tag}\\b[^>]*>([\\s\\S]*?)<\\/${tag}>`, 'i').exec(xmlText);
  return m ? m[1] : '';
}

export function parseSuiteGens(rootDir) {
  const xmlPath = findSuiteFile(rootDir, 'GensSystem.xml');
  if (xmlPath) {
    const main = parseXmlTags(xmlPath, 'GensSystem')[0] ?? {};
    const general = parseXmlTags(xmlPath, 'General')[0] ?? {};
    const rewardCfg = parseXmlTags(xmlPath, 'RewardSettings')[0] ?? {};
    const ranks = parseXmlTags(xmlPath, 'Gens').map((g) => ({
      rank: num(g.Class),
      reqContrib: num(g.ReqContributePoint),
      rankPos: num(g.RankingPosition),
      rewardCat: num(g.RewardItemCat),
      rewardIndex: num(g.RewardItemIndex),
      rewardCount: num(g.RewardItemCount),
      name: g.Name ?? '',
    }));
    const battleMaps = parseXmlTags(xmlPath, 'Map').map((m) => ({
      number: num(m.Number),
      enable: num(m.Enable),
      familyAccessType: num(m.FamilyAccessType),
    }));
    return {
      format: 'xml',
      initialContributePoint: num(main.InitialContributePoint),
      abuseWarning: num(main.AbusingPenaltyWarning),
      abuseLimit: num(main.AbusingPenaltyLimit),
      abuseResetMin: num(main.AbusingResetTime),
      pkBattleWarpZenMult: num(main.PkPenaltyAddNeedZenMapMove),
      battleZoneAddExp: Number(main.BattleZoneAddExperience ?? 1),
      mlBattleZoneAddExp: Number(main.ML_BattleZoneAddExperience ?? 1),
      damageReduction: num(main.DamageReduction),
      canSeeOppositeChat: num(general.CanSeeOppositeGensChat),
      canJoinPartyOpposite: num(general.CanJoinPartyWhileOppositeGens),
      canJoinGuildOpposite: num(general.CanJoinGuildWhileOppositeGens),
      canJoinUnionOpposite: num(general.CanJoinUnionWhileOppositeGens),
      canCreatePartyBattleZone: num(general.CanCreatePartyOnBattleZone),
      rewardStartDay: num(rewardCfg.StartDay),
      rewardEndDay: num(rewardCfg.EndDay),
      minRankToGainReward: num(rewardCfg.MinRankToGainReward),
      ranks,
      battleMaps,
    };
  }
  const iniPath = findSuiteFile(rootDir, 'GensSystem.ini');
  const rankPath = findSuiteFile(rootDir, 'GensRanking.dat');
  const ini = parseIni(iniPath);
  const g = ini.Gens ?? ini.GensSystem ?? ini.General ?? {};
  const ranks = [];
  if (rankPath && fs.existsSync(rankPath)) {
    for (const raw of fs.readFileSync(rankPath, 'utf8').split(/\r?\n/)) {
      const line = raw.replace(/\/\/.*$/, '').trim();
      if (!line || line.toLowerCase() === 'end') continue;
      const cols = line.split(/\s+/);
      if (cols.length >= 6 && /^\d+$/.test(cols[0])) {
        ranks.push({
          rank: num(cols[0]),
          reqContrib: num(cols[1]),
          rankPos: num(cols[2]),
          rewardCat: num(cols[3]),
          rewardIndex: num(cols[4]),
          rewardCount: num(cols[5]),
          name: '',
        });
      }
    }
  }
  return {
    format: 'ini+dat',
    initialContributePoint: num(g.GensInitialContributePoint),
    abuseWarning: num(g.GensAbusingPenaltyWarning),
    abuseLimit: num(g.GensAbusingPenaltyLimit),
    abuseResetMin: num(g.GensAbusingResetTime),
    pkBattleWarpZenMult: num(g.GensPkPenaltyAddNeedZenMapMove),
    battleZoneAddExp: 1,
    mlBattleZoneAddExp: 1,
    damageReduction: 100,
    canSeeOppositeChat: num(g.GensCanSeeOppositeGensChat),
    canJoinPartyOpposite: num(g.GensCanJoinPartyWhileOppositeGens),
    canJoinGuildOpposite: num(g.GensCanJoinGuildWhileOppositeGens),
    canJoinUnionOpposite: num(g.GensCanJoinUnionWhileOppositeGens),
    canCreatePartyBattleZone: num(g.GensCanCreatePartyOnBattleZone),
    rewardStartDay: 1,
    rewardEndDay: 7,
    minRankToGainReward: ranks.filter((r) => r.rewardCount > 0).reduce((m, r) => Math.max(m, r.rankPos), 0),
    ranks,
    battleMaps: [{ number: 63, enable: num(g.GensBattlezonesFromMuBlue, 0), familyAccessType: 0 }],
  };
}

export function parseSuiteCommonAndLimits(rootDir) {
  const commonCfg = parseKeyValCfg(findSuiteFile(rootDir, 'CommonServer.cfg'));
  const commonIni = parseIni(findSuiteFile(rootDir, 'Common.ini'));
  const petIni = parseIni(findSuiteFile(rootDir, 'PetSettings.ini'));
  const priceIni = parseIni(findSuiteFile(rootDir, 'PriceSettings.ini'));
  const socketIni = parseIni(findSuiteFile(rootDir, 'SocketOptionSettings.ini'));
  const vipXml = findSuiteFile(rootDir, 'VipSettings.xml');
  const vipTypes = parseXmlTags(vipXml, 'VipType');
  return {
    commonCfg,
    commonIni,
    petIni,
    priceIni,
    socketIni,
    vipCount: vipTypes.length,
  };
}
