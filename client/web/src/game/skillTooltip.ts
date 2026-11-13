// The skill tooltip of the skill bar and popup, as the desktop builds it
// (UI::Skills::Tooltip::BuildModel): the name, range, mana, AG and cooldown, the requirements (red
// with what's lacking when not met), the notes and the skill's element. The damage lines come with
// the character's damage (the character window step).

import type { TooltipLine } from './itemTooltip.js';
import { requiredEnergy, skillInfo, SkillNumber, SkillUseType, type SkillStats } from './skills.js';

const line = (text: string, color = 'white', bold = false): TooltipLine =>
  color === 'darkRed' ? { text, color: 'white', bold, background: 'darkRed', spacer: false } : { text, color, bold, background: null, spacer: false };
const blank = (): TooltipLine => ({ text: '', color: 'white', bold: false, background: null, spacer: true });

// Localization/Game.en.resx
const MASTERY = ['Ice attribute', 'Poison attribute', 'Lightning attribute', 'Fire attribute', 'Earth attribute', 'Wind attribute', 'Water attribute'];
const COMBO_SKILLS = new Set([19, 20, 21, 22, 23, 41, 42, 43]); // Falling Slash … Slash, Twisting Slash, Rageful Blow, Death Stab
// IsCastleSiegeOnlySkill: Rush, Javelin, Deep Impact, Spiral Slash, Death Cannon, Space Split, Occupy.
const CASTLE_SIEGE_ONLY = new Set([44, 45, 46, SkillNumber.SPIRAL_SLASH, 73, 74, 269]);
const IMPALE = 47, PARTY_TELEPORT = 63;

export function skillTooltipLines(skill: number, stats: SkillStats, classIndex: number, inParty = false): TooltipLine[] {
  const data = skillInfo(skill);
  if (!data || !data.name) return [];
  const lines: TooltipLine[] = [];
  const section = (build: () => void) => { const before = lines.length; build(); if (lines.length > before) lines.push(blank()); };
  lines.push(blank(), line(data.name, 'blue', true), blank());
  // EmitBodyStats
  section(() => {
    if (skill !== SkillNumber.EXPANSION_OF_WIZARDRY && data.distance) lines.push(line(`Range: ${data.distance}`));
    lines.push(line(`Mana: ${data.mana}`));
    if (data.ability > 0) lines.push(line(`AG Usage: ${data.ability}`));
    if (data.delay > 0) lines.push(line(`Cooldown: ${(data.delay / 1000).toFixed(1)} sec`));
  });
  // EmitRequirements
  section(() => {
    const requirement = (required: number, current: number, text: string) => {
      if (required <= 0) return;
      const met = current >= required;
      lines.push(line(text.replace('%d', String(required)), met ? 'white' : 'red'));
      if (!met) lines.push(line(`(lacking ${required - current})`, 'red'));
    };
    requirement(data.level, stats.level, 'Minimum Level Requirement: %d');
    requirement(data.strength, stats.strength, 'Strength Requirement: %d');
    requirement(data.dexterity, stats.agility, 'Agility Requirement: %d');
    requirement(requiredEnergy(skill, classIndex), stats.energy, 'Energy Requirement: %d');
    requirement(data.leadership, stats.leadership, 'Charisma Requirement: %d');
  });
  // EmitBottomBanners
  section(() => {
    if (classIndex === 1 && skill === IMPALE) lines.push(line('Can only be used in moving unit', 'darkRed'));
    // The combo note needs the knight's second class extension at level 220.
    if (classIndex === 1 && stats.level >= 220 && COMBO_SKILLS.has(skill)) lines.push(line('Combo', 'darkRed'));
    else if (classIndex === 1 && stats.level >= 220 && skill === SkillNumber.STRIKE_OF_DESTRUCTION) lines.push(line('Combination available(2 step only)', 'darkRed'));
    if (data.useType === SkillUseType.BRAND) {
      const brand = skillInfo(data.brand);
      lines.push(line(`Receive skill from ${brand?.name ?? ''}`, 'darkRed'), line(`Can only be used for ${brand?.damage ?? 0} seconds`, 'darkRed'));
    }
    if (data.useType === SkillUseType.MASTER) {
      lines.push(line('This is a master skill in Guild Battle and Castle Siege', 'darkRed'), line(`Can be used when the ${data.killCount} KillCount is completed`, 'darkRed'));
    }
    if (classIndex === 4 && skill === PARTY_TELEPORT && !inParty) lines.push(line('Can only be used during party', 'darkRed'));
    if (skill === SkillNumber.PLASMA_STORM_FENRIR) {
      lines.push(line('When the attack is successful it will decrease the durability of', 'darkRed'), line('one of the certain weapons to 50%.', 'darkRed'));
    }
    if (CASTLE_SIEGE_ONLY.has(skill)) lines.push(line('Only in Castle Siege', 'darkRed'));
    if (skill >= SkillNumber.STUN && skill <= SkillNumber.REMOVAL_BUFF && skill !== 69) lines.push(line('Can be used during Castle Siege with required Kill Count', 'darkRed'));
    if (skill === IMPALE) lines.push(line('Can be used from the mount item', 'darkRed'));
  });
  // EmitBlueTags
  section(() => {
    if (data.masteryType < MASTERY.length) lines.push(line(MASTERY[data.masteryType], 'blue'));
  });
  return lines;
}
