// Normalized parsers for Season 6 and Season 9 server suite tables.
// Paths resolve dynamically from LEGACY_S6_DIR / LEGACY_S9_DIR.

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

export function parseSuiteItems(rootDir) {
  const file = findSuiteFile(rootDir, 'ItemList.xml');
  if (!file) return new Map();
  const rows = parseXmlSections(file, 'Section', 'Index', 'Item');
  const map = new Map();
  for (const { section, attrs } of rows) {
    const group = num(section);
    const number = num(attrs.Index);
    const classes = [];
    if (num(attrs.DarkWizard) > 0) classes.push(0);
    if (num(attrs.DarkKnight) > 0) classes.push(2);
    if (num(attrs.FairyElf) > 0) classes.push(4);
    if (num(attrs.MagicGladiator) > 0) classes.push(6);
    if (num(attrs.DarkLord) > 0) classes.push(8);
    if (num(attrs.Summoner) > 0) classes.push(10);
    if (num(attrs.RageFighter) > 0) classes.push(12);
    map.set(`${group}-${number}`, {
      group,
      number,
      name: attrs.Name ?? '',
      slot: num(attrs.Slot, -1),
      skillIndex: num(attrs.SkillIndex),
      twoHand: num(attrs.TwoHand),
      width: num(attrs.Width),
      height: num(attrs.Height),
      drop: num(attrs.Drop),
      dropLevel: num(attrs.DropLevel),
      dmgMin: num(attrs.DamageMin),
      dmgMax: num(attrs.DamageMax),
      attackSpeed: num(attrs.AttackSpeed),
      durability: num(attrs.Durability),
      magicPower: num(attrs.MagicPower),
      reqLevel: num(attrs.ReqLevel),
      reqStr: num(attrs.ReqStrength),
      reqDex: num(attrs.ReqDexterity),
      reqEne: num(attrs.ReqEnergy),
      reqVit: num(attrs.ReqVitality),
      reqCmd: num(attrs.ReqCommand),
      tradable: num(attrs.Transaction),
      personalStore: num(attrs.PersonalStore),
      storable: num(attrs.StoreWarehouse),
      sellNpc: num(attrs.SellToNPC),
      repairable: num(attrs.Repair),
      darkWizard: num(attrs.DarkWizard),
      darkKnight: num(attrs.DarkKnight),
      fairyElf: num(attrs.FairyElf),
      magicGladiator: num(attrs.MagicGladiator),
      darkLord: num(attrs.DarkLord),
      summoner: num(attrs.Summoner),
      rageFighter: num(attrs.RageFighter),
      classesCount: classes.length,
    });
  }
  return map;
}

export function parseSuiteSkills(rootDir) {
  const file = findSuiteFile(rootDir, 'SkillList.xml');
  if (!file) return new Map();
  const rows = parseXmlTags(file, 'Skill');
  const map = new Map();
  for (const a of rows) {
    const id = num(a.Index);
    map.set(id, {
      number: id,
      name: a.Name ?? '',
      reqLevel: num(a.ReqLevel),
      damage: num(a.Damage),
      mana: num(a.ManaUsage),
      bp: num(a.BPUsage),
      distance: num(a.Distance),
      delay: num(a.Delay),
      reqEnergy: num(a.ReqEnergy),
      reqStrength: num(a.ReqStrength),
      reqCommand: num(a.ReqCommand),
      attribute: num(a.Attribute, -1),
      type: num(a.Type),
      useType: num(a.UseType),
      itemSkill: num(a.ItemSkill),
      dw: num(a.DarkWizard),
      dk: num(a.DarkKnight),
      elf: num(a.FairyElf),
      mg: num(a.MagicGladiator),
      dl: num(a.DarkLord),
      sum: num(a.Summoner),
      rf: num(a.RageFighter),
    });
  }
  return map;
}

export function parseSuiteMonsters(rootDir) {
  const file = findSuiteFile(rootDir, 'MonsterList.xml');
  if (!file) return new Map();
  const rows = parseXmlTags(file, 'Monster');
  const map = new Map();
  for (const a of rows) {
    const id = num(a.Index);
    map.set(id, {
      number: id,
      name: a.Name ?? '',
      level: num(a.Level),
      hp: num(a.HP),
      dmgMin: num(a.DamageMin),
      dmgMax: num(a.DamageMax),
      defense: num(a.Defense),
      magicDefense: num(a.MagicDefense),
      attackRate: num(a.AttackRate),
      blockRate: num(a.BlockRate),
      moveRange: num(a.MoveRange),
      attackType: num(a.AttackType),
      attackRange: num(a.AttackRange),
      viewRange: num(a.ViewRange),
      moveSpeed: num(a.MoveSpeed),
      attackSpeed: num(a.AttackSpeed),
      regenTime: num(a.RegenTime),
      iceRes: num(a.IceRes),
      poisonRes: num(a.PoisonRes),
      lightRes: num(a.LightRes),
      fireRes: num(a.FireRes),
    });
  }
  return map;
}

