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
  const quests = parseXmlTags(classQuestPath, 'QuestInfo').map((q) => ({
    index: num(q.Index),
    type: num(q.Type),
    subType: num(q.SubType),
    name: q.Name ?? '',
  }));
  const jobs = parseXmlTags(classQuestPath, 'Job').map((j) => ({
    type: num(j.Type),
    reqItemCat: num(j.ReqItemCat),
    reqItemIndex: num(j.ReqItemIndex),
    itemCount: num(j.ItemCount),
    minLevel: num(j.MonsterMinLevel),
    maxLevel: num(j.MonsterMaxLevel),
    dropRate: num(j.DropRate),
    rewardType: num(j.RewardType),
    rewardCount: num(j.RewardCount),
  }));
  const ini = parseIni(questIniPath);
  return { quests, jobs, ini };
}

export function parseSuiteEventsAndInvasions(rootDir) {
  const summary = {};
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
    if (ext === '.xml') {
      const tags = parseXmlTags(found, 'Setting');
      const entries = parseXmlTags(found, 'Event')
        .concat(parseXmlTags(found, 'Invasion'))
        .concat(parseXmlTags(found, 'Schedule'))
        .concat(parseXmlTags(found, 'Start'));
      summary[name] = { kind: 'xml', file: cleanName(found), settings: tags.length, entries: entries.length, sample: entries.slice(0, 3) };
    } else if (ext === '.ini') {
      const ini = parseIni(found);
      const keys = Object.values(ini).reduce((acc, s) => acc + Object.keys(s).length, 0);
      summary[name] = { kind: 'ini', file: cleanName(found), sections: Object.keys(ini).length, keys };
    } else {
      const secs = parseSectionedTxt(found);
      let rows = 0;
      for (const arr of secs.values()) rows += arr.length;
      summary[name] = { kind: 'dat', file: cleanName(found), sections: secs.size, rows };
    }
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

export function parseSuiteMasterSkillTree(rootDir) {
  const xmlPath = findSuiteFile(rootDir, 'MasterSkillTree.xml');
  if (xmlPath) {
    const classes = parseXmlSections(xmlPath, 'Class', 'ID', 'Skill');
    return {
      format: 'xml',
      skillCount: classes.length,
      skills: new Map(
        classes.map((c) => [
          `${c.section}:${c.attrs.MagicNumber ?? c.attrs.Index}`,
          {
            classId: num(c.section),
            index: num(c.attrs.Index),
            magicNumber: num(c.attrs.MagicNumber),
            maxPoint: num(c.attrs.MaxPoint),
            reqMinPoint: num(c.attrs.ReqMinPoint),
            rank: num(c.attrs.Rank),
            parent1: num(c.attrs.ParentSkill1),
            parent2: num(c.attrs.ParentSkill2),
          },
        ]),
      ),
    };
  }
  const datPath = findSuiteFile(rootDir, 'MasterSkillTree.dat');
  if (datPath) {
    const lines = fs.readFileSync(datPath, 'utf8').split(/\r?\n/);
    const skills = new Map();
    for (const raw of lines) {
      const line = raw.replace(/\/\/.*$/, '').trim();
      if (!line || line.toLowerCase() === 'end') continue;
      const cols = line.split(/\s+/);
      if (cols.length < 10 || !/^\d+$/.test(cols[0])) continue;
      const classId = num(cols[1]);
      const magicNumber = num(cols[9]);
      skills.set(`${classId}:${magicNumber}`, {
        classId,
        index: num(cols[0]),
        magicNumber,
        maxPoint: num(cols[4]),
        reqMinPoint: num(cols[3]),
        rank: num(cols[2]),
        parent1: num(cols[7]),
        parent2: num(cols[8]),
      });
    }
    return { format: 'dat', skillCount: skills.size, skills };
  }
  return { format: 'none', skillCount: 0, skills: new Map() };
}

export function parseSuiteCastleSiege(rootDir) {
  const csPath = findSuiteFile(rootDir, 'CastleSiege.xml');
  if (!csPath) return null;
  const event = parseXmlTags(csPath, 'CastleSiegeEvent')[0] ?? {};
  const cycles = parseXmlTags(csPath, 'Cycle');
  const states = parseXmlTags(csPath, 'State');
  const npcs = parseXmlTags(csPath, 'NPC');
  return {
    enable: num(event.Enable),
    friendlyFire: num(event.FriendlyFire),
    lordMixDailyLimit: num(event.LordMixDailyLimit),
    towerWithoutAlliance: num(event.TowerEntryWithoutAlliance),
    cycleDays: cycles.map((c) => num(c.Days)),
    stateCount: states.length,
    npcCount: npcs.length,
  };
}

export function parseSuiteGens(rootDir) {
  const xmlPath = findSuiteFile(rootDir, 'GensSystem.xml');
  if (xmlPath) {
    const main = parseXmlTags(xmlPath, 'GensSystem')[0] ?? {};
    const ranks = parseXmlTags(xmlPath, 'Rank');
    const rewards = parseXmlTags(xmlPath, 'Reward');
    return {
      format: 'xml',
      blueMuMode: num(main.BlueMuMode),
      minLevel: num(main.MinLevel),
      minReset: num(main.MinReset),
      rankCount: ranks.length,
      rewardCount: rewards.length,
    };
  }
  const iniPath = findSuiteFile(rootDir, 'GensSystem.ini');
  const rankPath = findSuiteFile(rootDir, 'GensRanking.dat');
  const ini = parseIni(iniPath);
  const rankSecs = parseSectionedTxt(rankPath);
  let rankCount = 0;
  for (const rows of rankSecs.values()) rankCount += rows.length;
  return {
    format: 'ini+dat',
    sections: Object.keys(ini).length,
    rankCount,
    general: ini.GensSystem ?? ini.General ?? {},
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
