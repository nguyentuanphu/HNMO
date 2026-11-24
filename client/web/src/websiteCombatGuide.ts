import { currentLanguage, onLanguageChange } from './i18n.js';

type Locale = 'en' | 'vi';
type LocalName = Record<Locale, string>;
type CombatGuide = { enabled: false } | {
  enabled: true;
  hit: {
    multiplier: number;
    emptyShare: number;
    duelFactor: number;
    penalties: { levelGap: number; penalty: number }[];
    example: { attackRate: number; defenseRate: number; attackerLevel: number; defenderLevel: number; chance: number };
  };
  servers: CombatServer[];
};

type CombatServer = {
  id: number;
  name: string;
  loot: {
    splitExpByDamage: boolean;
    dropToTopDamageDealer: boolean;
    example: { firstDamage: number; finishingDamage: number; firstShare: number; finishingShare: number };
  };
  limits: { damageCap: number; pvpReduction: number; siegeMapReduction: number } | null;
  bosses: {
    name: LocalName;
    partyRank: boolean;
    topAlsoGetParticipation: boolean;
    participationMinimumPercent: number;
    participationMinimumDamage: number;
    hasParticipation: boolean;
    hasLastHit: boolean;
    topRanks: number[];
  }[];
  moreBosses: boolean;
};

const panel = document.querySelector<HTMLElement>('#combat-guide');
const link = document.querySelector<HTMLAnchorElement>('[data-combat-guide-link]');
let guide: CombatGuide | undefined;
let selectedServer: number | undefined;
let failed = false;