export function parseSuiteSpawns(rootDir) {
  const counts = new Map(); // key: `${map}:${monster}` -> qty
  const add = (mapNo, mobNo, qty) => {
    const key = `${mapNo}:${mobNo}`;
    counts.set(key, (counts.get(key) ?? 0) + qty);
  };

  const xmlFile = findSuiteFile(rootDir, 'MonsterSpawn.xml');
  if (xmlFile) {
    const xml = fs.readFileSync(xmlFile, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
    const mapRe = /<Map\b([^>]*)>([\s\S]*?)<\/Map>/g;
    const spotRe = /<Spot\b([^>]*)>([\s\S]*?)<\/Spot>/g;
    const spawnRe = /<Spawn\s+([^>]*?)\s*\/?>/g;
    for (const mm of xml.matchAll(mapRe)) {
      const mNo = num((/Number="([^"]*)"/.exec(mm[1]) ?? [])[1]);
      for (const sm of mm[2].matchAll(spotRe)) {
        const sType = num((/Type="([^"]*)"/.exec(sm[1]) ?? [])[1]);
        for (const sp of sm[2].matchAll(spawnRe)) {
          const mob = num((/Index="([^"]*)"/.exec(sp[1]) ?? [])[1]);
          const count = num((/Count="([^"]*)"/.exec(sp[1]) ?? [])[1], 1);
          add(mNo, mob, sType === 0 ? 1 : count);
        }
      }
    }
    return counts;
  }

  for (const name of ['MonsterSetBase.txt', 'MonsterSetBase_CS.txt']) {
    const txtFile = findSuiteFile(rootDir, name, 'spawns');
    if (!txtFile) continue;
    const secMap = parseSectionedTxt(txtFile);
    for (const [sec, lines] of secMap.entries()) {
      for (const t of lines) {
        const mob = num(t[0]);
        const mapNo = num(t[1]);
        if (sec === 0 || sec === 3) add(mapNo, mob, 1);
        else if (sec === 1 || sec === 2 || sec === 4) {
          const qty = sec === 1 ? num(t[8], 1) : 1;
          add(mapNo, mob, qty);
        }
      }
    }
  }
  return counts;
}

export function parseSuiteGatesAndWarps(rootDir) {
  const gatesFile = findSuiteFile(rootDir, 'GateSettings.xml');
  const warpsFile = findSuiteFile(rootDir, 'MoveReq.xml');
  const mapsFile = findSuiteFile(rootDir, 'MapList.xml');
  const gates = new Map();
  if (gatesFile) {
    for (const a of parseXmlTags(gatesFile, 'Gate')) {
      gates.set(num(a.Index), {
        index: num(a.Index),
        flag: num(a.Flag),
        map: num(a.MapNumber),
        x1: num(a.StartX),
        y1: num(a.StartY),
        x2: num(a.EndX),
        y2: num(a.EndY),
        target: num(a.Target),
        direction: num(a.Direction),
        minLevel: num(a.MinLevel),
        name: a.Name ?? '',
      });
    }
  }
  const warps = [];
  if (warpsFile) {
    for (const a of parseXmlTags(warpsFile, 'Warp')) {
      warps.push({
        index: num(a.Index),
        name: a.ServerName ?? a.ClientName ?? '',
        minLevel: num(a.MinLevel),
        maxLevel: num(a.MaxLevel),
        zen: num(a.ReqMoney),
        gate: num(a.GateNumber),
      });
    }
  }
  const maps = new Map();
  if (mapsFile) {
    for (const a of parseXmlTags(mapsFile, 'Map')) {
      maps.set(num(a.Number), a.Name ?? '');
    }
  }
  return { gates, warps, maps };
}

export function parseSuiteShops(rootDir) {
  const shopListFile = findSuiteFile(rootDir, 'ShopList.xml');
  const shopFiles = findSuiteFilesInDir(rootDir, 'Shops', '.xml');
  const byFile = new Map(shopFiles.map((f) => [path.basename(f).toLowerCase(), f]));
  const npcs = new Map();
  if (shopListFile) {
    for (const a of parseXmlTags(shopListFile, 'Shop')) {
      const npc = num(a.NPCIndex);
      const fname = (a.FileName ?? '').toLowerCase();
      const full = byFile.get(fname);
      const items = full ? parseXmlTags(full, 'Item').map((it) => ({
        group: num(it.Cat),
        number: num(it.Index),
        level: num(it.Level),
        durability: num(it.Durability),
        skill: num(it.Skill),
        luck: num(it.Luck),
        option: num(it.Option),
        exc: num(it.Exc),
      })) : [];
      npcs.set(`${npc}:${a.MapNumber}`, {
        npc,
        map: num(a.MapNumber),
        x: num(a.PosX),
        y: num(a.PosY),
        gmShop: num(a.GMShop),
        fileName: a.FileName ?? '',
        items,
      });
    }
  }
  return npcs;
}

export function parseSuiteMixes(rootDir) {
  const chaosIni = findSuiteFile(rootDir, 'ChaosBox.ini');
  const socketItemXml = findSuiteFile(rootDir, 'SocketItem.xml');
  const ini = parseIni(chaosIni);
  const socketWeaponMix = socketItemXml ? parseXmlTags(socketItemXml, 'SocketWeaponMix') : [];
  return { ini, socketWeaponMix };
}

export function parseSuiteSocketOptions(rootDir) {
  const xmlFile = findSuiteFile(rootDir, 'SocketOption.xml');
  const iniFile = findSuiteFile(rootDir, 'SocketOptionSettings.ini');
  const options = new Map();
  if (xmlFile) {
    for (const { attrs: a } of parseXmlSections(xmlFile, 'SocketItemOptionSettings', null, 'Option')) {
      options.set(num(a.Index), {
        index: num(a.Index),
        element: num(a.ElementType),
        level: num(a.Level),
        bonusType: num(a.BonusType),
        name: a.Name ?? '',
        values: [num(a.BonusValue1), num(a.BonusValue2), num(a.BonusValue3), num(a.BonusValue4), num(a.BonusValue5)],
      });
    }
    return options;
  }
  if (iniFile) {
    const secs = parseSectionedTxt(iniFile);
    const list = secs.get(0) ?? [];
    for (const t of list) {
      options.set(num(t[0]), {
        index: num(t[0]),
        element: num(t[1]),
        level: num(t[2]),
        name: t[3] ?? '',
        bonusType: num(t[4]),
        values: [num(t[5]), num(t[6]), num(t[7]), num(t[8]), num(t[9])],
      });
    }
  }
  return options;
}

export function parseSuiteItemTables(rootDir) {
  const harmony = parseSectionedTxt(findSuiteFile(rootDir, 'HarmonyItemOption.ini'));
  const excellent = parseXmlTags(findSuiteFile(rootDir, 'ExcellentOptions.xml'), 'Option');
  const ancientSets = parseXmlTags(findSuiteFile(rootDir, 'ItemSetOption.xml'), 'SetItem');
  const prices = parseIni(findSuiteFile(rootDir, 'PriceSettings.ini'));
  return { harmony, excellent, ancientSets, prices };
}

export function parseSuiteExpAndEvents(rootDir) {
  const expFile = findSuiteFile(rootDir, 'ExpSystem.xml');
  const commonFile = findSuiteFile(rootDir, 'Common.ini');
  const bcFile = findSuiteFile(rootDir, 'BloodCastle.xml') ?? findSuiteFile(rootDir, 'BloodCastle.dat');
  const dsFile = findSuiteFile(rootDir, 'DevilSquare.xml') ?? findSuiteFile(rootDir, 'DevilSquare.dat');
  const ccFile = findSuiteFile(rootDir, 'ChaosCastle.xml') ?? findSuiteFile(rootDir, 'ChaosCastle.dat');
  return {
    expSettings: expFile ? fs.readFileSync(expFile, 'utf8').match(/<StaticExp[^>]*\/>/g) ?? [] : [],
    common: parseIni(commonFile),
    bcTags: bcFile && bcFile.endsWith('.xml') ? parseXmlTags(bcFile, 'Time') : [],
    dsTags: dsFile && dsFile.endsWith('.xml') ? parseXmlTags(dsFile, 'Time') : [],
    ccTags: ccFile && ccFile.endsWith('.xml') ? parseXmlTags(ccFile, 'Time') : [],
  };
}
