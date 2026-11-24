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