function render(): void {
  if (!panel || !link) return;
  const vi = currentLanguage() === 'vi';
  const locale = vi ? 'vi' : 'en';
  const label = (en: string, translated: string) => vi ? translated : en;
  const number = new Intl.NumberFormat(vi ? 'vi-VN' : 'en-AU', { maximumFractionDigits: 2 });
  const percent = new Intl.NumberFormat(vi ? 'vi-VN' : 'en-AU', { style: 'percent', maximumFractionDigits: 2 });
  const text = (tag: string, content: string, parent: HTMLElement = panel) => {
    const node = document.createElement(tag);
    node.textContent = content;
    parent.append(node);
    return node;
  };

  panel.hidden = link.hidden = !failed && !guide?.enabled;
  if (panel.hidden) return;
  panel.replaceChildren();
  const heading = label('PvP and loot rules', 'Quy tắc PvP và chiến lợi phẩm');
  link.textContent = heading;
  text('h2', heading);
  if (!guide?.enabled) {
    text('p', label('Combat rules are temporarily unavailable.', 'Quy tắc chiến đấu tạm thời không có.'));
    return;
  }

  const server = guide.servers.find(candidate => candidate.id === selectedServer) ?? guide.servers[0];
  if (!server) return;
  const serverLabel = text('label', label('Game server', 'Máy chủ'));
  const picker = document.createElement('select');
  picker.id = 'combat-guide-server';
  serverLabel.setAttribute('for', picker.id);
  for (const candidate of guide.servers) {
    picker.add(new Option(candidate.name || label(`Server ${candidate.id}`, `Máy chủ ${candidate.id}`), String(candidate.id)));
  }
  picker.value = String(server.id);
  picker.addEventListener('change', () => {
    selectedServer = Number(picker.value);
    render();
    document.getElementById(picker.id)?.focus();
  });
  panel.append(picker);

  const { hit } = guide;
  text('h3', label('Chance to hit another player', 'Xác suất đánh trúng người chơi'));
  text('p', label(
    `Multiply ${number.format(hit.multiplier)} by the attacker's share of the combined PvP attack and defense rates and by the attacker's share of the combined total levels (including master levels).`,
    `Nhân ${number.format(hit.multiplier)} với tỷ lệ chỉ số tấn công PvP trong tổng chỉ số tấn công và phòng thủ PvP, rồi nhân với tỷ lệ cấp của người tấn công trong tổng cấp hai bên (gồm cả cấp Master).`,
  ));
  text('p', label(
    `If either sum is zero, use ${percent.format(hit.emptyShare)} for that share. Subtract the highest applicable penalty and keep the result between ${percent.format(0)} and ${percent.format(1)}.`,
    `Nếu một tổng bằng không, dùng ${percent.format(hit.emptyShare)} cho tỷ lệ đó. Trừ mức phạt cao nhất áp dụng và giới hạn kết quả từ ${percent.format(0)} đến ${percent.format(1)}.`,
  ));
  const penalties = text('ul', '');
  for (const entry of [...hit.penalties].reverse()) {
    const levelGap = number.format(entry.levelGap);
    const points = number.format(entry.penalty * 100);
    text('li', label(`Defender at least ${levelGap} levels higher · ${points} percentage points off`, `Bên phòng thủ cao hơn ít nhất ${levelGap} cấp · trừ ${points} điểm phần trăm`), penalties);
  }
  const { example } = hit;
  text('p', label(
    `Fictional example: attack ${number.format(example.attackRate)} at level ${number.format(example.attackerLevel)} against defense ${number.format(example.defenseRate)} at level ${number.format(example.defenderLevel)} gives ${percent.format(example.chance)}.`,
    `Ví dụ hư cấu: tấn công ${number.format(example.attackRate)} ở cấp ${number.format(example.attackerLevel)} gặp phòng thủ ${number.format(example.defenseRate)} ở cấp ${number.format(example.defenderLevel)} cho xác suất ${percent.format(example.chance)}.`,
  ));
  text('h3', label('Damage modifiers', 'Điều chỉnh sát thương'));
  text('p', label(
    `The duel opponent takes ${percent.format(hit.duelFactor)} of the damage at the duel adjustment step. Class tuning, skills, shield, and the following host limits can affect the final damage.`,
    `Ở bước điều chỉnh đấu tay đôi, sát thương lên đối thủ được nhân với ${percent.format(hit.duelFactor)}. Cân bằng lớp, kỹ năng, khiên và giới hạn máy chủ có thể thay đổi sát thương cuối cùng.`,
  ));
  const limits = server.limits;
  if (limits) {
    const list = text('ul', '');
    if (limits.pvpReduction) text('li', label(`PvP reduction · ${percent.format(limits.pvpReduction / 100)}`, `Giảm PvP · ${percent.format(limits.pvpReduction / 100)}`), list);
    if (limits.siegeMapReduction) text('li', label(`Siege map reduction · ${percent.format(limits.siegeMapReduction / 100)}`, `Giảm trên bản đồ công thành · ${percent.format(limits.siegeMapReduction / 100)}`), list);
    if (limits.damageCap) text('li', label(`Cap per hit · ${number.format(limits.damageCap)}`, `Trần mỗi đòn · ${number.format(limits.damageCap)}`), list);
    if (!list.children.length) list.remove();
  }

  const { loot } = server;
  text('h3', label('Monster experience and drops', 'Kinh nghiệm và đồ rơi từ quái'));
  text('p', loot.splitExpByDamage
    ? label('The server divides experience by damage, grouping party members before distributing the party share.', 'Máy chủ chia kinh nghiệm theo sát thương, gộp thành viên cùng nhóm trước khi phân bổ phần của nhóm.')
    : label('Experience goes to the finishing player or their party.', 'Kinh nghiệm thuộc về người kết liễu hoặc nhóm của họ.'));
  text('p', loot.dropToTopDamageDealer
    ? label('The highest damage dealer gets initial pickup rights. The last hit wins a tie when the finishing player is tied. Party members share those pickup rights.', 'Người gây nhiều sát thương nhất có quyền nhặt ban đầu. Khi người kết liễu đồng hạng cao nhất, người đó được ưu tiên. Thành viên cùng nhóm có chung quyền nhặt.')
    : label('The finishing player or their party gets initial pickup rights.', 'Người kết liễu hoặc nhóm của họ có quyền nhặt ban đầu.'));
  const a = loot.example;
  text('p', label(
    `Fictional solo example: A deals ${number.format(a.firstDamage)} damage and B finishes with ${number.format(a.finishingDamage)}. The experience shares before eligibility and modifiers are A ${percent.format(a.firstShare)} and B ${percent.format(a.finishingShare)}. ${loot.dropToTopDamageDealer ? 'A' : 'B'} gets initial pickup rights.`,
    `Ví dụ hư cấu khi chơi solo: A gây ${number.format(a.firstDamage)} sát thương, B kết liễu với ${number.format(a.finishingDamage)}. Trước khi xét điều kiện và hệ số, phần kinh nghiệm của A là ${percent.format(a.firstShare)}, của B là ${percent.format(a.finishingShare)}. ${loot.dropToTopDamageDealer ? 'A' : 'B'} có quyền nhặt ban đầu.`,
  ));

  const bossHeading = label('World boss MVP and reward rules', 'Quy tắc MVP và thưởng boss thế giới');
  text('h3', bossHeading);
  if (!server.bosses.length) text('p', label('No world bosses are configured on this server.', 'Máy chủ này chưa cấu hình boss thế giới.'));
  const bosses = text('div', '');
  bosses.className = 'cards';
  for (const boss of server.bosses) {
    const card = text('article', '', bosses);
    card.className = 'card';
    text('h4', boss.name[locale] || boss.name.en || boss.name.vi, card);
    text('p', boss.partyRank
      ? label('Ranked by combined party damage. Party members with damage share that rank.', 'Xếp hạng theo tổng sát thương của nhóm. Thành viên có gây sát thương nhận cùng hạng.')
      : label('Ranked by individual damage.', 'Xếp hạng theo sát thương cá nhân.'), card);
    if (boss.topRanks.length) {
      const ranks = boss.topRanks.map(rank => rank > 3 ? `4–${number.format(rank)}` : number.format(rank)).join(', ');
      text('p', label(`Rank rewards · ${ranks}`, `Hạng có thưởng · ${ranks}`), card);
    }
    if (boss.hasParticipation) {
      const minimum = percent.format(boss.participationMinimumPercent / 100);
      const requirement = boss.participationMinimumDamage > 0
        ? label(`${minimum} of damage or ${number.format(boss.participationMinimumDamage)} damage`, `${minimum} tổng sát thương hoặc ${number.format(boss.participationMinimumDamage)} sát thương`)
        : label(`${minimum} of damage`, `${minimum} tổng sát thương`);
      text('p', label(`Participation reward · at least ${requirement}`, `Thưởng tham gia · ít nhất ${requirement}`), card);
      text('p', boss.topAlsoGetParticipation
        ? label('Top-ranked contributors can also earn participation rewards.', 'Người xếp hạng cao vẫn có thể nhận thưởng tham gia.')
        : label('Only contributors outside the top ranks can earn participation rewards.', 'Chỉ người ngoài nhóm hạng đầu mới có thể nhận thưởng tham gia.'), card);
    }
    if (boss.hasLastHit) text('p', label('A separate last-hit reward is configured.', 'Có thưởng riêng cho người kết liễu.'), card);
  }
  if (server.moreBosses) text('p', label('There are more configured world bosses on this server.', 'Máy chủ này còn boss thế giới khác đã được cấu hình.'));
}

onLanguageChange(render);
void fetch('/website/combat-guide', { cache: 'no-store' }).then(async response => {
  if (!response.ok) throw new Error('Combat guide unavailable');
  guide = await response.json() as CombatGuide;
}).catch(() => { failed = true; }).finally(render);
